using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Converters;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;

public static class EffectNodeFactory
{
    private static readonly Dictionary<string, Type> TypeCache = new();

    private static readonly AssemblyBuilder AsmBuilder =
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("DynamicEffectNodes"),
            AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder ModBuilder =
        AsmBuilder.DefineDynamicModule("MainModule");

    private static readonly PersistedAssemblyBuilder DynamicAsmBuilder =
        new(
            new AssemblyName("DynamicEffectNodes"),
            typeof(object).Assembly);

    private static readonly ModuleBuilder DynamicModBuilder =
        DynamicAsmBuilder.DefineDynamicModule("MainModule");

    public static Type[] Create()
    {
        var result = new List<Type>();
        foreach (var effectType in PluginLoader.VideoEffects)
            try
            {
                var nodeType = GetOrCreate(effectType);
                Registry.RegisterType(nodeType);
                result.Add(nodeType);
#if DEBUG
                Console.WriteLine($@"[EffectNodeFactory] OK: {effectType.Name} -> {nodeType.FullName}");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine($@"[EffectNodeFactory] FAIL: {effectType.Name} -- {ex.GetType().Name}: {ex.Message}");
#endif
            }
#if DEBUG
        Console.WriteLine(
            $@"[EffectNodeFactory] Generated {result.Count} / {PluginLoader.VideoEffects.Count()} effects.");
#endif
        DynamicAsmBuilder.Save("DynamicEffectNodes.dll");
        return result.ToArray();
    }

    private static Type GetOrCreate(Type effectType)
    {
        if (effectType.GetCustomAttribute<ObsoleteAttribute>() != null)
            throw new InvalidOperationException($"{effectType.Name} is obsolete");

        if (TypeCache.TryGetValue(effectType.Name, out var cached))
        {
#if DEBUG
            Console.WriteLine($@"[EffectNodeFactory]   cache hit: {effectType.Name}");
#endif
            return cached;
        }

        var effectInstance = Activator.CreateInstance(effectType) as IVideoEffect
                             ?? throw new InvalidOperationException($"Cannot instantiate {effectType.Name}");

        var (staticPortDefs, dynamicParams) = EffectPortCollector.Collect(effectInstance);

#if DEBUG
        Console.WriteLine(
            $@"[EffectNodeFactory]   {effectType.Name}: {staticPortDefs.Count + dynamicParams.Count} port(s) collected (including {dynamicParams.Count} dynamics)");
        foreach (var d in staticPortDefs)
            Console.WriteLine($@"[EffectNodeFactory]     {d.PortType,-6} {d.PropName} (label={d.LabelKey})");
        foreach (var d in dynamicParams)
            Console.WriteLine(
                $@"[EffectNodeFactory]     {d.Item1.PortType,-6} {d.Item1.PropName} (Dynamic, label={d.Item1.LabelKey})");
#endif

        var veAttr = effectType.GetCustomAttribute<VideoEffectAttribute>();
        var categoryKey = veAttr?.Categories.FirstOrDefault() ?? "Effect";
        var labelKey = veAttr?.Name ?? effectType.Name;
        var resourceType = veAttr?.ResourceType;

        var generated =
            EffectNodeTypeBuilder.Build(ModBuilder, effectType.Name, categoryKey, labelKey, resourceType,
                staticPortDefs, dynamicParams);
        EffectNodeTypeBuilder.Build(DynamicModBuilder, effectType.Name, categoryKey, labelKey, resourceType,
            staticPortDefs, dynamicParams);
        TypeCache[effectType.Name] = generated;
        return generated;
    }

    internal static string? GetEffectName(string assemblyQualifiedName)
    {
        foreach (var (effectName, type) in TypeCache)
            if ((type.AssemblyQualifiedName ?? type.Name) == assemblyQualifiedName)
                return effectName;
        return null;
    }

    internal static Type? GetOrCreate(string effectName)
    {
        if (TypeCache.TryGetValue(effectName, out var cached))
            return cached;

        var effectType = PluginLoader.VideoEffects.FirstOrDefault(t => t.Name == effectName);
        if (effectType == null) return null;

        try
        {
            return GetOrCreate(effectType);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
///     IVideoEffect のプロパティを1つのノード InputPort として表現するためのデータクラス。
///     EffectPortCollector が収集し、EffectNodeTypeBuilder と EffectNodeCalculator が参照する。
/// </summary>
public sealed class PortDefinition
{
    /// <summary>
    ///     IVideoEffect 側の C# プロパティ名。
    /// </summary>
    public required string PropName { get; init; }

    public required PortType PortType { get; init; }

    /// <summary>[Display].Name の値（YMM4 リソースキーまたはリテラル文字列）。</summary>
    public required string LabelKey { get; init; }

    /// <summary>[Display].Description の値。</summary>
    public required string DescKey { get; init; }

    /// <summary>[Display].ResourceType の値。null の場合は LabelKey をリテラルとして使う。</summary>
    public Type? ResourceType { get; init; }

    // 共通
    public object? DefaultValue { get; init; }

    // Float / Animation 型プロパティ用
    public float Min { get; init; } = float.NaN;
    public float Max { get; init; } = float.NaN;
    public int Digits { get; init; } = 2;
    public string Unit { get; init; } = "";

    // Enum 型プロパティ用
    public Type? EnumType { get; init; }
}

public enum PortType
{
    Float,
    Enum,
    Bool,
    Color,
    Brush,
    Unknown
}

public static class EffectPortCollector
{
    public static (List<PortDefinition>, List<(PortDefinition, PropertyInfo, object)>) Collect(object root)
    {
        List<PortDefinition> staticResult = [];
        List<(PortDefinition, PropertyInfo, object)> dynamicResult = [];
        CollectRecursive(root, staticResult, dynamicResult);
        return (staticResult, dynamicResult);
    }

    private static void CollectRecursive(
        object obj,
        List<PortDefinition> staticResult,
        List<(PortDefinition, PropertyInfo, object)> dynamicResult,
        HashSet<object>? visited = null)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(obj)) return;

        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<ObsoleteAttribute>() != null) continue;

            var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr == null) continue;

            var propInstance = prop.GetValue(obj);

            var def = TryMakePortDefinition(prop, displayAttr, propInstance);

            if (propInstance is not null && propInstance.GetType().GetProperties()
                    .Any(info => info.GetCustomAttribute<DisplayAttribute>() != null))
            {
                dynamicResult.Add((def, prop, propInstance));
                continue;
            }

            staticResult.Add(def);
        }
    }

    private static PortDefinition TryMakePortDefinition(PropertyInfo prop, DisplayAttribute display, object? inst)
    {
        var labelKey = display.Name ?? prop.Name;
        var descKey = display.Description ?? "";
        var resourceType = display.ResourceType;

        if (prop.PropertyType == typeof(Animation))
        {
            var slider = prop.GetCustomAttribute<AnimationSliderAttribute>();
            var defaultValue = (inst as Animation)?.DefaultValue;
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Float,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = defaultValue,
                Min = slider != null ? (float)slider.DefaultMin : float.NaN,
                Max = slider != null ? (float)slider.DefaultMax : float.NaN,
                Digits = slider?.StringFormat != null ? ParseDigits(slider.StringFormat) : 2,
                Unit = slider?.UnitText ?? ""
            };
        }

        if (prop.PropertyType.IsEnum)
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Enum,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = inst,
                EnumType = prop.PropertyType
            };

        if (prop.PropertyType == typeof(bool))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Bool,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = inst
            };

        if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(double) ||
            prop.PropertyType == typeof(int))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Float,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = inst
            };

        if (prop.PropertyType == typeof(Color))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Color,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = inst is Color c ? c : Colors.White
            };

        if (prop.PropertyType == typeof(Plugin.Brush.Brush))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Brush,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = null
            };

        return new PortDefinition
        {
            PropName = prop.Name,
            PortType = PortType.Unknown,
            LabelKey = labelKey,
            DescKey = descKey,
            ResourceType = resourceType,
            DefaultValue = null
        };
    }

    private static int ParseDigits(string format)
    {
        if (format.Length >= 2 && (format[0] == 'F' || format[0] == 'f')
                               && int.TryParse(format[1..], out var d))
            return d;
        return 2;
    }
}

public static class EffectNodeTypeBuilder
{
    public static Type Build(
        ModuleBuilder mod,
        string effectName,
        string categoryKey,
        string labelKey,
        Type? resourceType,
        List<PortDefinition> staticPortDefs,
        List<(PortDefinition, PropertyInfo, object)> dynamicPropertyDefs)
    {
        var typeName = $"DynamicEffectNode_{effectName}";
        var tb = mod.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            typeof(NodeLogic));

        tb.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(NodeAttribute).GetConstructors()[1],
            [
                "NodeEffectKey_EffectCategoryName/NodeEffectKey_VideoEffectCategoryName" +
                (string.IsNullOrEmpty(categoryKey) ? "" : "/" + categoryKey),
                labelKey,
                labelKey,
                resourceType
            ]));

        var loaderField = tb.DefineField("_videoEffect", typeof(VideoEffectsLoader), FieldAttributes.Private);

        EffectNodeCalculator.RegisterPortDefs(effectName, staticPortDefs.ToArray(),
            dynamicPropertyDefs.Select(d => d.Item2.Name).ToArray(),
            dynamicPropertyDefs.Select(d => $"_cachedSubType_{d.Item2.Name}").ToArray());

        var effectNameField = tb.DefineField("_effectNameCache", typeof(string),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        var portDefsField = tb.DefineField("_portDefs", typeof(PortDefinition[]),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        var containerBackFields = dynamicPropertyDefs.Select(props =>
            tb.DefineField($"_{props.Item2.Name}", typeof(InputsContainer), FieldAttributes.Private)).ToList();

        var cctor = tb.DefineTypeInitializer();
        var cil = cctor.GetILGenerator();
        cil.Emit(OpCodes.Ldstr, effectName);
        cil.Emit(OpCodes.Stsfld, effectNameField);
        cil.Emit(OpCodes.Ldstr, effectName);
        cil.Emit(OpCodes.Call, typeof(EffectNodeCalculator).GetMethod(nameof(EffectNodeCalculator.GetPortDefs))!);
        cil.Emit(OpCodes.Stsfld, portDefsField);

        cil.Emit(OpCodes.Ret);

        EmitImageInputPort(tb);
        foreach (var def in staticPortDefs) EmitParameterPort(tb, def);
        for (var index = 0; index < dynamicPropertyDefs.Count; index++)
        {
            var props = dynamicPropertyDefs[index];
            EmitContainerPort(tb, props, containerBackFields[index]);
        }

        EmitImageOutputPort(tb);
        EmitCalculate(tb, loaderField, effectNameField, portDefsField);

        if (dynamicPropertyDefs.Count > 0)
            EmitOnInputValueChanged(tb, loaderField, effectNameField, dynamicPropertyDefs, containerBackFields);

        var baseCtor = typeof(NodeLogic).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!;

        var ctor = tb.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard, Type.EmptyTypes);
        var il = ctor.GetILGenerator();

        for (var i = 0; i < dynamicPropertyDefs.Count; i++)
        {
            var def = dynamicPropertyDefs[i];
            var field = containerBackFields[i];
            var containerType = ContainerFactory.CreateOrGenerate(def.Item3, mod);
            var containerCtor = containerType?.GetConstructor(Type.EmptyTypes);
            if (containerCtor == null)
                throw new InvalidOperationException($"No parameterless ctor: {containerType}");

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Newobj, containerCtor);
            il.Emit(OpCodes.Stfld, field);
        }

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);

        return tb.CreateType()
               ?? throw new InvalidOperationException($"Failed to create type for {effectName}");
    }

    private static void EmitImageInputPort(TypeBuilder tb)
    {
        var pb = EmitInputProperty(tb, "InputImage", typeof(ImageWrapper));
        Attr.InputPort(pb, nameof(TextUi.Input), "", typeof(TextUi));
        Attr.PortColor(pb, nameof(Colors.CornflowerBlue));
    }

    private static void EmitParameterPort(TypeBuilder tb, PortDefinition def)
    {
        var clrType = def.PortType switch
        {
            PortType.Enum => typeof(int),
            PortType.Bool => typeof(bool),
            PortType.Color => typeof(Color),
            PortType.Brush => typeof(BrushWrapper),
            _ => typeof(float)
        };

        var pb = EmitInputProperty(tb, def.PropName, clrType);
        Attr.InputPort(pb, def.LabelKey, def.DescKey, def.ResourceType);

        switch (def.PortType)
        {
            case PortType.Float:
                Attr.NumberControl(pb, def.Min, def.Max, def.Digits, def.Unit,
                    def.DefaultValue is null
                        ? 0f
                        : Convert.ToSingle(def.DefaultValue));
                Attr.PortColor(pb, nameof(Colors.DarkOrange));
                break;
            case PortType.Enum:
                Attr.EnumControl(pb, def.EnumType!, Convert.ToInt32((Enum?)def.DefaultValue));
                Attr.PortColor(pb, nameof(Colors.DarkOrange));
                break;
            case PortType.Bool:
                Attr.BoolControl(pb, (bool)(def.DefaultValue ?? false));
                break;
            case PortType.Color:
                Attr.ColorControl(pb, (Color)(def.DefaultValue ?? Colors.White));
                Attr.PortColor(pb, nameof(Colors.MediumPurple));
                break;
            case PortType.Brush:
                Attr.PortColor(pb, nameof(Colors.LawnGreen));
                break;
        }
    }

    private static void EmitContainerPort(TypeBuilder tb, (PortDefinition port, PropertyInfo prop, object _) info,
        FieldInfo field)
    {
        var pb = EmitContainerProperty(tb, info.prop.Name, field);
        Attr.InputPort(pb, info.port.LabelKey, info.port.DescKey, info.port.ResourceType, true);
    }

    private static void EmitImageOutputPort(TypeBuilder tb)
    {
        var pb = EmitOutputProperty(tb, "Output", typeof(ImageWrapper));
        Attr.OutputPort(pb, nameof(TextUi.Output), "", typeof(TextUi));
        Attr.PortColor(pb, nameof(Colors.CornflowerBlue));
    }

    /// <summary>
    ///     OnInputValueChanged override を Emit する。
    ///     _videoEffect が非 null の場合、Task.Run で Effect への書き込みと Dynamic コンテナ差し替えを行う。
    /// </summary>
    private static void EmitOnInputValueChanged(
        TypeBuilder tb,
        FieldBuilder loaderField,
        FieldBuilder effectNameField,
        List<(PortDefinition, PropertyInfo, object)> dynamicPropertyDefs,
        List<FieldBuilder> containerBackFields)
    {
        var refreshMethod = typeof(EffectNodeCalculator)
            .GetMethod(nameof(EffectNodeCalculator.RefreshDynamicContainer),
                BindingFlags.Public | BindingFlags.Static)!;
        var loadEffectSyncMethod = typeof(VideoEffectsLoader)
            .GetMethod(nameof(VideoEffectsLoader.LoadEffectSync),
                BindingFlags.Public | BindingFlags.Static,
                null, [typeof(string)], null)!;

        var m = tb.DefineMethod("OnInputValueChanged",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig |
            MethodAttributes.FamORAssem,
            typeof(void), [typeof(string), typeof(object)]);

        var il = m.GetILGenerator();

        // if (_videoEffect == null) _videoEffect = VideoEffectsLoader.LoadEffectSync(_effectNameCache);
        var notNullLabel = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, loaderField);
        il.Emit(OpCodes.Brtrue_S, notNullLabel);
        // _videoEffect = VideoEffectsLoader.LoadEffectSync(_effectNameCache);
        // Stfld は (obj, value) をスタックから消費するため Ldarg_0 を先に積む
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsfld, effectNameField);
        il.Emit(OpCodes.Call, loadEffectSyncMethod);
        il.Emit(OpCodes.Stfld, loaderField);
        il.MarkLabel(notNullLabel);

        // foreach dynamic prop:
        // EffectNodeCalculator.RefreshDynamicContainer(
        //     this, _videoEffect, portName, value, "PropName", "_PropName");
        for (var i = 0; i < dynamicPropertyDefs.Count; i++)
        {
            var def = dynamicPropertyDefs[i];
            var containerFieldName = containerBackFields[i].Name;

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldfld, loaderField);
            il.Emit(OpCodes.Ldarg_1);
            il.Emit(OpCodes.Ldarg_2);
            il.Emit(OpCodes.Ldstr, def.Item2.Name);
            il.Emit(OpCodes.Ldstr, containerFieldName);
            il.Emit(OpCodes.Call, refreshMethod);
        }

        il.Emit(OpCodes.Ret);

        tb.DefineMethodOverride(m,
            typeof(NodeLogic).GetMethod("OnInputValueChanged",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!);
    }

    private static PropertyBuilder EmitInputProperty(TypeBuilder tb, string name, Type t)
    {
        var getMethod = typeof(NodeLogic)
            .GetMethod("GetInput", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(t);
        var setMethod = typeof(NodeLogic)
            .GetMethod("SetInput", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(t);

        var getter = tb.DefineMethod($"get_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            t, Type.EmptyTypes);
        var gil = getter.GetILGenerator();
        gil.Emit(OpCodes.Ldarg_0);
        gil.Emit(OpCodes.Ldstr, name);
        gil.Emit(OpCodes.Call, getMethod);
        gil.Emit(OpCodes.Ret);

        var setter = tb.DefineMethod($"set_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [t]);
        var sil = setter.GetILGenerator();
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldarg_1);
        sil.Emit(OpCodes.Ldstr, name);
        sil.Emit(OpCodes.Call, setMethod);
        sil.Emit(OpCodes.Ret);

        var pb = tb.DefineProperty(name, PropertyAttributes.None, t, null);
        pb.SetGetMethod(getter);
        pb.SetSetMethod(setter);
        return pb;
    }

    private static PropertyBuilder EmitContainerProperty(TypeBuilder tb, string name, FieldInfo field)
    {
        var setMethod = typeof(NodeLogic)
            .GetMethod(nameof(NodeLogic.SetDynamicContainer),
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)!;

        var getter = tb.DefineMethod($"get_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            typeof(InputsContainer), Type.EmptyTypes);
        var gil = getter.GetILGenerator();
        gil.Emit(OpCodes.Ldarg_0);
        gil.Emit(OpCodes.Ldfld, field);
        gil.Emit(OpCodes.Ret);

        var setter = tb.DefineMethod($"set_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [typeof(InputsContainer)]);
        var sil = setter.GetILGenerator();
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldarg_1);
        sil.Emit(OpCodes.Ldstr, name);
        sil.Emit(OpCodes.Call, setMethod);
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldarg_1);
        sil.Emit(OpCodes.Stfld, field);
        sil.Emit(OpCodes.Ret);

        var pb = tb.DefineProperty(name, PropertyAttributes.None, typeof(InputsContainer), null);
        pb.SetGetMethod(getter);
        pb.SetSetMethod(setter);
        return pb;
    }

    private static PropertyBuilder EmitOutputProperty(TypeBuilder tb, string name, Type t)
    {
        var getMethod = typeof(NodeLogic)
            .GetMethod("GetOutput", BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(t);
        var setOutputMethod = typeof(NodeLogic)
            .GetMethod("SetOutput", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var getter = tb.DefineMethod($"get_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            t, Type.EmptyTypes);
        var gil = getter.GetILGenerator();
        gil.Emit(OpCodes.Ldarg_0);
        gil.Emit(OpCodes.Ldstr, name);
        gil.Emit(OpCodes.Call, getMethod);
        gil.Emit(OpCodes.Ret);

        var setter = tb.DefineMethod($"set_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [t]);
        var sil = setter.GetILGenerator();
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldarg_1);
        if (t.IsValueType) sil.Emit(OpCodes.Box, t);
        sil.Emit(OpCodes.Ldstr, name);
        sil.Emit(OpCodes.Call, setOutputMethod);
        sil.Emit(OpCodes.Ret);

        var pb = tb.DefineProperty(name, PropertyAttributes.None, t, null);
        pb.SetGetMethod(getter);
        pb.SetSetMethod(setter);
        return pb;
    }

    private static void EmitCalculate(
        TypeBuilder tb,
        FieldBuilder loaderField,
        FieldBuilder effectNameField,
        FieldBuilder portDefsField)
    {
        var calcTarget = typeof(EffectNodeCalculator)
            .GetMethod(nameof(EffectNodeCalculator.Calculate), BindingFlags.Public | BindingFlags.Static)!;

        var m = tb.DefineMethod("Calculate",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task), Type.EmptyTypes);

        var il = m.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsfld, effectNameField);
        il.Emit(OpCodes.Ldsfld, portDefsField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, loaderField);
        il.Emit(OpCodes.Call, calcTarget);
        il.Emit(OpCodes.Ret);

        tb.DefineMethodOverride(m,
            typeof(NodeLogic).GetMethod("Calculate", BindingFlags.NonPublic | BindingFlags.Instance)!);
    }
}

/// <summary>
///     動的生成した NodeLogic サブクラスの Calculate() から呼ばれる静的ヘルパー。
/// </summary>
public static class EffectNodeCalculator
{
    private static readonly Dictionary<string, PortDefinition[]> PortDefsRegistry = new();

    private static readonly Dictionary<string, string[]> DynamicPropNamesRegistry = new();

    // effectName -> propName -> (cachedSubTypeFieldName, containerFieldName)
    private static readonly Dictionary<string, Dictionary<string, (string subTypeField, string containerField)>>
        DynamicFieldNamesRegistry = new();

    public static void RegisterPortDefs(
        string effectName,
        PortDefinition[] defs,
        string[] dynamicPropNames,
        string[] cachedSubTypeFieldNames)
    {
        PortDefsRegistry[effectName] = defs;
        DynamicPropNamesRegistry[effectName] = dynamicPropNames;
        var fieldMap = new Dictionary<string, (string, string)>();
        for (var i = 0; i < dynamicPropNames.Length; i++)
            fieldMap[dynamicPropNames[i]] = (cachedSubTypeFieldNames[i], $"_{dynamicPropNames[i]}");
        DynamicFieldNamesRegistry[effectName] = fieldMap;
    }

    public static PortDefinition[] GetPortDefs(string effectName)
    {
        return PortDefsRegistry.TryGetValue(effectName, out var d) ? d : [];
    }

    /// <summary>
    ///     OnInputValueChanged から各 Dynamic プロパティごとに呼ばれる。
    ///     Task.Run 内で BeginEdit → 値書き込み → EndEditAsync を実行し、
    ///     Effect 側の型切り替えが完了した後にコンテナを差し替える。
    ///     戻り値なし（fire-and-forget）。
    /// </summary>
    public static void RefreshDynamicContainer(
        NodeLogic self,
        VideoEffectsLoader loader,
        string changedPortName,
        object? changedValue,
        string dynamicPropName,
        string containerFieldName)
    {
        // 値を先にキャプチャしておく（Task.Run に渡すため）
        var nodeType = self.GetType();
        var containerFieldInfo = nodeType.GetField(containerFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (containerFieldInfo == null) return;

        _ = Task.Run(async () =>
        {
            // VideoEffectsLoader.SetValue は BeginEdit → 値書き込み → EndEditAsync を内部で行う。
            // これにより Effect 側の EndEditAsync 内での型切り替えが発生する。
            await loader.SetValue(changedPortName, changedValue).ConfigureAwait(false);

            // 型切り替え後にサブオブジェクト型を確認する
            var subObject = GetEffectSubObject(loader, dynamicPropName);
            if (subObject == null) return;

            var currentType = subObject.GetType();
            var containerType = ContainerFactory.CreateOrGenerate(subObject);
            if (containerType == null) return;
            if (currentType == containerType) return;

            // 型が変わった: 新しいコンテナを生成してプロパティ setter 経由でセットする
            var newContainer = (InputsContainer?)Activator.CreateInstance(containerType);
            if (newContainer == null) return;

            containerFieldInfo.SetValue(self, newContainer);

            // SetDynamicContainer を発火させて VM に通知する
            self.SetDynamicContainer(newContainer, dynamicPropName);
        });
    }

    public static Task Calculate(
        NodeLogic self,
        string effectName,
        PortDefinition[] portDefs,
        ref VideoEffectsLoader? loaderRef)
    {
        var ctx = (EvaluationContext?)typeof(NodeLogic)
            .GetProperty("EvaluationContext", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(self);

        if (ctx is null)
            return Task.FromException(new NullReferenceException("EvaluationContext"));

        var inputImage = self.GetType().GetProperty("InputImage")?.GetValue(self) as ImageWrapper;
        if (inputImage?.Image is null || inputImage.Image.NativePointer == nint.Zero)
            return Task.FromException(new NullReferenceException("InputImage"));

        loaderRef ??= VideoEffectsLoader.LoadEffectSync(effectName);

        var loader = loaderRef;
        var dynamicPropNames = DynamicPropNamesRegistry.TryGetValue(effectName, out var names) ? names : [];
        return Task.Run(async () =>
        {
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                await CalculateAsync(self, portDefs, dynamicPropNames, loader, ctx, inputImage).ConfigureAwait(false);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        });
    }

    private static Task CalculateAsync(
        NodeLogic self,
        PortDefinition[] portDefs,
        string[] dynamicPropNames,
        VideoEffectsLoader loader,
        EvaluationContext ctx,
        ImageWrapper inputImage)
    {
        try
        {
            // 静的ポートを Effect に書き込む
            foreach (var def in portDefs)
            {
                var value = GetPortValue(self, def);
                SetPropertyDirect(loader, def.PropName, value);
            }

            // Dynamic ポートのサブポートを Effect のサブオブジェクトに書き込む
            // キー形式: "PropName.SubPropName"
            foreach (var propName in dynamicPropNames)
            {
                var subObject = GetEffectSubObject(loader, propName);
                if (subObject == null) continue;

                var prefix = propName + ".";

                // Effect のサブオブジェクトが持つ [Display] プロパティ名の集合を取得する
                var effectSubPropNames = subObject.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<DisplayAttribute>() != null)
                    .Select(p => prefix + p.Name)
                    .ToHashSet();

                // Inputs に登録済みのサブポートキーの集合を取得する
                var registeredKeys = self.Inputs.Keys
                    .Where(k => k.StartsWith(prefix))
                    .ToHashSet();

                // 不一致の場合: コンテナ型が切り替わっているが Inputs がまだ古い状態。
                // プロパティ setter 経由で SetDynamicContainer を発火させて Inputs を同期する。
                if (!effectSubPropNames.SetEquals(registeredKeys))
                {
                    var containerType = ContainerFactory.CreateOrGenerate(subObject);
                    if (containerType != null)
                    {
                        var newContainer = (InputsContainer?)Activator.CreateInstance(containerType);
                        if (newContainer != null)
                            // プロパティ setter 経由で SetDynamicContainer を発火させる
                            self.GetType().GetProperty(propName)?.SetValue(self, newContainer);
                    }
                }

                foreach (var kv in self.Inputs)
                {
                    if (!kv.Key.StartsWith(prefix)) continue;
                    var subPropName = kv.Key.Substring(prefix.Length);
                    var subProp = subObject.GetType().GetProperty(subPropName,
                        BindingFlags.Public | BindingFlags.Instance);
                    if (subProp == null) continue;

                    var raw = kv.Value.GetValue(null).GetAwaiter().GetResult();
                    SetSubPropertyDirect(subObject, subProp, raw);
                }
            }

            if (loader.Update(out var output, ctx, inputImage.Image))
                self.GetType().GetProperty("Output")?.SetValue(self, new ImageWrapper { Image = output });
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private static void SetSubPropertyDirect(object target, PropertyInfo propInfo, object? value)
    {
        if (propInfo.PropertyType == typeof(Animation))
        {
            if (propInfo.GetValue(target) is not Animation anim) return;
            var valuesProp = typeof(Animation).GetProperty("Values", BindingFlags.Public | BindingFlags.Instance);
            var values = valuesProp?.GetValue(anim) as ImmutableList<AnimationValue>;
            if (values == null) return;
            var doubleVal = Convert.ToDouble(value ?? 0);
            var newValues = values.Count > 0
                ? values.SetItem(0, new AnimationValue(doubleVal))
                : values.Add(new AnimationValue(doubleVal));
            valuesProp!.SetValue(anim, newValues);
        }
        else
        {
            if (!propInfo.CanWrite) return;
            propInfo.SetValue(target, value);
        }
    }

    private static object? GetEffectSubObject(VideoEffectsLoader loader, string propName)
    {
        var effectField = typeof(VideoEffectsLoader)
            .GetField("_videoEffect", BindingFlags.NonPublic | BindingFlags.Instance);
        var effectInstance = effectField?.GetValue(loader);
        if (effectInstance == null) return null;

        var result = FindPropertyByDisplay(effectInstance, propName);
        if (result == null) return null;

        var (target, propInfo) = result.Value;
        try
        {
            return propInfo.GetValue(target);
        }
        catch
        {
            return null;
        }
    }

    private static void SetPropertyDirect(VideoEffectsLoader loader, string propName, object? value)
    {
        var effectField = typeof(VideoEffectsLoader)
            .GetField("_videoEffect", BindingFlags.NonPublic | BindingFlags.Instance);
        var effectInstance = effectField?.GetValue(loader);
        if (effectInstance == null) return;

        var result = FindPropertyByDisplay(effectInstance, propName);
        if (result == null) return;

        var (target, propInfo) = result.Value;

        if (propInfo.PropertyType == typeof(Animation))
        {
            if (propInfo.GetValue(target) is not Animation anim) return;
            var valuesProp = typeof(Animation).GetProperty("Values",
                BindingFlags.Public | BindingFlags.Instance);
            var values = valuesProp?.GetValue(anim) as ImmutableList<AnimationValue>;
            if (values == null) return;
            var doubleVal = Convert.ToDouble(value ?? 0);
            var newValues = values.Count > 0
                ? values.SetItem(0, new AnimationValue(doubleVal))
                : values.Add(new AnimationValue(doubleVal));
            valuesProp!.SetValue(anim, newValues);
        }
        else
        {
            if (!propInfo.CanWrite) return;
            propInfo.SetValue(target, value);
        }
    }

    private static (object target, PropertyInfo property)? FindPropertyByDisplay(object? obj, string name)
    {
        if (obj == null) return null;
        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr != null && prop.Name == name) return (obj, prop);
            if (!prop.CanRead || !prop.PropertyType.IsClass || prop.PropertyType == typeof(string)) continue;
            object? sub;
            try
            {
                sub = prop.GetValue(obj);
            }
            catch
            {
                continue;
            }

            if (sub == null) continue;
            var result = FindPropertyByDisplay(sub, name);
            if (result != null) return result;
        }

        return null;
    }

    private static object? GetPortValue(NodeLogic self, PortDefinition def)
    {
        if (!self.Inputs.TryGetValue(def.PropName, out var port)) return null;

        var raw = port.GetValue(null).GetAwaiter().GetResult();

        return def.PortType switch
        {
            PortType.Enum when def.EnumType != null =>
                raw is int i ? Enum.ToObject(def.EnumType, i) : raw,
            PortType.Float =>
                raw is float f ? f : raw != null ? Convert.ToSingle(raw) : (object)0f,
            PortType.Color =>
                raw ?? Colors.White,
            _ => raw
        };
    }
}

/// <summary>
///     Emit でプロパティに属性を付与するための静的ヘルパー。
/// </summary>
internal static class Attr
{
    private static readonly ConstructorInfo InputPortCtor =
        typeof(InputPortAttribute).GetConstructor(
            [typeof(string), typeof(string), typeof(Type), typeof(bool)])!;

    private static readonly ConstructorInfo OutputPortCtor =
        typeof(OutputPortAttribute).GetConstructor([typeof(string), typeof(string), typeof(Type)])!;

    private static readonly ConstructorInfo PortColorCtor =
        typeof(PortColorSettingAttribute).GetConstructor([typeof(string)])!;

    private static readonly ConstructorInfo NumberCtor =
        typeof(NumberPortControlAttribute).GetConstructor(Type.EmptyTypes)!;

    private static readonly ConstructorInfo EnumCtor =
        typeof(EnumPortControlAttribute).GetConstructor(Type.EmptyTypes)!;

    private static readonly ConstructorInfo BoolCtor =
        typeof(BoolPortControlAttribute).GetConstructor(Type.EmptyTypes)!;

    private static readonly ConstructorInfo ColorCtor =
        typeof(ColorPortControlAttribute).GetConstructor(Type.EmptyTypes)!;

    public static void InputPort(PropertyBuilder pb, string label, string desc, Type? resourceType,
        bool isDynamic = false)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(InputPortCtor, [label, desc, resourceType!, isDynamic]));
    }

    public static void OutputPort(PropertyBuilder pb, string label, string desc, Type? resourceType)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(OutputPortCtor, [label, desc, resourceType!]));
    }

    public static void PortColor(PropertyBuilder pb, string colorName)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(PortColorCtor, [colorName]));
    }

    public static void NumberControl(PropertyBuilder pb, float min, float max, int digits, string unit,
        float defaultValue)
    {
        var minP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Min))!;
        var maxP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Max))!;
        var digP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Digits))!;
        var unitP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Unit))!;
        var defaultP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Default))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(NumberCtor, [], [minP, maxP, digP, unitP, defaultP],
            [min, max, digits, unit, defaultValue]));
    }

    public static void EnumControl(PropertyBuilder pb, Type enumType, int defaultValue)
    {
        var itemsP = typeof(EnumPortControlAttribute).GetProperty(nameof(EnumPortControlAttribute.Items))!;
        var editP = typeof(EnumPortControlAttribute).GetProperty(nameof(EnumPortControlAttribute.IsEditable))!;
        var defP = typeof(EnumPortControlAttribute).GetProperty(nameof(EnumPortControlAttribute.Default))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(EnumCtor, [], [itemsP, editP, defP],
            [enumType, false, defaultValue]));
    }

    public static void BoolControl(PropertyBuilder pb, bool defaultValue)
    {
        var defP = typeof(BoolPortControlAttribute).GetProperty(nameof(BoolPortControlAttribute.Default))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(BoolCtor, [], [defP],
            [defaultValue]));
    }

    public static void ColorControl(PropertyBuilder pb, Color defaultValue)
    {
        var defP = typeof(ColorPortControlAttribute).GetProperty(nameof(ColorPortControlAttribute.DefaultColor))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(ColorCtor, [], [defP],
            [ColorStringConverter.ToString(defaultValue)]));
    }
}