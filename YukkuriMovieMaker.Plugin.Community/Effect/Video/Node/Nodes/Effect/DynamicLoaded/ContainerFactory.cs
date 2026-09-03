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
    private static readonly Lock Lock = new();
    private static readonly Dictionary<string, Type> TypeCache = new();
    private static readonly Dictionary<string, PortDefinition[]> PortDefsRegistry = new();

    // ModuleBuilder を内部保持して、EffectNodeCalculator から ModuleBuilder なしで呼べるようにする
    private static ModuleBuilder? _moduleBuilder;

    public static PortDefinition[] GetPortDefs(string name)
    {
        return PortDefsRegistry.TryGetValue(name, out var d) ? d : [];
    }

    /// <summary>
    ///     Unknown型でカスタムエディタが見つかったポートに、元のインスタンスが持っていた実際の値を
    ///     初期値として書き込む。EffectNodeCalculator.SeedDefaultValues の InputsContainer 版。
    ///     InputsContainer は NodeLogic.Inputs を持たないため、バッキングフィールドへ直接書き込む。
    /// </summary>
    public static void SeedUnknownDefaults(InputsContainer self, PortDefinition[] portDefs)
    {
        var type = self.GetType();
        foreach (var def in portDefs)
        {
            if (def.PortType != PortType.Unknown) continue;
            if (def.DefaultValue == null) continue;
            var field = type.GetField($"_{def.PropName}", BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(self, def.DefaultValue);
        }
    }

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
        var name = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;

        lock (Lock)
        {
            if (TypeCache.TryGetValue(name, out var cached))
                return cached;
        }

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
            .ToList();

        lock (Lock)
        {
            return TypeCache.TryGetValue(name, out var cached) ? cached : CreateOrGenerate(name, type, ports, mod);
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

        if (prop.PropertyType == typeof(string))
        {
            var hasCustomEditor = prop.GetCustomAttributesData()
                .Any(ad => typeof(PropertyEditorAttribute2).IsAssignableFrom(ad.AttributeType));
            if (!hasCustomEditor)
                return new PortDefinition
                {
                    PropName = prop.Name,
                    PortType = PortType.Text,
                    LabelKey = labelKey,
                    DescKey = descKey,
                    ResourceType = resourceType,
                    DefaultValue = inst as string ?? ""
                };
        }

        var editorAttrData = prop.GetCustomAttributesData()
            .FirstOrDefault(ad => typeof(PropertyEditorAttribute2).IsAssignableFrom(ad.AttributeType));
        var editorAttrInstance = editorAttrData == null ? null : Attr.CreateEditorAttributeInstance(editorAttrData);

        return new PortDefinition
        {
            PropName = prop.Name,
            PortType = PortType.Unknown,
            LabelKey = labelKey,
            DescKey = descKey,
            ResourceType = resourceType,
            // 実際のインスタンスが持っていた値をそのまま初期値として引き継ぐ。
            // null のままだと、非null前提で実装されているエディタ側で例外になる。
            DefaultValue = inst,
            UnknownClrType = prop.PropertyType,
            EditorAttributeData = editorAttrData,
            EditorAttributeInstance = editorAttrInstance
        };

        static int ParseDigits(string format)
        {
            if (format.Length >= 2 && (format[0] == 'F' || format[0] == 'f')
                                   && int.TryParse(format[1..], out var d))
                return d;
            return 2;
        }
    }

    private static Type CreateOrGenerate(string name, Type originalType, List<PortDefinition> ports, ModuleBuilder mod)
    {
        var flatName = string.Concat(name.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        PortDefsRegistry[flatName] = ports.ToArray();

        var tb = mod.DefineType(
            $"DynamicContainer_{flatName}",
            TypeAttributes.Public,
            typeof(InputsContainer));

        var portDefsField = tb.DefineField("_portDefs", typeof(PortDefinition[]),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        // CustomEditorPort が「本物のインスタンス」を用意してカスタムエディタに渡すために使う。
        var originalTypeField = tb.DefineField("_originalOwnerType", typeof(Type),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);

        var cctor = tb.DefineTypeInitializer();
        var cctorIl = cctor.GetILGenerator();
        cctorIl.Emit(OpCodes.Ldstr, flatName);
        cctorIl.Emit(OpCodes.Call, typeof(ContainerFactory).GetMethod(nameof(GetPortDefs))!);
        cctorIl.Emit(OpCodes.Stsfld, portDefsField);
        cctorIl.Emit(OpCodes.Ldtoken, originalType);
        cctorIl.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        cctorIl.Emit(OpCodes.Stsfld, originalTypeField);
        cctorIl.Emit(OpCodes.Ret);

        foreach (var port in ports)
        {
            var clrType = port.PortType switch
            {
                PortType.Enum => port.EnumType ?? typeof(int),
                PortType.Bool => typeof(bool),
                PortType.Color => typeof(Color),
                PortType.Brush => typeof(BrushWrapper),
                PortType.Text => typeof(string),
                // Unknown の場合、floatに丸めてしまうと元の値が壊れる（型が合わないため）。
                // 必ず元プロパティの実際のCLR型を使う。
                PortType.Unknown => port.UnknownClrType ?? typeof(float),
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
                    Attr.PortColor(pb, nameof(Colors.Gold));
                    break;
                case PortType.Brush:
                    Attr.PortColor(pb, nameof(Colors.LawnGreen));
                    break;
                case PortType.Text:
                    Attr.TextControl(pb, (string?)port.DefaultValue ?? "");
                    Attr.PortColor(pb, nameof(Colors.MediumSeaGreen));
                    break;
                case PortType.Unknown:
                    // こちらで用意した既知のコントロールが使えない型でも、元プロパティ側に
                    // PropertyEditorAttribute2 継承属性が付いていれば、それを引き継いで
                    // CustomEditorPort による編集を可能にする。見つからない場合は
                    // 今までどおり接続専用（編集UIなし）のポートになる。
                    // ここで失敗しても、コンテナ（グループ）全体の生成を巻き添えで失敗させない。
                    if (port.EditorAttributeData != null)
                        try
                        {
                            Attr.CustomEditorControl(pb, port.EditorAttributeData);
                            Attr.PortColor(pb, nameof(Colors.Gray));
                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            Console.WriteLine(
                                $@"[ContainerFactory] Failed to attach custom editor to '{port.PropName}': " +
                                $@"{ex.GetType().Name}: {ex.Message}");
#endif
                        }

                    break;
            }
        }

        EmitConstructor(tb, portDefsField);

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

    private static void EmitConstructor(TypeBuilder tb, FieldBuilder portDefsField)
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

        // Unknown型でカスタムエディタが見つかったポートには、元のインスタンスが持っていた
        // 実際の値を初期値として書き込む（EffectNodeCalculator.SeedDefaultValues 相当）。
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsfld, portDefsField);
        il.Emit(OpCodes.Call, typeof(ContainerFactory).GetMethod(nameof(SeedUnknownDefaults))!);

        il.Emit(OpCodes.Ret);
    }
}