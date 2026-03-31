using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows;
using SharpGen.Runtime;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.ItemEditor;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Effects;
using Rect = Vortice.Mathematics.Rect;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

public class VideoEffectsLoader : IDisposable
{
    private static readonly ConcurrentDictionary<string, byte[]> ShaderDictionaries = [];
    private readonly IBrushParameter? _brushParameter;
    private readonly ShaderEffect? _shaderEffect;
    private readonly EffectType _type;
    private readonly IVideoEffect? _videoEffect;
    private IVideoEffectProcessor? _processor;
    private IBrushSource? _source;

    private VideoEffectsLoader(IVideoEffect? effect)
    {
        _videoEffect = effect ?? throw new ArgumentNullException(nameof(effect), TextUi.UnableLoadEffect);
        _type = EffectType.VideoEffect;
    }

    private VideoEffectsLoader(ShaderEffect? effect)
    {
        if (effect == null) throw new ArgumentNullException(nameof(effect), TextUi.UnableGenerateEffect);
        _shaderEffect = effect;
        _type = EffectType.ShaderEffect;
    }

    private VideoEffectsLoader(IBrushPlugin? brush, EvaluationContext evaluationContext)
    {
        ArgumentNullException.ThrowIfNull(evaluationContext);
        _brushParameter = brush?.CreateBrushParameter();
        _source = _brushParameter?.CreateBrush(evaluationContext.Devices);
        _type = EffectType.BrushEffect;
    }

    public void Dispose()
    {
        _processor?.Output.Dispose();
        _processor?.ClearInput();
        _processor?.Dispose();
        _processor = null;
        _shaderEffect?.Output?.Dispose();
        for (var i = 0; i < _shaderEffect?.InputCount; i++)
            _shaderEffect?.SetInput(i, null, true);
        _shaderEffect?.Dispose();
        _source?.Brush.Dispose();
        _source?.Dispose();
        _source = null;
        GC.SuppressFinalize(this);
    }

    ~VideoEffectsLoader()
    {
        Dispose();
    }

    public VideoEffectsLoader SetValue(params object[]? values)
    {
        if (values == null) return this;
        switch (_type)
        {
            case EffectType.ShaderEffect when _shaderEffect != null:
            {
                lock (_shaderEffect)
                {
                    for (var i = 0; i < values.Length; i++) _shaderEffect.SetValueByIndex(i, values[i]);
                }

                return this;
            }
            case EffectType.VideoEffect when _videoEffect != null:
            case EffectType.BrushEffect when _brushParameter != null:
            default:
            {
                return this;
            }
        }
    }

    public async Task<VideoEffectsLoader> SetValue(string propertyName, object? value)
    {
        switch (_type)
        {
            case EffectType.ShaderEffect when _shaderEffect != null:
                lock (_shaderEffect)
                {
                    _shaderEffect.SetValueByName(propertyName, value);
                }

                break;
            case EffectType.VideoEffect when _videoEffect != null:
            {
                // Recursively search for properties with DisplayAttribute within the _videoEffect hierarchy,
                // and match the property name (identifier) with the argument propertyName
                var result = FindPropertyByDisplay(_videoEffect, propertyName);
                if (result == null)
                    throw new ArgumentException(string.Format(TextUi.PropertyNotFound, propertyName),
                        nameof(propertyName));

                var (targetObject, propInfo) = result.Value;

                // If the property type is Animation, update the Values property of the Animation object directly
                if (propInfo.PropertyType == typeof(Animation))
                {
                    // Retrieve the Animation object. If it does not exist, create a new one
                    if (propInfo.GetValue(targetObject) is not Animation animObj)
                    {
                        if (!propInfo.CanWrite)
                            throw new InvalidOperationException(string.Format(TextUi.PropertyReadOnly, propertyName));
                        animObj = Activator.CreateInstance<Animation>()
                                  ?? throw new InvalidOperationException(
                                      TextUi.UnableCreateAnimationInstance);
                        // Set the Animation object to the target property only if it did not exist
                        propInfo.SetValue(targetObject, animObj);
                    }

                    // Retrieve the Values property of the Animation object
                    var valuesProp = animObj.GetType()
                        .GetProperty("Values", BindingFlags.Public | BindingFlags.Instance);
                    if (valuesProp == null || !valuesProp.CanRead || !valuesProp.CanWrite)
                        throw new InvalidOperationException(
                            TextUi.AnimationValuesPropertyError);

                    // Create a new AnimationValue and add it to the existing list
                    var newList =
                        ImmutableList<AnimationValue>.Empty.Add(new AnimationValue(Convert.ToDouble(value ?? 0)));
                    // Update the Values property of the Animation object directly
                    animObj.BeginEdit();
                    valuesProp.SetValue(animObj, newList);
                    await animObj.EndEditAsync();
                }
                else
                {
                    if (!propInfo.CanWrite)
                        throw new InvalidOperationException(string.Format(TextUi.PropertyReadOnly, propertyName));
                    // For non-Animation types, set the value to the property as usual
                    propInfo.SetValue(targetObject, value);
                }

                if (_videoEffect is IEditable editable) await editable.EndEditAsync();
            }
                break;
            case EffectType.BrushEffect when _brushParameter != null:
            {
                // Recursively search for properties with DisplayAttribute within the _brushParameter hierarchy,
                // and match the property name (identifier) with the argument propertyName
                var result = FindPropertyByDisplay(_brushParameter, propertyName);
                if (result == null)
                    throw new ArgumentException(string.Format(TextUi.PropertyNotFound, propertyName),
                        nameof(propertyName));

                var (targetObject, propInfo) = result.Value;

                // If the property type is Animation, update the Values property of the Animation object directly
                if (propInfo.PropertyType == typeof(Animation))
                {
                    // Retrieve the Animation object. If it does not exist, create a new one
                    if (propInfo.GetValue(targetObject) is not Animation animObj)
                    {
                        if (!propInfo.CanWrite)
                            throw new InvalidOperationException(string.Format(TextUi.PropertyReadOnly, propertyName));
                        animObj = Activator.CreateInstance<Animation>()
                                  ?? throw new InvalidOperationException(
                                      TextUi.UnableCreateAnimationInstance);
                        // Set the Animation object to the target property only if it did not exist
                        propInfo.SetValue(targetObject, animObj);
                    }

                    // Retrieve the Values property of the Animation object
                    var valuesProp = animObj.GetType()
                        .GetProperty("Values", BindingFlags.Public | BindingFlags.Instance);
                    if (valuesProp == null || !valuesProp.CanRead || !valuesProp.CanWrite)
                        throw new InvalidOperationException(
                            TextUi.AnimationValuesPropertyError);

                    // Create a new AnimationValue and add it to the existing list
                    var newList =
                        ImmutableList<AnimationValue>.Empty.Add(new AnimationValue(Convert.ToDouble(value ?? 0)));
                    // Update the Values property of the Animation object directly
                    animObj.BeginEdit();
                    valuesProp.SetValue(animObj, newList);
                    await animObj.EndEditAsync();
                }
                else
                {
                    if (!propInfo.CanWrite)
                        throw new InvalidOperationException(string.Format(TextUi.PropertyReadOnly, propertyName));
                    // For non-Animation types, set the value to the property as usual
                    propInfo.SetValue(targetObject, value);
                }

                if (_brushParameter is IEditable editable) await editable.EndEditAsync();
            }
                break;
        }

        return this;

        (object target, PropertyInfo property)? FindPropertyByDisplay(object? obj, string name)
        {
            if (obj == null) return null;

            // Traverse the properties directly under obj
            foreach (var prop in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                // Exclude properties that require parameters, such as indexers
                if (prop.GetIndexParameters().Length > 0)
                    continue;

                // Check if DisplayAttribute is applied and compare the property name
                var displayAttr = prop.GetCustomAttribute<DisplayAttribute>();
                if (displayAttr != null && prop.Name == name) return (obj, prop);

                // Recursively search (excluding string type)
                if (prop is not { CanRead: true, PropertyType.IsClass: true } ||
                    prop.PropertyType == typeof(string)) continue;
                object? subObj;
                try
                {
                    subObj = prop.GetValue(obj);
                }
                catch
                {
                    // Skip if an exception occurs while retrieving the property
                    continue;
                }

                if (subObj == null) continue;
                var result = FindPropertyByDisplay(subObj, name);
                if (result != null)
                    return result;
            }

            return null;
        }
    }

    public bool Update(out ID2D1Image? output, EvaluationContext evaluationContext, params ID2D1Image?[] image)
    {
        output = null;
        ArgumentNullException.ThrowIfNull(evaluationContext);
        switch (_type)
        {
            case EffectType.VideoEffect when _videoEffect != null:
            {
                _processor ??= _videoEffect.CreateVideoEffect(evaluationContext.Devices);
                if (image[0] == null) return false;
                lock (_processor)
                {
                    try
                    {
                        if (_processor.Output.NativePointer == IntPtr.Zero)
                            _processor = _videoEffect.CreateVideoEffect(evaluationContext.Devices);
                        _processor.SetInput(image[0]);
                        _processor.Update(evaluationContext.EffectDescription);
                        output = _processor.Output;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            case EffectType.ShaderEffect when _shaderEffect != null:
            {
                lock (_shaderEffect)
                {
                    try
                    {
                        for (var i = 0; i < image.Length; i++)
                            _shaderEffect.SetInput(i, image[i], true);
                        output = _shaderEffect.Output;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }

            case EffectType.BrushEffect:
            default:
                return false;
        }
    }

    public bool Update(out ID2D1Brush? output, EffectDescription info)
    {
        output = null;
        switch (_type)
        {
            case EffectType.BrushEffect when _brushParameter != null:
            {
                lock (_brushParameter)
                {
                    try
                    {
                        _source?.Update(info);
                        output = _source?.Brush;
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            case EffectType.VideoEffect:
            case EffectType.ShaderEffect:
            default:
                return false;
        }
    }

    public VideoEffectsLoader SetInputImageMargin(Rect margin)
    {
        if (_type != EffectType.ShaderEffect || _shaderEffect == null) return this;
        lock (_shaderEffect)
        {
            _shaderEffect?.SetInputImageMargin(margin);
        }

        return this;
    }

    public VideoEffectsLoader SetOutputImageMargin(params Rect[] margin)
    {
        if (_type != EffectType.ShaderEffect || _shaderEffect == null) return this;
        lock (_shaderEffect)
        {
            _shaderEffect?.SetOutputImageMargin(margin);
        }

        return this;
    }

    public static async Task<VideoEffectsLoader> LoadEffect(string name)
    {
        return await Task.Run(() => LoadEffectSync(name));
    }

    public static VideoEffectsLoader LoadEffectSync(string name)
    {
        return new VideoEffectsLoader(
            Activator.CreateInstance(PluginLoader.VideoEffects.ToList().First(type => type.Name == name)) as
                IVideoEffect);
    }

    public static VideoEffectsLoader LoadBrushSync(string name, EvaluationContext evaluationContext)
    {
        return new VideoEffectsLoader(
            Activator.CreateInstance(PluginLoader.BrushPlugins.Select(plugin => plugin.GetType())
                    .First(type => type.Name == name)) as
                IBrushPlugin, evaluationContext);
    }

    public static async Task<VideoEffectsLoader> LoadEffect(List<(Type type, string name)> properties,
        Guid shaderResourceId,
        EvaluationContext evaluationContext)
    {
        return await Task.Run(() => LoadEffectSync(properties, shaderResourceId, evaluationContext));
    }

    public static VideoEffectsLoader LoadEffectSync(List<(Type type, string name)> properties,
        Guid shaderResourceId, EvaluationContext evaluationContext, int inputImageNum = 1)
    {
        if (shaderResourceId == Guid.Empty)
            throw new ArgumentException(TextUi.ShaderResourceIdEmpty);
        ArgumentNullException.ThrowIfNull(evaluationContext);
        var effect = ShaderEffect.Create(evaluationContext.Devices, properties, shaderResourceId.ToString("N"),
            inputImageNum);
        if (effect.IsEnabled) return new VideoEffectsLoader(effect);
        effect.Dispose();
        effect = null;
        return new VideoEffectsLoader(effect);
    }

    public static Guid RegisterShader(string shaderName)
    {
        byte[] shader;

        using (var resourceStream = Application.GetResourceStream(ShaderResourceUri.Get(shaderName))?.Stream)
        {
            if (resourceStream == null) return Guid.Empty;

            using (var memoryStream = new MemoryStream())
            {
                resourceStream.CopyTo(memoryStream);
                shader = memoryStream.ToArray();
            }
        }

        var id = Guid.NewGuid();
        ShaderDictionaries.TryAdd(id.ToString("N"), shader);
        return id;
    }

    public static byte[] GetShader(string id)
    {
        return ShaderDictionaries[id];
    }

    private enum EffectType
    {
        VideoEffect,
        ShaderEffect,
        BrushEffect
    }

    public abstract class ShaderEffect : D2D1CustomShaderEffectBase
    {
        private int _propertiesCount;

        public ShaderEffect(nint ptr) : base(ptr)
        {
        }

        public static ShaderEffect Create(IGraphicsDevicesAndContext context, List<(Type type, string name)> properties,
            string shaderId,
            int inputImageNum)
        {
            // Generate a unique class name based on properties
            var className = $"ShaderEffect_{shaderId}_{string.Join("_", properties.Select(p => p.type.Name + p.name))}";
            var effectType = GenerateEffectType(className, properties, shaderId, inputImageNum);

            var effectInstance = Activator.CreateInstance(effectType, context) as ShaderEffect
                                 ?? throw new InvalidOperationException(TextUi.CannotCreateEffectInstance);

            effectInstance._propertiesCount = properties.Count;

            return effectInstance
                   ?? throw new InvalidOperationException(TextUi.CannotCastToShaderEffect);
        }

        public void SetValueByIndex(int index, object? value)
        {
            var properties = GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            if (index < 0 || index >= properties.Length)
                throw new IndexOutOfRangeException(TextUi.IndexOutOfRange);

            var property = properties[index];

            if (!property.CanWrite)
                throw new InvalidOperationException(string.Format(TextUi.PropertyReadOnly, property.Name));

            property.SetValue(this, value);
        }

        public void SetInputImageMargin(Rect margin)
        {
            SetValue(_propertiesCount + 0, (int)margin.Left);
            SetValue(_propertiesCount + 1, (int)margin.Top);
            SetValue(_propertiesCount + 2, (int)margin.Right);
            SetValue(_propertiesCount + 3, (int)margin.Bottom);
        }

        public void SetOutputImageMargin(Rect[] margins)
        {
            for (var i = 0; i < margins.Length; i++)
            {
                SetValue(_propertiesCount + 4 * i + 4, (int)margins[i].Left);
                SetValue(_propertiesCount + 4 * i + 5, (int)margins[i].Top);
                SetValue(_propertiesCount + 4 * i + 6, (int)margins[i].Right);
                SetValue(_propertiesCount + 4 * i + 7, (int)margins[i].Bottom);
            }
        }

        public void SetValueByName(string propertyName, object? value)
        {
            // 指定されたプロパティ名（名前文字列）に対応するプロパティを取得する（BindingFlags: 公開・インスタンス）
            var property = GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            if (property == null)
                throw new ArgumentException(string.Format(TextUi.PropertyReadOnly, propertyName),
                    nameof(propertyName));
            if (!property.CanWrite)
                throw new InvalidOperationException(string.Format(TextUi.PropertyReadOnly, propertyName));
            property.SetValue(this, value);
        }

        private static Type GenerateEffectType(string className, List<(Type, string)> properties, string shaderId,
            int inputImageNum)
        {
            AssemblyName assemblyName = new("DynamicID2D1PropertiesAssembly");
#if ASM_EXPORT
            var persistedAssemblyBuilder = new PersistedAssemblyBuilder(assemblyName, typeof(object).Assembly);
            var moduleBuilder = persistedAssemblyBuilder.DefineDynamicModule("MainModule");
#else
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
            var moduleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
#endif

            var typeBuilder = moduleBuilder.DefineType(className, TypeAttributes.Public, typeof(ShaderEffect));

            var effectImplType =
                DynamicEffectImplGenerator.GenerateEffectImpl(properties, shaderId, moduleBuilder, inputImageNum);

            var index = 0;
            foreach (var (type, name) in properties)
            {
                //
                // Define getter
                // {type} get_{name}()
                // {
                //     return GetValue<{type}>({index});
                //     // e.g. if type is float:
                //     //    return GetFloatValue(index);
                //     // * The method info is from MakeGetterMethodInfo().
                // }
                //
                var getterBuilder = typeBuilder.DefineMethod(
                    $"get_{name}",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    type,
                    Type.EmptyTypes
                );

                // Get the method info for the getter
                var getterInfo = MakeGetterMethodInfo(type) ??
                                 throw new InvalidOperationException("Cannot get the method \"GetValue\"");

                var getterIl = getterBuilder.GetILGenerator();
                // return GetValue<{type}>({index});
                getterIl.Emit(OpCodes.Ldarg_0);
                getterIl.Emit(OpCodes.Ldc_I4, index);
                getterIl.Emit(OpCodes.Call, getterInfo);
                getterIl.Emit(OpCodes.Ret);

                //
                // Define setter
                // set_{name}({type} value)
                // {
                //    SetValue({index}, value);
                //    return;
                // }
                //
                var setterBuilder = typeBuilder.DefineMethod(
                    $"set_{name}",
                    MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                    null,
                    [type]
                );

                var setterIl = setterBuilder.GetILGenerator();
                // SetValue(index, value);
                setterIl.Emit(OpCodes.Ldarg_0);
                setterIl.Emit(OpCodes.Ldc_I4, index);
                setterIl.Emit(OpCodes.Ldarg_1);
                setterIl.Emit(OpCodes.Call, typeof(ID2D1Properties).GetMethod("SetValue", [typeof(int), type])
                                            ?? throw new InvalidOperationException(
                                                "Cannot get the method \"SetValue\""));
                setterIl.Emit(OpCodes.Nop);
                setterIl.Emit(OpCodes.Ret);

                // Define the property
                // public {type} {name} { get => get_{name}(); set => set_{name}(value); }
                var propertyBuilder = typeBuilder.DefineProperty(
                    name,
                    PropertyAttributes.None,
                    type,
                    null
                );

                propertyBuilder.SetGetMethod(getterBuilder);
                propertyBuilder.SetSetMethod(setterBuilder);

                index++;
            }

            var ctorBuilder = typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName |
                MethodAttributes.RTSpecialName,
                CallingConventions.Standard,
                [typeof(IGraphicsDevicesAndContext)]);
            var ctorIl = ctorBuilder.GetILGenerator();
            ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Ldarg_1);
            ctorIl.Emit(OpCodes.Call, typeof(D2D1CustomShaderEffectBase)
                                          .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                                          .FirstOrDefault(m => m is { Name: "Create", IsGenericMethod: true })
                                          ?.MakeGenericMethod(effectImplType)
                                      ?? throw new InvalidOperationException("Cannot get Create method"));
            ctorIl.Emit(OpCodes.Call, typeof(ShaderEffect).GetConstructor([typeof(nint)])
                                      ?? throw new InvalidOperationException("Cannot get the constructor"));
            ctorIl.Emit(OpCodes.Nop);
            ctorIl.Emit(OpCodes.Ret);

            var generateEffectType = typeBuilder.CreateTypeInfo().AsType();
            return generateEffectType
                   ?? throw new InvalidOperationException(TextUi.CannotCreateType);

            MethodInfo? MakeGetterMethodInfo(Type type)
            {
                return type switch
                {
                    not null when type == typeof(bool) => typeof(ShaderEffect).GetMethod("GetBoolValue"),
                    not null when type == typeof(Guid) => typeof(ShaderEffect).GetMethod("GetGuidValue"),
                    not null when type == typeof(float) => typeof(ShaderEffect).GetMethod("GetFloatValue"),
                    not null when type == typeof(int) => typeof(ShaderEffect).GetMethod("GetIntValue"),
                    not null when type == typeof(Matrix3x2) => typeof(ShaderEffect).GetMethod("GetMatrix3x2Value"),
                    not null when type == typeof(Matrix4x3) => typeof(ShaderEffect).GetMethod("GetMatrix4x3Value"),
                    not null when type == typeof(Matrix4x4) => typeof(ShaderEffect).GetMethod("GetMatrix4x4Value"),
                    not null when type == typeof(Matrix5x4) => typeof(ShaderEffect).GetMethod("GetMatrix5x4Value"),
                    not null when type == typeof(string) => typeof(ShaderEffect).GetMethod("GetStringValue"),
                    not null when type == typeof(uint) => typeof(ShaderEffect).GetMethod("GetUIntValue"),
                    not null when type == typeof(Vector2) => typeof(ShaderEffect).GetMethod("GetVector2Value"),
                    not null when type == typeof(Vector3) => typeof(ShaderEffect).GetMethod("GetVector3Value"),
                    not null when type == typeof(Vector4) => typeof(ShaderEffect).GetMethod("GetVector4Value"),
                    not null when type == typeof(Enum) => typeof(ShaderEffect).GetMethod("GetEnumValue")
                        ?.MakeGenericMethod(type),
                    not null when type == typeof(ComObject) => typeof(ShaderEffect).GetMethod("GetIUnknownValue")
                        ?.MakeGenericMethod(type),
                    not null when type == typeof(ID2D1ColorContext) => typeof(ShaderEffect).GetMethod(
                        "GetColorContextValue"),
                    _ => throw new InvalidOperationException("Unsupported type")
                };
            }
        }
    }
}

public static class DynamicEffectImplGenerator
{
    private static readonly Dictionary<string, Type> TypeCache = new();

    public static Type GenerateEffectImpl(List<(Type type, string name)> fields, string shaderId,
        ModuleBuilder moduleBuild, int inputImageNum)
    {
        // Ensure unique type name based on shader name and field definitions
        var typeName = $"ShaderEffectImpl_{shaderId}_{string.Join("_", fields.Select(f => f.type.Name + f.name))}";

        if (TypeCache.TryGetValue(typeName, out var value)) return value;

        // Define the EffectImpl class
        var effectImplTypeBuilder = moduleBuild.DefineType(
            typeName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AutoClass | TypeAttributes.AnsiClass |
            TypeAttributes.BeforeFieldInit
        );

        var baseType = typeof(D2D1CustomShaderEffectImplBase<>).MakeGenericType(effectImplTypeBuilder);
        effectImplTypeBuilder.SetParent(baseType);

        var customEffectAttributeConstructor = typeof(CustomEffectAttribute).GetConstructor(
                                               [
                                                   typeof(int), typeof(string), typeof(string), typeof(string),
                                                   typeof(string)
                                               ])
                                               ?? throw new InvalidOperationException("Cannot get the constructor");
        var customEffectAttributeBuilder = new CustomAttributeBuilder(
            customEffectAttributeConstructor,
            [inputImageNum, null, null, null, null]
        );
        effectImplTypeBuilder.SetCustomAttribute(customEffectAttributeBuilder);

        // Generate the ConstantBuffer struct
        var constantBufferTypeBuilder = moduleBuild.DefineType(
            $"ConstantBuffer_{shaderId}_{string.Join("_", fields.Select(f => f.type.Name + f.name))}",
            TypeAttributes.Public
            | TypeAttributes.SequentialLayout
            | TypeAttributes.AnsiClass
            | TypeAttributes.Sealed
            | TypeAttributes.BeforeFieldInit,
            typeof(ValueType));

        // Add fields to the struct
        foreach (var field in fields)
            constantBufferTypeBuilder.DefineField(field.name, field.type, FieldAttributes.Public);
        for (var i = 0; i < 4 * (1 + inputImageNum); i++)
            constantBufferTypeBuilder.DefineField("margin" + i, typeof(int), FieldAttributes.Public);
        var constantBufferType = constantBufferTypeBuilder.CreateType();

        // Define the constantBuffer field
        var constantBufferField = effectImplTypeBuilder.DefineField(
            "constantBuffer",
            constantBufferTypeBuilder,
            FieldAttributes.Private
        );

        // Define properties based on fields
        for (var i = 0; i < fields.Count; i++)
        {
            var (fieldType, fieldName) = fields[i];

            //
            // Define getter
            //
            // get_{fieldName}() {
            //     return constantBufferField.{fieldName};
            // }
            //
            var getter = effectImplTypeBuilder.DefineMethod(
                "get_" + fieldName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                fieldType,
                Type.EmptyTypes
            );
            var getIl = getter.GetILGenerator();
            getIl.DeclareLocal(fieldType);
            var getLabel = getIl.DefineLabel();
            getIl.Emit(OpCodes.Nop);
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldflda, constantBufferField);
            getIl.Emit(OpCodes.Ldfld, constantBufferType.GetField(fieldName)
                                      ?? throw new InvalidOperationException(
                                          $"The field constantBuffer.{fieldName} not found."));
            getIl.Emit(OpCodes.Stloc_0);
            getIl.Emit(OpCodes.Br_S, getLabel);

            getIl.MarkLabel(getLabel);
            getIl.Emit(OpCodes.Ldloc_0);
            getIl.Emit(OpCodes.Ret);

            //
            // Define setter
            // set_{fieldName}(value) {
            //     constantBufferField.{fieldName} = value;
            //     UpdateConstants();
            // }
            //
            var setter = effectImplTypeBuilder.DefineMethod(
                "set_" + fieldName,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                [fieldType]
            );
            var setIl = setter.GetILGenerator();

            // Set constantBuffer.{fieldName} = value
            setIl.Emit(OpCodes.Nop);
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldflda, constantBufferField);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, constantBufferType.GetField(fieldName)
                                      ?? throw new InvalidOperationException(
                                          $"The field constantBuffer.{fieldName} not found."));

            // UpdateConstants()
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Callvirt, TypeBuilder.GetMethod(baseType,
                typeof(D2D1CustomShaderEffectImplBase<>).GetMethod(
                    "UpdateConstants",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException(
                    "Cannot get the method \"UpdateConstants\"")));
            setIl.Emit(OpCodes.Nop);
            setIl.Emit(OpCodes.Ret);

            // Define property
            var propertyBuilder = effectImplTypeBuilder.DefineProperty(
                fieldName,
                PropertyAttributes.None,
                fieldType,
                Type.EmptyTypes
            );

            // Map getter and setter to the property
            propertyBuilder.SetGetMethod(getter);
            propertyBuilder.SetSetMethod(setter);

            // Add CustomEffectProperty attribute
            // [CustomEffectProperty(PropertyType.{PropertyType}, i)]
            var customEffectPropertyAttributeConstructor =
                typeof(CustomEffectPropertyAttribute).GetConstructor([typeof(PropertyType), typeof(int)]) ??
                throw new InvalidOperationException("Cannot get the constructor");
            CustomAttributeBuilder customEffectPropertyAttributeBuilder = new(
                customEffectPropertyAttributeConstructor,
                [GetPropertyType(fieldType), i]
            );
            propertyBuilder.SetCustomAttribute(customEffectPropertyAttributeBuilder);
        }

        List<MethodBuilder> marginGetter = new();
        for (var i = 0; i < 4 * (1 + inputImageNum); i++)
        {
            //
            // Define getter
            //
            // get_margin{0...}() {
            //     return constantBufferField.margin{0...};
            // }
            //
            var getter = effectImplTypeBuilder.DefineMethod(
                "get_margin" + i,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                typeof(int),
                Type.EmptyTypes
            );
            var getIl = getter.GetILGenerator();
            getIl.DeclareLocal(typeof(int));
            getIl.Emit(OpCodes.Ldarg_0);
            getIl.Emit(OpCodes.Ldflda, constantBufferField);
            getIl.Emit(OpCodes.Ldfld, constantBufferType.GetField("margin" + i)
                                      ?? throw new InvalidOperationException(
                                          $"The field constantBuffer.{"margin" + i} not found."));
            getIl.Emit(OpCodes.Ret);

            //
            // Define setter
            // set_margin{0...}(value) {
            //     constantBufferField.margin{0...} = value;
            // }
            //
            var setter = effectImplTypeBuilder.DefineMethod(
                "set_margin" + i,
                MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig,
                null,
                [typeof(int)]
            );
            var setIl = setter.GetILGenerator();

            // Set constantBuffer.margin{0...} = value
            setIl.Emit(OpCodes.Ldarg_0);
            setIl.Emit(OpCodes.Ldflda, constantBufferField);
            setIl.Emit(OpCodes.Ldarg_1);
            setIl.Emit(OpCodes.Stfld, constantBufferType.GetField("margin" + i)
                                      ?? throw new InvalidOperationException(
                                          $"The field constantBuffer.{"margin" + i} not found."));
            setIl.Emit(OpCodes.Ret);

            // Define property
            var propertyBuilder = effectImplTypeBuilder.DefineProperty(
                "margin" + i,
                PropertyAttributes.None,
                typeof(int),
                Type.EmptyTypes
            );

            // Map getter and setter to the property
            propertyBuilder.SetGetMethod(getter);
            propertyBuilder.SetSetMethod(setter);

            // Add CustomEffectProperty attribute
            // [CustomEffectProperty(PropertyType.{PropertyType}, i)]
            var customEffectPropertyAttributeConstructor =
                typeof(CustomEffectPropertyAttribute).GetConstructor([typeof(PropertyType), typeof(int)]) ??
                throw new InvalidOperationException("Cannot get the constructor");
            CustomAttributeBuilder customEffectPropertyAttributeBuilder = new(
                customEffectPropertyAttributeConstructor,
                [GetPropertyType(typeof(int)), i + fields.Count]
            );
            propertyBuilder.SetCustomAttribute(customEffectPropertyAttributeBuilder);
            marginGetter.Add(getter);
        }

        //
        // Define constructor
        // public EffectImpl() : base({shader}) { }
        //
        var constructor = effectImplTypeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig |
            MethodAttributes.RTSpecialName,
            CallingConventions.Standard,
            Type.EmptyTypes
        );

        var ctorIl = constructor.GetILGenerator();
        ctorIl.Emit(OpCodes.Ldarg_0);
        ctorIl.Emit(OpCodes.Ldstr, shaderId);
        ctorIl.Emit(OpCodes.Call,
            typeof(VideoEffectsLoader).GetMethod("GetShader", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Cannot get the method \"GetShader\""));
        ctorIl.Emit(OpCodes.Call,
            TypeBuilder.GetConstructor(baseType, typeof(D2D1CustomShaderEffectImplBase<>)
                                                     .GetConstructor([typeof(byte[])])
                                                 ?? throw new InvalidOperationException("Cannot get the constructor")));
        ctorIl.Emit(OpCodes.Ret);


        //
        // Define UpdateConstants method
        // protected override void UpdateConstants()
        // {
        //    drawInformation?.SetPixelShaderConstantBuffer(in constantBuffer);
        // }
        //
        var updateConstantsMethod = effectImplTypeBuilder.DefineMethod(
            "UpdateConstants",
            MethodAttributes.Family | MethodAttributes.Virtual | MethodAttributes.HideBySig,
            CallingConventions.Standard,
            null,
            []
        );

        var updateIl = updateConstantsMethod.GetILGenerator();
        var updateLabel1 = updateIl.DefineLabel();
        var updateLabel2 = updateIl.DefineLabel();
        updateIl.Emit(OpCodes.Ldarg_0);
        updateIl.Emit(OpCodes.Ldfld, TypeBuilder.GetField(baseType,
            typeof(D2D1CustomShaderEffectImplBase<>).GetField("drawInformation",
                BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Cannot get the field \"drawInformation\"")));
        updateIl.Emit(OpCodes.Dup);
        updateIl.Emit(OpCodes.Brtrue, updateLabel1);

        updateIl.Emit(OpCodes.Pop);
        updateIl.Emit(OpCodes.Br_S, updateLabel2);

        updateIl.MarkLabel(updateLabel1);
        updateIl.Emit(OpCodes.Ldarg_0);
        updateIl.Emit(OpCodes.Ldflda, constantBufferField);
        updateIl.Emit(OpCodes.Call, typeof(ID2D1DrawInfo).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .First(m => m is { Name: "SetPixelShaderConstantBuffer", IsGenericMethodDefinition: true }
                        && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType.IsByRef).MakeGenericMethod(constantBufferType));

        updateIl.MarkLabel(updateLabel2);
        updateIl.Emit(OpCodes.Ret);


        //
        // public override void MapInputRectsToOutputRect(RawRect[] inputRects,
        //     RawRect[] inputOpaqueSubRects,
        //     out RawRect outputRect,
        //     out RawRect outputOpaqueSubRect){
        //      outputRect = new RawRect(
        //          inputRects[0].Left - margin0,
        //          inputRects[0].Top - margin1, 
        //          inputRects[0].Right + margin2, 
        //          inputRects[0].Bottom + margin3);
        //      outputOpaqueSubRect = default;
        // }
        //
        var mapInputRectsToOutputRectMethod = effectImplTypeBuilder.DefineMethod(
            "MapInputRectsToOutputRect",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            typeof(void),
            [
                typeof(RawRect[]), typeof(RawRect[]),
                typeof(RawRect).MakeByRefType(), typeof(RawRect).MakeByRefType()
            ]);

        var mapInputRectsToOutputRectIl = mapInputRectsToOutputRectMethod.GetILGenerator();

        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_3);

        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_1);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldc_I4_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldelema, typeof(RawRect));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Left))
                                                        ?? throw new InvalidOperationException(
                                                            "Cannot get the field \"Left\""));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Call, marginGetter[0]);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Sub);

        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_1);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldc_I4_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldelema, typeof(RawRect));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Top))
                                                        ?? throw new InvalidOperationException(
                                                            "Cannot get the field \"Top\""));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Call, marginGetter[1]);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Sub);

        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_1);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldc_I4_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldelema, typeof(RawRect));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Right))
                                                        ?? throw new InvalidOperationException(
                                                            "Cannot get the field \"Right\""));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Call, marginGetter[2]);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Add);

        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_1);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldc_I4_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldelema, typeof(RawRect));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Bottom))
                                                        ?? throw new InvalidOperationException(
                                                            "Cannot get the field \"Bottom\""));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_0);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Call, marginGetter[3]);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Add);

        mapInputRectsToOutputRectIl.Emit(OpCodes.Newobj, typeof(RawRect).GetConstructors()[0]);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Stobj, typeof(RawRect));

        mapInputRectsToOutputRectIl.Emit(OpCodes.Ldarg_S, 4);
        mapInputRectsToOutputRectIl.Emit(OpCodes.Initobj, typeof(RawRect));
        mapInputRectsToOutputRectIl.Emit(OpCodes.Ret);

        //
        // public override void MapOutputRectToInputRects(RawRect outputRect, RawRect[] inputRects)
        // {
        //     inputRects[0] = new RawRect(
        //         outputRect.Left - margin4,
        //         outputRect.Top - margin5,
        //         outputRect.Right + margin6,
        //         outputRect.Bottom + margin7);
        //
        //if inputImageNum > 0:
        //     inputRects[1] = new RawRect(
        //         outputRect.Left - margin8,
        //         outputRect.Top - margin9,
        //         outputRect.Right + margin10,
        //         outputRect.Bottom + margin11);
        //     ...
        // }
        //
        var mapOutputRectToInputRectsMethod = effectImplTypeBuilder.DefineMethod(
            "MapOutputRectToInputRects",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual,
            typeof(void),
            [typeof(RawRect), typeof(RawRect[])]);

        var mapOutputRectToInputRectsIl = mapOutputRectToInputRectsMethod.GetILGenerator();

        for (var i = 0; i < inputImageNum; i++)
        {
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_2);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldc_I4, i);

            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_1);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Left))
                                                            ?? throw new InvalidOperationException(
                                                                "Cannot get the field \"Left\""));
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_0);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Call, marginGetter[4 * i + 4]);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Sub);

            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_1);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Top))
                                                            ?? throw new InvalidOperationException(
                                                                "Cannot get the field \"Top\""));
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_0);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Call, marginGetter[4 * i + 5]);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Sub);

            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_1);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Right))
                                                            ?? throw new InvalidOperationException(
                                                                "Cannot get the field \"Right\""));
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_0);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Call, marginGetter[4 * i + 6]);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Add);

            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_1);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldfld, typeof(RawRect).GetField(nameof(RawRect.Bottom))
                                                            ?? throw new InvalidOperationException(
                                                                "Cannot get the field \"Bottom\""));
            mapOutputRectToInputRectsIl.Emit(OpCodes.Ldarg_0);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Call, marginGetter[4 * i + 7]);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Add);

            mapOutputRectToInputRectsIl.Emit(OpCodes.Newobj, typeof(RawRect).GetConstructors()[0]);
            mapOutputRectToInputRectsIl.Emit(OpCodes.Stelem, typeof(RawRect));
        }

        mapOutputRectToInputRectsIl.Emit(OpCodes.Ret);

        // Create the type and cache it
        var generatedType = effectImplTypeBuilder.CreateType();
        TypeCache.Add(typeName, generatedType);
        return generatedType;
    }

    private static PropertyType GetPropertyType(Type type)
    {
        if (type == typeof(string))
            return PropertyType.String;
        if (type == typeof(bool))
            return PropertyType.Bool;
        if (type == typeof(uint))
            return PropertyType.UInt32;
        if (type == typeof(int))
            return PropertyType.Int32;
        if (type == typeof(float))
            return PropertyType.Float;
        if (type == typeof(Vector2))
            return PropertyType.Vector2;
        if (type == typeof(Vector3))
            return PropertyType.Vector3;
        if (type == typeof(Vector4))
            return PropertyType.Vector4;
        if (type == typeof(float[]))
            return PropertyType.Blob;
        if (type == typeof(IUnknown))
            return PropertyType.IUnknown;
        if (type == typeof(Enum))
            return PropertyType.Enum;
        if (type == typeof(Array))
            return PropertyType.Array;
        if (type == typeof(Guid))
            return PropertyType.Clsid;
        if (type == typeof(Matrix3x2))
            return PropertyType.Matrix3x2;
        if (type == typeof(Matrix4x3))
            return PropertyType.Matrix4x3;
        if (type == typeof(Matrix4x4))
            return PropertyType.Matrix4x4;
        if (type == typeof(Matrix5x4))
            return PropertyType.Matrix5x4;
        if (type == typeof(ID2D1ColorContext))
            return PropertyType.ColorContext;
        return PropertyType.Unknown;
    }

    public abstract class EffectImplBase : D2D1CustomShaderEffectImplBase<EffectImplBase>
    {
        public EffectImplBase(byte[] shaderBytes) : base(shaderBytes)
        {
        }

        protected abstract override void UpdateConstants();
    }
}