using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;

public class ContainerFactory
{
    private static readonly Dictionary<string, Type> TypeCache = new();

    // ModuleBuilder を内部保持して、EffectNodeCalculator から ModuleBuilder なしで呼べるようにする
    private static ModuleBuilder? _moduleBuilder;

    /// <summary>
    ///     EffectNodeTypeBuilder.Build から呼ばれ、ModuleBuilder を登録する。
    /// </summary>
    public static void SetModuleBuilder(ModuleBuilder mod)
    {
        _moduleBuilder = mod;
    }

    /// <summary>
    ///     ModuleBuilder なしで呼べるオーバーロード。SetModuleBuilder が先に呼ばれている必要がある。
    /// </summary>
    public static Type? CreateOrGenerate(object effectPropertyInst)
    {
        if (_moduleBuilder == null)
            throw new InvalidOperationException("ModuleBuilder が未設定。SetModuleBuilder を先に呼ぶこと。");
        return CreateOrGenerate(effectPropertyInst, _moduleBuilder);
    }

    public static Type? CreateOrGenerate(object effectPropertyInst, ModuleBuilder mod)
    {
        // ModuleBuilder を記録しておく（RefreshDynamicContainer から呼ばれる際に使う）
        _moduleBuilder ??= mod;

        var type = effectPropertyInst.GetType();
        var name = type.FullName ?? type.Name;
        if (TypeCache.TryGetValue(name, out var cached))
            return cached;

        var properties = new List<PropertyInfo>();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var attribute = property.GetCustomAttribute<DisplayAttribute>();
            if (attribute is null)
                continue;

            properties.Add(property);
        }

        if (properties.Count == 0)
            return null;

        var ports = properties.Select(property =>
                TryMakePortDefinition(
                    property,
                    property.GetCustomAttribute<DisplayAttribute>()!,
                    property.GetValue(effectPropertyInst)))
            .Where(x => x is not null)
            .Cast<PortDefinition>()
            .ToList();

        return CreateOrGenerate(name, ports, mod);
    }

    private static PortDefinition? TryMakePortDefinition(PropertyInfo prop, DisplayAttribute display, object? inst)
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

        if (prop.PropertyType == typeof(BrushWrapper))
            return new PortDefinition
            {
                PropName = prop.Name,
                PortType = PortType.Brush,
                LabelKey = display.Name ?? TextNode.BrushName ?? prop.Name,
                DescKey = descKey,
                ResourceType = resourceType,
                DefaultValue = null
            };

        return null;

        static int ParseDigits(string format)
        {
            if (format.Length >= 2 && (format[0] == 'F' || format[0] == 'f')
                                   && int.TryParse(format[1..], out var d))
                return d;
            return 2;
        }
    }

    private static Type CreateOrGenerate(string name, List<PortDefinition> ports, ModuleBuilder mod)
    {
        var tb = mod.DefineType(
            $"DynamicContainer_{name}",
            TypeAttributes.Public,
            typeof(InputsContainer));

        foreach (var port in ports)
        {
            var clrType = port.PortType switch
            {
                PortType.Enum => port.EnumType ?? typeof(int),
                PortType.Bool => typeof(bool),
                PortType.Color => typeof(Color),
                PortType.Brush => typeof(BrushWrapper),
                _ => typeof(float)
            };

            var field = tb.DefineField($"_{port.PropName}", clrType, FieldAttributes.Private);
            var pb = EmitContainerPort(tb, port.PropName, clrType, field);

            Attr.InputPort(pb, port.LabelKey, port.DescKey, port.ResourceType);
            switch (port.PortType)
            {
                case PortType.Float:
                    Attr.NumberControl(pb, port.Min, port.Max, port.Digits, port.Unit,
                        port.DefaultValue is null
                            ? 0f
                            : Convert.ToSingle(port.DefaultValue));
                    Attr.PortColor(pb, nameof(Colors.DarkOrange));
                    break;
                case PortType.Enum:
                    Attr.EnumControl(pb, port.EnumType!, Convert.ToInt32((Enum?)port.DefaultValue));
                    Attr.PortColor(pb, nameof(Colors.DarkOrange));
                    break;
                case PortType.Bool:
                    Attr.BoolControl(pb, (bool)(port.DefaultValue ?? false));
                    break;
                case PortType.Color:
                    Attr.ColorControl(pb, (Color)(port.DefaultValue ?? Colors.White));
                    Attr.PortColor(pb, nameof(Colors.MediumPurple));
                    break;
                case PortType.Brush:
                    Attr.PortColor(pb, nameof(Colors.LawnGreen));
                    break;
            }
        }

        EmitConstructor(tb);

        var type = tb.CreateType()
                   ?? throw new InvalidOperationException();
#if DEBUG
        foreach (var p in type.GetProperties())
        {
            var hasAttr = Attribute.IsDefined(p, typeof(InputPortAttribute));
            Console.WriteLine($@"[ContainerFactory] {type.Name}.{p.Name} HasInputPort={hasAttr}");
        }
#endif
        TypeCache[name] = type;

        return type;
    }

    private static PropertyBuilder EmitContainerPort(TypeBuilder tb, string name, Type t, FieldBuilder field)
    {
        var setMethod = typeof(InputsContainer)
            .GetMethod(nameof(InputsContainer.Set), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(t);

        var getter = tb.DefineMethod($"get_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            t, Type.EmptyTypes);
        var gil = getter.GetILGenerator();
        gil.Emit(OpCodes.Ldarg_0);
        gil.Emit(OpCodes.Ldfld, field);
        gil.Emit(OpCodes.Ret);

        var setter = tb.DefineMethod($"set_{name}",
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
            null, [t]);
        var sil = setter.GetILGenerator();
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldarg_0);
        sil.Emit(OpCodes.Ldflda, field);
        sil.Emit(OpCodes.Ldarg_1);
        sil.Emit(OpCodes.Ldstr, name);
        sil.Emit(OpCodes.Call, setMethod);
        sil.Emit(OpCodes.Ret);

        var pb = tb.DefineProperty(name, PropertyAttributes.None, t, null);
        pb.SetGetMethod(getter);
        pb.SetSetMethod(setter);
        return pb;
    }

    private static void EmitConstructor(TypeBuilder tb)
    {
        var baseCtor = typeof(InputsContainer).GetConstructor(
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
}