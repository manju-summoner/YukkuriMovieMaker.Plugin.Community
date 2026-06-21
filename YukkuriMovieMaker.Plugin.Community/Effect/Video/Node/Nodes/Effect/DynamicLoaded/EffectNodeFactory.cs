using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;
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
    private static readonly Lock Lock = new();
    private static readonly Dictionary<string, Type> TypeCache = new();

    private static readonly AssemblyBuilder AsmBuilder =
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("DynamicEffectNodes"),
            AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder ModBuilder =
        AsmBuilder.DefineDynamicModule("MainModule");

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
        return result.ToArray();
    }

    private static Type GetOrCreate(Type effectType)
    {
        if (effectType.GetCustomAttribute<ObsoleteAttribute>() != null)
            throw new InvalidOperationException($"{effectType.Name} is obsolete");

        lock (Lock)
        {
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
            TypeCache[effectType.Name] = generated;
            return generated;
        }
    }

    internal static string? GetEffectName(string assemblyQualifiedName)
    {
        lock (Lock)
        {
            foreach (var (effectName, type) in TypeCache)
                if ((type.AssemblyQualifiedName ?? type.Name) == assemblyQualifiedName)
                    return effectName;
            return null;
        }
    }

    internal static Type? GetOrCreate(string effectName)
    {
        lock (Lock)
        {
            if (TypeCache.TryGetValue(effectName, out var cached))
                return cached;
        }

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

public sealed class PortDefinition
{
    public required string PropName { get; init; }
    public required PortType PortType { get; init; }
    public required string LabelKey { get; init; }
    public required string DescKey { get; init; }
    public Type? ResourceType { get; init; }
    public object? DefaultValue { get; init; }
    public float Min { get; init; } = float.NaN;
    public float Max { get; init; } = float.NaN;
    public int Digits { get; init; } = 2;
    public string Unit { get; init; } = "";
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

            if (propInstance is not null
                && prop.PropertyType != typeof(Plugin.Brush.Brush)
                && propInstance.GetType().GetProperties()
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
        var labelKey = display.Name ?? display.GroupName ?? prop.Name;
        var descKey = display.Description ?? "";
        var resourceType = display.ResourceType;

        if (prop.PropertyType == typeof(Animation))
        {
            var slider = prop.GetCustomAttribute<AnimationSliderAttribute>();
            var defaultValue = (inst as Animation)?.DefaultValue;
            return new PortDefinition
            {
                PropName = prop.Name, PortType = PortType.Float,
                LabelKey = labelKey, DescKey = descKey, ResourceType = resourceType,
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
                PropName = prop.Name, PortType = PortType.Enum,
                LabelKey = labelKey, DescKey = descKey, ResourceType = resourceType,
                DefaultValue = inst, EnumType = prop.PropertyType
            };

        if (prop.PropertyType == typeof(bool))
            return new PortDefinition
            {
                PropName = prop.Name, PortType = PortType.Bool,
                LabelKey = labelKey, DescKey = descKey, ResourceType = resourceType,
                DefaultValue = inst
            };

        if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(double) ||
            prop.PropertyType == typeof(int))
            return new PortDefinition
            {
                PropName = prop.Name, PortType = PortType.Float,
                LabelKey = labelKey, DescKey = descKey, ResourceType = resourceType,
                DefaultValue = inst
            };

        if (prop.PropertyType == typeof(Color))
            return new PortDefinition
            {
                PropName = prop.Name, PortType = PortType.Color,
                LabelKey = labelKey, DescKey = descKey, ResourceType = resourceType,
                DefaultValue = inst is Color c ? c : Colors.White
            };

        if (prop.PropertyType == typeof(Plugin.Brush.Brush))
            return new PortDefinition
            {
                PropName = prop.Name, PortType = PortType.Brush,
                LabelKey = display.Name ?? TextNode.BrushName ?? prop.Name, DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = null
            };

        return new PortDefinition
        {
            PropName = prop.Name, PortType = PortType.Unknown,
            LabelKey = labelKey, DescKey = descKey, ResourceType = resourceType,
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
                labelKey, labelKey, resourceType
            ]));

        var loaderField = tb.DefineField("_videoEffect", typeof(VideoEffectsLoader), FieldAttributes.Private);

        EffectNodeCalculator.RegisterPortDefs(effectName, staticPortDefs.ToArray(),
            dynamicPropertyDefs.Select(d => d.Item2.Name).ToArray());

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
            EmitContainerPort(tb, dynamicPropertyDefs[index], containerBackFields[index]);

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
                    def.DefaultValue is null ? 0f : Convert.ToSingle(def.DefaultValue));
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
    ///     _videoEffect が null の場合は LoadEffectSync で初期化する。
    ///     各 Dynamic プロパティについて RefreshDynamicContainer を呼ぶ。
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
        il.Emit(OpCodes.Ldarg_0); // obj for Stfld
        il.Emit(OpCodes.Ldsfld, effectNameField); // string
        il.Emit(OpCodes.Call, loadEffectSyncMethod); // VideoEffectsLoader
        il.Emit(OpCodes.Stfld, loaderField);
        il.MarkLabel(notNullLabel);

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

public static class EffectNodeCalculator
{
    private static readonly Dictionary<string, PortDefinition[]> PortDefsRegistry = new();
    private static readonly Dictionary<string, string[]> DynamicPropNamesRegistry = new();

    public static void RegisterPortDefs(
        string effectName,
        PortDefinition[] defs,
        string[] dynamicPropNames)
    {
        PortDefsRegistry[effectName] = defs;
        DynamicPropNamesRegistry[effectName] = dynamicPropNames;
    }

    public static PortDefinition[] GetPortDefs(string effectName)
    {
        return PortDefsRegistry.TryGetValue(effectName, out var d) ? d : [];
    }

    public static void RefreshDynamicContainer(
        NodeLogic self,
        VideoEffectsLoader loader,
        string changedPortName,
        object? changedValue,
        string dynamicPropName,
        string containerFieldName)
    {
        if (changedPortName == "InputImage") return;

        var containerFieldInfo = self.GetType().GetField(containerFieldName,
            BindingFlags.NonPublic | BindingFlags.Instance);
        if (containerFieldInfo == null) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await loader.SetValue(changedPortName, changedValue)
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                // ignore
            }

            var subObject = GetEffectSubObject(loader, dynamicPropName);
            if (subObject == null) return;

            var containerType = ContainerFactory.CreateOrGenerate(subObject);
            if (containerType == null) return;

            var currentContainer = (InputsContainer?)containerFieldInfo.GetValue(self);
            var rawName = subObject.GetType().FullName ?? subObject.GetType().Name;
            var expectedName = $"DynamicContainer_{rawName.Replace('.', '_')}";
            if (currentContainer?.GetType().Name == expectedName) return;

            var newContainer = (InputsContainer?)Activator.CreateInstance(containerType);
            if (newContainer == null) return;

            containerFieldInfo.SetValue(self, newContainer);

            // **
            // NOTE
            // **
            //
            // NeedToReinitializeInputPorts の発火は UI スレッドから行う。
            // Task.Run スレッドから dispatcher.Invoke（ブロッキング）を呼ぶと
            // NodeViewModel ハンドラ内の処理でフリーズするため BeginInvoke を使う。
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            _ = dispatcher.BeginInvoke(() => self.SetDynamicContainer(newContainer, dynamicPropName));
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
                await CalculateAsync(self, portDefs, dynamicPropNames, loader, ctx, inputImage)
                    .ConfigureAwait(false);
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
            lock (loader)
            {
                foreach (var def in portDefs)
                {
                    var value = GetPortValue(self, def, ctx);
#if DEBUG
                    if (def.PortType == PortType.Brush)
                        Console.WriteLine(
                            $@"  CalculateAsync Brush port '{def.PropName}': value={value?.GetType().Name ?? "null"}");
#endif
                    loader.SetValue(def.PropName, value)
                        .GetAwaiter()
                        .GetResult();
                }
            }

            foreach (var propName in dynamicPropNames)
            {
                var subObject = GetEffectSubObject(loader, propName);
                if (subObject == null) continue;

                var prefix = propName + ".";

                var effectSubPropNames = subObject.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<DisplayAttribute>() != null)
                    .Select(p => prefix + p.Name)
                    .ToHashSet();

                var registeredKeys = self.Inputs.Keys
                    .Where(k => k.StartsWith(prefix))
                    .ToHashSet();

                if (!effectSubPropNames.SetEquals(registeredKeys))
                {
                    var containerType = ContainerFactory.CreateOrGenerate(subObject);
                    if (containerType != null)
                    {
                        var newContainer = (InputsContainer?)Activator.CreateInstance(containerType);
                        if (newContainer != null)
                            typeof(NodeLogic)
                                .GetMethod("SwapDynamicContainer",
                                    BindingFlags.NonPublic | BindingFlags.Instance)
                                ?.Invoke(self, [propName, newContainer]);
                    }
                }

                lock (loader)
                {
                    foreach (var kv in self.Inputs)
                    {
                        if (!kv.Key.StartsWith(prefix)) continue;
                        var subPropName = kv.Key.Substring(prefix.Length);
                        var subProp = subObject.GetType().GetProperty(subPropName,
                            BindingFlags.Public | BindingFlags.Instance);
                        if (subProp == null) continue;
                        var raw = kv.Value.GetValue(ctx).GetAwaiter().GetResult();
                        SetSubPropertyDirect(subObject, subProp, raw);
                    }
                }
            }

            if (loader.Update(out var output, ctx, inputImage.Image))
                self.GetType().GetProperty("Output")?.SetValue(self, new ImageWrapper { Image = output });
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
#if DEBUG
            Console.WriteLine(
                $@"  CalculateAsync EXCEPTION: {exception.GetType().Name}: {exception.Message}" +
                (exception.InnerException != null
                    ? $@" --> Inner: {exception.InnerException.GetType().Name}: {exception.InnerException.Message}"
                    : ""));
#endif
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

    private static (object target, PropertyInfo property)? FindPropertyByDisplay(object? obj, string name,
        HashSet<object>? visited = null)
    {
        if (obj == null) return null;
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(obj)) return null;
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
            var result = FindPropertyByDisplay(sub, name, visited);
            if (result != null) return result;
        }

        return null;
    }

    private static object? GetPortValue(NodeLogic self, PortDefinition def, EvaluationContext? ctx)
    {
        if (!self.Inputs.TryGetValue(def.PropName, out var port)) return null;
        var raw = port.GetValue(ctx).GetAwaiter().GetResult();
        return def.PortType switch
        {
            PortType.Enum when def.EnumType != null =>
                raw is int i ? Enum.ToObject(def.EnumType, i) : raw,
            PortType.Float =>
                raw is float f ? f : raw != null ? Convert.ToSingle(raw) : (object)0f,
            PortType.Color =>
                raw ?? Colors.White,
            PortType.Brush =>
                ConvertBrush(raw),
            _ => raw
        };
    }

    private static IBrushParameter? ConvertBrush(object? raw)
    {
#if DEBUG
        Console.WriteLine(
            $@"  ConvertBrush: raw={raw?.GetType().Name ?? "null"}, " +
            $@"isBrushWrapper={raw is BrushWrapper}, " +
            $@"innerBrushIsNull={(raw as BrushWrapper)?.Brush == null}");
#endif
        if (raw is not BrushWrapper wrapper)
            return null;

        return wrapper.Brush == null ? null : NodeBrushFactory.Create(wrapper);
    }
}

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
        pb.SetCustomAttribute(new CustomAttributeBuilder(BoolCtor, [], [defP], [defaultValue]));
    }

    public static void ColorControl(PropertyBuilder pb, Color defaultValue)
    {
        var defP = typeof(ColorPortControlAttribute).GetProperty(nameof(ColorPortControlAttribute.DefaultColor))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(ColorCtor, [], [defP],
            [ColorStringConverter.ToString(defaultValue)]));
    }
}