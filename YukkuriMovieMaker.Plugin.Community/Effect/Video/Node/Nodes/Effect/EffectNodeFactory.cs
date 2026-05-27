using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.Plugin.Effects;
using YukkuriMovieMaker.Resources.Localization;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect;

public static class EffectNodeFactory
{
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

        if (TypeCache.TryGetValue(effectType.Name, out var cached))
        {
#if DEBUG
            Console.WriteLine($@"[EffectNodeFactory]   cache hit: {effectType.Name}");
#endif
            return cached;
        }

        var effectInstance = Activator.CreateInstance(effectType) as IVideoEffect
                             ?? throw new InvalidOperationException($"Cannot instantiate {effectType.Name}");

        var portDefs = EffectPortCollector.Collect(effectInstance);

#if DEBUG
        Console.WriteLine($@"[EffectNodeFactory]   {effectType.Name}: {portDefs.Count} port(s) collected");
        foreach (var d in portDefs)
            Console.WriteLine($@"[EffectNodeFactory]     {d.PortType,-6} {d.PropName} (label={d.LabelKey})");
#endif

        var veAttr = effectType.GetCustomAttribute<VideoEffectAttribute>();
        var categoryKey = veAttr?.Categories.FirstOrDefault() ?? "Effect";
        var labelKey = veAttr?.Name ?? effectType.Name;

        var generated = EffectNodeTypeBuilder.Build(ModBuilder, effectType.Name, categoryKey, labelKey, portDefs);
        TypeCache[effectType.Name] = generated;
        return generated;
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

    // Float / Animation 型プロパティ用
    public float Min { get; init; } = float.NaN;
    public float Max { get; init; } = float.NaN;
    public int Digits { get; init; } = 2;
    public string Unit { get; init; } = "";

    // Enum 型プロパティ用
    public Type? EnumType { get; init; }

    // Color 型プロパティ用
    public Color DefaultColor { get; init; } = Colors.White;
}

public enum PortType
{
    Float,
    Enum,
    Bool,
    Color,
    Brush
}

public static class EffectPortCollector
{
    public static List<PortDefinition> Collect(object root)
    {
        var result = new List<PortDefinition>();
        CollectRecursive(root, result);
        return result;
    }

    private static bool IsUnsupportedSubObject(Type t)
    {
        if (typeof(IBrushPlugin).IsAssignableFrom(t)) return true;
        if (t.FullName?.StartsWith("YukkuriMovieMaker.Brush.Brush") == true) return true;
        return false;
    }

    private static void CollectRecursive(object obj, List<PortDefinition> result,
        HashSet<object>? visited = null)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(obj)) return;

        foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0) continue;
            if (prop.GetCustomAttribute<ObsoleteAttribute>() != null) continue;

            var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
            if (displayAttr != null)
            {
                var def = TryMakePortDefinition(prop, displayAttr);
                if (def != null)
                {
                    result.Add(def);
                    continue;
                }

                if (!prop.CanRead) continue;
                if (!prop.PropertyType.IsClass || prop.PropertyType == typeof(string)) continue;
                if (IsUnsupportedSubObject(prop.PropertyType)) continue;

                object? subDisplay;
                try
                {
                    subDisplay = prop.GetValue(obj);
                }
                catch
                {
                    continue;
                }

                if (subDisplay == null) continue;

                CollectRecursive(subDisplay, result, visited);
                continue;
            }

            if (!prop.CanRead) continue;
            if (!prop.PropertyType.IsClass || prop.PropertyType == typeof(string)) continue;
            if (IsUnsupportedSubObject(prop.PropertyType)) continue;

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

            CollectRecursive(sub, result, visited);
        }
    }

    private static PortDefinition? TryMakePortDefinition(PropertyInfo prop, DisplayAttribute display)
    {
        var labelKey = display.Name ?? prop.Name;
        var descKey = display.Description ?? "";
        var resourceType = display.ResourceType;

        if (prop.PropertyType == typeof(Animation))
        {
            var slider = prop.GetCustomAttribute<AnimationSliderAttribute>();
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Float,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
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
                EnumType = prop.PropertyType
            };

        if (prop.PropertyType == typeof(bool))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Bool,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType
            };

        if (prop.PropertyType == typeof(float) || prop.PropertyType == typeof(double) ||
            prop.PropertyType == typeof(int))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Float,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType
            };

        if (prop.PropertyType == typeof(Color))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Color,
                LabelKey = labelKey,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultColor = Colors.White
            };

        return null;
    }

    private static int ParseDigits(string format)
    {
        if (format.Length >= 2 && (format[0] == 'F' || format[0] == 'f')
                               && int.TryParse(format[1..], out var d))
            return d;
        return 2;
    }
}

/// <summary>
///     System.Reflection.Emit を使って NodeLogic サブクラスを動的生成する。
///     <code>
///     [Node(...)]
///     public class DynamicEffectNode_{effectName} : NodeLogic
///     {
///         private VideoEffectsLoader? _videoEffect;
///         private static readonly string _effectNameCache;
///         private static readonly PortDefinition[] _portDefs;
/// 
///         static DynamicEffectNode_xxx() { /* _effectNameCache / _portDefs を初期化 */ }
///         public DynamicEffectNode_xxx() : base() { }
/// 
///         [InputPort][PortColor] public ImageWrapper? InputImage { get/set }
///         [InputPort][NumberPortControl/EnumPortControl/...] public T PropName { get/set }
///         [OutputPort][PortColor] public ImageWrapper? Output { get/set }
/// 
///         protected override Task Calculate()
///             => EffectNodeCalculator.Calculate(this, _effectNameCache, _portDefs, ref _videoEffect);
///     }
///   </code>
/// </summary>
public static class EffectNodeTypeBuilder
{
    public static Type Build(
        ModuleBuilder mod,
        string effectName,
        string categoryKey,
        string labelKey,
        List<PortDefinition> portDefs)
    {
        var typeName = $"DynamicEffectNode_{effectName}";
        var tb = mod.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            typeof(NodeLogic));

        tb.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(NodeAttribute).GetConstructors()[0],
            [typeof(EffectNodeCategory), labelKey, labelKey, typeof(Texts)]));

        var loaderField = tb.DefineField("_videoEffect", typeof(VideoEffectsLoader), FieldAttributes.Private);

        EffectNodeCalculator.RegisterPortDefs(effectName, portDefs.ToArray());

        var effectNameField = tb.DefineField("_effectNameCache", typeof(string),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        var portDefsField = tb.DefineField("_portDefs", typeof(PortDefinition[]),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);

        var cctor = tb.DefineTypeInitializer();
        var cil = cctor.GetILGenerator();
        cil.Emit(OpCodes.Ldstr, effectName);
        cil.Emit(OpCodes.Stsfld, effectNameField);
        cil.Emit(OpCodes.Ldstr, effectName);
        cil.Emit(OpCodes.Call, typeof(EffectNodeCalculator).GetMethod(nameof(EffectNodeCalculator.GetPortDefs))!);
        cil.Emit(OpCodes.Stsfld, portDefsField);
        cil.Emit(OpCodes.Ret);

        EmitImageInputPort(tb);
        foreach (var def in portDefs) EmitParameterPort(tb, def);
        EmitImageOutputPort(tb);
        EmitConstructor(tb);
        EmitCalculate(tb, loaderField, effectNameField, portDefsField);

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
                Attr.NumberControl(pb, def.Min, def.Max, def.Digits, def.Unit);
                Attr.PortColor(pb, nameof(Colors.DarkOrange));
                break;
            case PortType.Enum:
                Attr.EnumControl(pb, def.EnumType!);
                Attr.PortColor(pb, nameof(Colors.DarkOrange));
                break;
            case PortType.Bool:
                Attr.BoolControl(pb);
                break;
            case PortType.Color:
                Attr.ColorControl(pb);
                Attr.PortColor(pb, nameof(Colors.MediumPurple));
                break;
            case PortType.Brush:
                Attr.PortColor(pb, nameof(Colors.LawnGreen));
                break;
        }
    }

    private static void EmitImageOutputPort(TypeBuilder tb)
    {
        var pb = EmitOutputProperty(tb, "Output", typeof(ImageWrapper));
        Attr.OutputPort(pb, nameof(TextUi.Output), "", typeof(TextUi));
        Attr.PortColor(pb, nameof(Colors.CornflowerBlue));
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

    private static void EmitConstructor(TypeBuilder tb)
    {
        var baseCtor = typeof(NodeLogic).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null)!;

        var ctor = tb.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig |
            MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.Standard, Type.EmptyTypes);
        var il = ctor.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseCtor);
        il.Emit(OpCodes.Ret);
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

    public static void RegisterPortDefs(string effectName, PortDefinition[] defs)
    {
        PortDefsRegistry[effectName] = defs;
    }

    public static PortDefinition[] GetPortDefs(string effectName)
    {
        return PortDefsRegistry.TryGetValue(effectName, out var d) ? d : [];
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
        return Task.Run(async () =>
        {
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                await CalculateAsync(self, portDefs, loader, ctx, inputImage).ConfigureAwait(false);
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
        VideoEffectsLoader loader,
        EvaluationContext ctx,
        ImageWrapper inputImage)
    {
        try
        {
            foreach (var def in portDefs)
            {
                var value = GetPortValue(self, def);
                SetPropertyDirect(loader, def.PropName, value);
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

public sealed class EffectNodeCategory : INodeCategory
{
    public string Category => "Effect/VideoEffect";
    public string Color => nameof(Colors.SteelBlue);
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

    public static void InputPort(PropertyBuilder pb, string label, string desc, Type? resourceType)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(InputPortCtor, [label, desc, resourceType!, false]));
    }

    public static void OutputPort(PropertyBuilder pb, string label, string desc, Type? resourceType)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(OutputPortCtor, [label, desc, resourceType!]));
    }

    public static void PortColor(PropertyBuilder pb, string colorName)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(PortColorCtor, [colorName]));
    }

    public static void NumberControl(PropertyBuilder pb, float min, float max, int digits, string unit)
    {
        var minP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Min))!;
        var maxP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Max))!;
        var digP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Digits))!;
        var unitP = typeof(NumberPortControlAttribute).GetProperty(nameof(NumberPortControlAttribute.Unit))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(NumberCtor, [], [minP, maxP, digP, unitP],
            [min, max, digits, unit]));
    }

    public static void EnumControl(PropertyBuilder pb, Type enumType)
    {
        var itemsP = typeof(EnumPortControlAttribute).GetProperty(nameof(EnumPortControlAttribute.Items))!;
        var editP = typeof(EnumPortControlAttribute).GetProperty(nameof(EnumPortControlAttribute.IsEditable))!;
        var defP = typeof(EnumPortControlAttribute).GetProperty(nameof(EnumPortControlAttribute.Default))!;
        pb.SetCustomAttribute(new CustomAttributeBuilder(EnumCtor, [], [itemsP, editP, defP], [enumType, false, 0]));
    }

    public static void BoolControl(PropertyBuilder pb)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(BoolCtor, []));
    }

    public static void ColorControl(PropertyBuilder pb)
    {
        pb.SetCustomAttribute(new CustomAttributeBuilder(ColorCtor, []));
    }
}