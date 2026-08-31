using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using PortDefinition = YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded.PortDefinition;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Generator.Brush;

/// <summary>
///     全 IBrushPlugin を動的にノード化するファクトリ。
///     EffectNodeFactory と同じ Reflection.Emit ベースの仕組みを IBrushPlugin に対して適用する。
///     既存の EffectPortCollector / ContainerFactory / Attr をそのまま再利用し、
///     入力ポートの収集・動的サブオブジェクトの扱いはエフェクトノードと完全に共通化する。
/// </summary>
public static class DynamicBrushNodeFactory
{
    private static readonly Lock Lock = new();
    private static readonly Dictionary<string, Type> TypeCache = new();

    private static readonly AssemblyBuilder AsmBuilder =
        AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("DynamicBrushNodes"),
            AssemblyBuilderAccess.Run);

    private static readonly ModuleBuilder ModBuilder =
        AsmBuilder.DefineDynamicModule("MainModule");

    public static Type[] Create()
    {
        var result = new List<Type>();
        foreach (var plugin in PluginLoader.BrushPlugins)
            try
            {
                var nodeType = GetOrCreate(plugin.GetType());
                Registry.RegisterType(nodeType);
                result.Add(nodeType);
#if DEBUG
                Console.WriteLine($@"[DynamicBrushNodeFactory] OK: {plugin.GetType().Name} -> {nodeType.FullName}");
#endif
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.WriteLine(
                    $@"[DynamicBrushNodeFactory] FAIL: {plugin.GetType().Name} -- {ex.GetType().Name}: {ex.Message}");
#endif
            }
#if DEBUG
        Console.WriteLine(
            $@"[DynamicBrushNodeFactory] Generated {result.Count} / {PluginLoader.BrushPlugins.Count()} brushes.");
#endif
        return result.ToArray();
    }

    internal static string? GetBrushName(string assemblyQualifiedName)
    {
        lock (Lock)
        {
            foreach (var (pluginName, type) in TypeCache)
                if ((type.AssemblyQualifiedName ?? type.Name) == assemblyQualifiedName)
                    return pluginName;
            return null;
        }
    }

    internal static Type? GetOrCreate(string pluginName)
    {
        lock (Lock)
        {
            if (TypeCache.TryGetValue(pluginName, out var cached))
                return cached;
        }

        var pluginType = PluginLoader.BrushPlugins
            .Select(p => p.GetType())
            .FirstOrDefault(t => (t.AssemblyQualifiedName ?? t.FullName ?? t.Name) == pluginName);

        if (pluginType == null) return null;

        try
        {
            return GetOrCreate(pluginType);
        }
        catch
        {
            return null;
        }
    }

    private static Type GetOrCreate(Type pluginType)
    {
        lock (Lock)
        {
            var pluginName = pluginType.AssemblyQualifiedName
                             ?? pluginType.FullName
                             ?? pluginType.Name;

            if (TypeCache.TryGetValue(pluginName, out var cached))
                return cached;

            var pluginInstance = Activator.CreateInstance(pluginType) as IBrushPlugin
                                 ?? throw new InvalidOperationException($"Cannot instantiate {pluginType.Name}");

            var parameterInstance = pluginInstance.CreateBrushParameter();

            var (staticPortDefs, dynamicParams) = EffectPortCollector.Collect(parameterInstance);

            var labelKey = pluginInstance.Name;

            var generated =
                BrushNodeTypeBuilder.Build(ModBuilder, pluginName, parameterInstance.GetType(), labelKey,
                    staticPortDefs, dynamicParams);
            TypeCache[pluginName] = generated;
            return generated;
        }
    }
}

public static class BrushNodeTypeBuilder
{
    public static Type Build(
        ModuleBuilder mod,
        string pluginName,
        Type originalParameterType,
        string labelKey,
        List<PortDefinition> staticPortDefs,
        List<(PortDefinition, PropertyInfo, object)> dynamicPropertyDefs)
    {
        var typeName = $"DynamicBrushNode_{Convert.ToHexString(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(pluginName)))[..32]}";
        var tb = mod.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.BeforeFieldInit,
            typeof(NodeLogic));

        tb.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(NodeAttribute).GetConstructors()[0],
            [
                typeof(BrushCategory),
                labelKey, labelKey, null
            ]));

        var loaderField = tb.DefineField("_loader", typeof(VideoEffectsLoader), FieldAttributes.Private);
        var lastDevicesField = tb.DefineField("_lastDevices", typeof(object), FieldAttributes.Private);

        BrushNodeCalculator.RegisterPortDefs(pluginName, staticPortDefs.ToArray(),
            dynamicPropertyDefs.Select(d => d.Item2.Name).ToArray());

        var pluginNameField = tb.DefineField("_pluginNameCache", typeof(string),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        var portDefsField = tb.DefineField("_portDefs", typeof(PortDefinition[]),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        // CustomEditorPort が「本物のパラメータインスタンス」を用意してカスタムエディタに渡すために使う。
        var originalTypeField = tb.DefineField("_originalOwnerType", typeof(Type),
            FieldAttributes.Private | FieldAttributes.Static | FieldAttributes.InitOnly);
        var containerBackFields = dynamicPropertyDefs.Select(props =>
            tb.DefineField($"_{props.Item2.Name}", typeof(InputsContainer), FieldAttributes.Private)).ToList();

        var cctor = tb.DefineTypeInitializer();
        var cil = cctor.GetILGenerator();
        cil.Emit(OpCodes.Ldstr, pluginName);
        cil.Emit(OpCodes.Stsfld, pluginNameField);
        cil.Emit(OpCodes.Ldstr, pluginName);
        cil.Emit(OpCodes.Call, typeof(BrushNodeCalculator).GetMethod(nameof(BrushNodeCalculator.GetPortDefs))!);
        cil.Emit(OpCodes.Stsfld, portDefsField);
        cil.Emit(OpCodes.Ldtoken, originalParameterType);
        cil.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);
        cil.Emit(OpCodes.Stsfld, originalTypeField);
        cil.Emit(OpCodes.Ret);

        foreach (var def in staticPortDefs) EmitParameterPort(tb, def);
        for (var index = 0; index < dynamicPropertyDefs.Count; index++)
            EmitContainerPort(tb, dynamicPropertyDefs[index], containerBackFields[index]);

        EmitBrushOutputPort(tb);
        EmitCalculate(tb, loaderField, lastDevicesField, pluginNameField, portDefsField);
        EmitDispose(tb, loaderField);

        if (dynamicPropertyDefs.Count > 0)
            EmitOnInputValueChanged(tb, loaderField, dynamicPropertyDefs, containerBackFields);

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

        // Unknown型でカスタムエディタが見つかったポートには、元のブラシパラメータインスタンスが
        // 持っていた実際の値を初期値として書き込む（EffectNodeFactory側と同様）。
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsfld, portDefsField);
        il.Emit(OpCodes.Call,
            typeof(EffectNodeCalculator).GetMethod(nameof(EffectNodeCalculator.SeedDefaultValues))!);

        il.Emit(OpCodes.Ret);

        return tb.CreateType()
               ?? throw new InvalidOperationException($"Failed to create type for {pluginName}");
    }

    private static void EmitParameterPort(TypeBuilder tb, PortDefinition def)
    {
        var clrType = def.PortType switch
        {
            PortType.Enum => typeof(int),
            PortType.Bool => typeof(bool),
            PortType.Color => typeof(Color),
            // ブラシパラメータ内に Brush 型（ネストされた Brush プロパティ）が現れることは想定していない。
            // EffectPortCollector が PortType.Brush を返した場合も Float にフォールバックしておき、
            // 想定外の構造でも型生成自体は失敗しないようにする。
            PortType.Brush => typeof(float),
            // Unknown の場合、floatに丸めてしまうと元の値が壊れる（型が合わないため）。
            // 必ず元プロパティの実際のCLR型を使う。
            PortType.Unknown => def.UnknownClrType ?? typeof(float),
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
                // IBrushParameter の中に Brush 型がネストすることは通常想定されないが、
                // 万一発生した場合でも float ポートとして最低限機能するようにしておく。
                Attr.NumberControl(pb, def.Min, def.Max, def.Digits, def.Unit, 0f);
                Attr.PortColor(pb, nameof(Colors.DarkOrange));
                break;
            case PortType.Unknown:
                // こちらで用意した既知のコントロールが使えない型でも、元プロパティ側に
                // PropertyEditorAttribute2 継承属性が付いていれば、それを引き継いで
                // CustomEditorPort による編集を可能にする。見つからない場合は
                // 今までどおり接続専用（編集UIなし）のポートになる。
                // ここで失敗しても、ノード（ブラシ全体）の生成自体を巻き添えで失敗させない。
                if (def.EditorAttributeData != null)
                    try
                    {
                        Attr.CustomEditorControl(pb, def.EditorAttributeData);
                        Attr.PortColor(pb, nameof(Colors.Gray));
                    }
                    catch (Exception ex)
                    {
#if DEBUG
                        Console.WriteLine(
                            $@"[BrushNodeTypeBuilder] Failed to attach custom editor to '{def.PropName}': " +
                            $@"{ex.GetType().Name}: {ex.Message}");
#endif
                    }

                break;
        }
    }

    private static void EmitContainerPort(TypeBuilder tb, (PortDefinition port, PropertyInfo prop, object _) info,
        FieldInfo field)
    {
        var pb = EmitContainerProperty(tb, info.prop.Name, field);
        Attr.InputPort(pb, info.port.LabelKey, info.port.DescKey, info.port.ResourceType, true);
    }

    private static void EmitBrushOutputPort(TypeBuilder tb)
    {
        var pb = EmitOutputProperty(tb, "Output", typeof(BrushWrapper));
        Attr.OutputPort(pb, nameof(TextUi.Output), "", typeof(TextUi));
        Attr.PortColor(pb, nameof(Colors.LawnGreen));
    }

    private static void EmitDispose(TypeBuilder tb, FieldBuilder loaderField)
    {
        var iDisposableDispose = typeof(IDisposable).GetMethod(nameof(IDisposable.Dispose))!;
        var baseDispose = typeof(NodeLogic).GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance)!;

        var m = tb.DefineMethod(
            "Dispose",
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(void),
            Type.EmptyTypes);

        var il = m.GetILGenerator();
        var skipLabel = il.DefineLabel();
        var afterLabel = il.DefineLabel();

        // if (_loader != null) _loader.Dispose();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, loaderField);
        il.Emit(OpCodes.Dup);
        il.Emit(OpCodes.Brfalse_S, skipLabel);
        il.Emit(OpCodes.Callvirt, iDisposableDispose);
        il.Emit(OpCodes.Br_S, afterLabel);
        il.MarkLabel(skipLabel);
        il.Emit(OpCodes.Pop);
        il.MarkLabel(afterLabel);

        // _loader = null;
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldnull);
        il.Emit(OpCodes.Stfld, loaderField);

        // base.Dispose();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, baseDispose);
        il.Emit(OpCodes.Ret);

        tb.DefineMethodOverride(m,
            typeof(NodeLogic).GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance)!);
    }

    /// <summary>
    ///     OnInputValueChanged override を Emit する。
    ///     EffectNodeTypeBuilder.EmitOnInputValueChanged と同じ構造だが、対象が
    ///     VideoEffectsLoader（IVideoEffect 経由）ではなく BrushNodeCalculator が管理する
    ///     IBrushParameter 経由のローダーである点のみ異なる。
    /// </summary>
    private static void EmitOnInputValueChanged(
        TypeBuilder tb,
        FieldBuilder loaderField,
        List<(PortDefinition, PropertyInfo, object)> dynamicPropertyDefs,
        List<FieldBuilder> containerBackFields)
    {
        var refreshMethod = typeof(BrushNodeCalculator)
            .GetMethod(nameof(BrushNodeCalculator.RefreshDynamicContainer),
                BindingFlags.Public | BindingFlags.Static)!;

        var m = tb.DefineMethod("OnInputValueChanged",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig |
            MethodAttributes.FamORAssem,
            typeof(void), [typeof(string), typeof(object)]);

        var il = m.GetILGenerator();

        // _loader が null（まだ一度も Calculate が走っていない）場合は、
        // ローダーが存在しないのでコンテナの再評価はスキップする。
        // エフェクトノードと異なり、ブラシのローダーは EvaluationContext.Devices が
        // 必要なため LoadEffectSync のような同期初期化ができない。
        var skipLabel = il.DefineLabel();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, loaderField);
        il.Emit(OpCodes.Brfalse_S, skipLabel);

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

        il.MarkLabel(skipLabel);
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
        FieldBuilder lastDevicesField,
        FieldBuilder pluginNameField,
        FieldBuilder portDefsField)
    {
        var calcTarget = typeof(BrushNodeCalculator)
            .GetMethod(nameof(BrushNodeCalculator.Calculate), BindingFlags.Public | BindingFlags.Static)!;

        var m = tb.DefineMethod("Calculate",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            typeof(Task), Type.EmptyTypes);

        var il = m.GetILGenerator();
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldsfld, pluginNameField);
        il.Emit(OpCodes.Ldsfld, portDefsField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, loaderField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldflda, lastDevicesField);
        il.Emit(OpCodes.Call, calcTarget);
        il.Emit(OpCodes.Ret);

        tb.DefineMethodOverride(m,
            typeof(NodeLogic).GetMethod("Calculate", BindingFlags.NonPublic | BindingFlags.Instance)!);
    }
}

/// <summary>
///     動的ブラシノードの実行時ロジック。EffectNodeCalculator と対になるブラシ専用版。
///     IVideoEffect 経由ではなく IBrushPlugin / IBrushParameter 経由で値を反映し、
///     最終的に ID2D1Brush を BrushWrapper に詰めて出力する。
/// </summary>
public static class BrushNodeCalculator
{
    private static readonly ConcurrentDictionary<string, PortDefinition[]> PortDefsRegistry = new();
    private static readonly ConcurrentDictionary<string, string[]> DynamicPropNamesRegistry = new();

    public static void RegisterPortDefs(
        string pluginName,
        PortDefinition[] defs,
        string[] dynamicPropNames)
    {
        PortDefsRegistry[pluginName] = defs;
        DynamicPropNamesRegistry[pluginName] = dynamicPropNames;
    }

    public static PortDefinition[] GetPortDefs(string pluginName)
    {
        return PortDefsRegistry.TryGetValue(pluginName, out var d) ? d : [];
    }

    /// <summary>
    ///     EffectNodeCalculator.RefreshDynamicContainer と同じ構造だが、対象が _videoEffect ではなく
    ///     _brushParameter であるため、サブオブジェクトの探索元を GetParameterSubObject に差し替えた独立実装。
    /// </summary>
    public static void RefreshDynamicContainer(
        NodeLogic self,
        VideoEffectsLoader loader,
        string changedPortName,
        object? changedValue,
        string dynamicPropName,
        string containerFieldName)
    {
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
                // changedPortName は必ずしも dynamicPropName 配下のプロパティとは限らないため、
                // ここで失敗してもコンテナの再評価自体は継続する。
            }

            var subObject = GetParameterSubObject(loader, dynamicPropName);
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

            // NeedToReinitializeInputPorts の発火は UI スレッドから行う必要がある。
            // Task.Run スレッドから dispatcher.Invoke（ブロッキング）を呼ぶとフリーズするため BeginInvoke を使う。
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            _ = dispatcher.BeginInvoke(() => self.SetDynamicContainer(newContainer, dynamicPropName));
        });
    }

    public static Task Calculate(
        NodeLogic self,
        string pluginName,
        PortDefinition[] portDefs,
        ref VideoEffectsLoader? loaderRef,
        ref object? lastDevicesRef)
    {
        var ctx = (EvaluationContext?)typeof(NodeLogic)
            .GetProperty("EvaluationContext", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(self);

        if (ctx is null)
            return Task.FromException(new NullReferenceException("EvaluationContext"));

        // SolidColorBrushNode / NoiseNode と同様、デバイスが変わった場合はローダーを作り直す。
        if (loaderRef == null || !ReferenceEquals(lastDevicesRef, ctx.Devices))
        {
            loaderRef?.Dispose();
            loaderRef = VideoEffectsLoader.LoadBrushSync(pluginName, ctx);
            lastDevicesRef = ctx.Devices;
        }

        var loader = loaderRef;
        var dynamicPropNames = DynamicPropNamesRegistry.TryGetValue(pluginName, out var names) ? names : [];
        return Task.Run(async () =>
        {
            var prev = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
            try
            {
                await CalculateAsync(self, portDefs, dynamicPropNames, loader, ctx)
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
        EvaluationContext ctx)
    {
        try
        {
            lock (loader)
            {
                foreach (var def in portDefs)
                {
                    var value = GetPortValue(self, def, ctx);
                    loader.SetValue(def.PropName, value)
                        .GetAwaiter()
                        .GetResult();
                }
            }

            foreach (var propName in dynamicPropNames)
            {
                var subObject = GetParameterSubObject(loader, propName);
                if (subObject == null) continue;

                var prefix = propName + ".";

                var subPropNames = subObject.GetType()
                    .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<DisplayAttribute>() != null)
                    .Select(p => prefix + p.Name)
                    .ToHashSet();

                var registeredKeys = self.Inputs.Keys
                    .Where(k => k.StartsWith(prefix))
                    .ToHashSet();

                if (!subPropNames.SetEquals(registeredKeys))
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

            self.GetType().GetProperty("Output")?.SetValue(self,
                loader.Update(out var brush, ctx.EffectDescription) ? new BrushWrapper { Brush = brush } : null);
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
#if DEBUG
            Console.WriteLine(
                $@"  BrushNodeCalculator EXCEPTION: {exception.GetType().Name}: {exception.Message}");
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

            propInfo.SetValue(
                target,
                PropertyValueTypeConverter.ConvertPropertyValue(
                    propInfo.PropertyType,
                    value));
        }
    }

    /// <summary>
    ///     VideoEffectsLoader 内の _brushParameter フィールドから、指定した [Display] プロパティ名に
    ///     対応するネストされたサブオブジェクトを取得する。
    /// </summary>
    private static object? GetParameterSubObject(VideoEffectsLoader loader, string propName)
    {
        var paramField = typeof(VideoEffectsLoader)
            .GetField("_brushParameter", BindingFlags.NonPublic | BindingFlags.Instance);
        var parameterInstance = paramField?.GetValue(loader);
        if (parameterInstance == null) return null;

        var result = FindPropertyByDisplay(parameterInstance, propName);
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
            _ => raw
        };
    }
}