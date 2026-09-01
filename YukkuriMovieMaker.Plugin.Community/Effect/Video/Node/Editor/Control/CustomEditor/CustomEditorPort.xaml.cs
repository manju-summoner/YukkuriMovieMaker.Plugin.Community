using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor.ControlReplacement;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Effect.DynamicLoaded;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor;

/// <summary>
///     ポートとして固定に実装されていない、任意のプラグイン製コントロールをノードのポートとして表示するための汎用ホスト。
/// </summary>
public partial class CustomEditorPort
{
    private PropertyEditorAttribute2? _appliedAttribute;
    private FrameworkElement? _hostedControl;

    private object? _shadowInstance;
    private object? _shadowRealOwner;
    private IPortValueHost? _valueHost;

    public CustomEditorPort()
    {
        InitializeComponent();

        ControlReplacementEngine.EnsureInitialized();

        DataContextChanged += OnDataContextChanged;
        Unloaded += OnUnloaded;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();

        if (e.NewValue is PortViewModel port)
            Attach(port);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach();
    }

    private void Attach(PortViewModel port)
    {
        var editorAttribute = port.EditorAttribute;
        if (editorAttribute == null)
        {
#if DEBUG
            Debug.WriteLine(
                $@"[CustomEditorPort] Port '{port.Name}' has no EditorAttribute — nothing to host.");
#endif
            return;
        }

        FrameworkElement control;
        try
        {
            control = editorAttribute.Create();
        }
        catch (Exception ex)
        {
            ShowError(port, ex);
            return;
        }

        if (control is not IPropertyEditorControl editorControl)
        {
            ShowError(port,
                new InvalidOperationException(
                    $"{control.GetType().FullName} は IPropertyEditorControl を実装していません。"));
            return;
        }

        IPortValueHost? fallbackHost = null;
        object item;
        object propertyOwner;
        PropertyInfo propertyInfo;

        if (port.EditorPropertyOwner != null && port.EditorPropertyInfo != null)
        {
            var shadow = TryCreateShadowInstance(
                port.EditorPropertyOwner, port.EditorPropertyInfo.Name, out var shadowProperty);
            if (shadow != null && shadowProperty != null)
            {
                _shadowInstance = shadow;
                _shadowRealOwner = port.EditorPropertyOwner;
                item = shadow;
                propertyOwner = shadow;
                propertyInfo = shadowProperty;
            }
            else
            {
                item = port.EditorPropertyOwner;
                propertyOwner = port.EditorPropertyOwner;
                propertyInfo = port.EditorPropertyInfo;
            }
        }
        else
        {
            var hostType = typeof(PortValueHost<>).MakeGenericType(port.ValueType);
            var host = (IPortValueHost)Activator.CreateInstance(hostType, port)!;
            fallbackHost = host;
            item = host;
            propertyOwner = host;
            propertyInfo = hostType.GetProperty(nameof(PortValueHost<>.Value))!;
        }

        try
        {
            editorAttribute.SetBindings(control, item, propertyOwner, propertyInfo);
        }
        catch (Exception ex)
        {
            fallbackHost?.Detach();
            _shadowInstance = null;
            _shadowRealOwner = null;
            ShowError(port, ex);
            return;
        }

        editorControl.BeginEdit += OnControlBeginEdit;
        editorControl.EndEdit += OnControlEndEdit;

        if (control is IPropertyEditorControl2 editorControl2 && port.EditorInfo != null)
            try
            {
                editorControl2.SetEditorInfo(port.EditorInfo);
            }
            catch (Exception ex)
            {
                ShowError(port, ex);
                return;
            }

        _hostedControl = control;
        _appliedAttribute = editorAttribute;
        _valueHost = fallbackHost;

        HostPresenter.Content = control;
    }

    private void Detach()
    {
        if (_hostedControl is IPropertyEditorControl editorControl)
        {
            editorControl.BeginEdit -= OnControlBeginEdit;
            editorControl.EndEdit -= OnControlEndEdit;
        }

        CopyBackFromShadow();

        if (_hostedControl != null && _appliedAttribute != null)
            try
            {
                _appliedAttribute.ClearBindings(_hostedControl);
            }
            catch
            {
                // ignore
            }

        _valueHost?.Detach();

        _hostedControl = null;
        _appliedAttribute = null;
        _valueHost = null;
        _shadowInstance = null;
        _shadowRealOwner = null;
        HostPresenter.Content = null;
    }

    private void OnControlBeginEdit(object? sender, EventArgs e)
    {
        RefreshShadowFromReal();
        BeginEditCommand?.Execute(null);
    }

    private void OnControlEndEdit(object? sender, EventArgs e)
    {
        CopyBackFromShadow();
        EndEditCommand?.Execute(null);
    }

    private static object? TryCreateShadowInstance(object realOwner, string propertyName,
        out PropertyInfo? shadowProperty)
    {
        shadowProperty = null;

        var ownerType = ResolveOriginalOwnerType(realOwner);
        if (ownerType == null)
        {
#if DEBUG
            Debug.WriteLine(
                $@"[CustomEditorPort] {realOwner.GetType().FullName} has no _originalOwnerType — falling back to real instance.");
#endif
            return null;
        }

        object? shadow;
        try
        {
            shadow = Activator.CreateInstance(ownerType, true);
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine(
                $@"[CustomEditorPort] Failed to create shadow instance of {ownerType.FullName}: {ex}");
#endif
            return null;
        }

        if (shadow == null) return null;

        shadowProperty = ownerType.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (shadowProperty == null)
        {
#if DEBUG
            Debug.WriteLine(
                $@"[CustomEditorPort] {ownerType.FullName} has no public property named '{propertyName}'.");
#endif
            return null;
        }

        CopyProperties(realOwner, shadow, realOwner.GetHashCode());
        return shadow;
    }

    private static Type? ResolveOriginalOwnerType(object owner)
    {
        var field = owner.GetType().GetField("_originalOwnerType", BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null) as Type;
    }

    private static void CopyProperties(object from, object to, int portId)
    {
        var fromProps = from.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var toType = to.GetType();
        var fromNode = from as NodeLogic;
        var toNode = to as NodeLogic;

        foreach (var fromProp in fromProps)
        {
            if (fromProp.Name is "InputImage" or "Output") continue;
            if (!fromProp.CanRead) continue;

            var toProp = toType.GetProperty(fromProp.Name, BindingFlags.Public | BindingFlags.Instance);
            if (toProp == null) continue;

            try
            {
                object? value;
                if (fromNode != null && fromNode.Inputs.TryGetValue(fromProp.Name, out var fromPort))
                {
                    if (fromPort.IsConnected) continue;
                    value = fromPort.LocalValue;
                }
                else
                {
                    value = fromProp.GetValue(from);
                }

                if (fromProp.PropertyType == typeof(Animation) && toProp.PropertyType != typeof(Animation))
                {
                    if (toProp.CanWrite && value is Animation sourceAnimation)
                    {
                        var current = sourceAnimation.Values.Count > 0
                            ? sourceAnimation.Values[0].Value
                            : sourceAnimation.DefaultValue;
                        SetProperty(to, toProp, toNode,
                            PropertyValueTypeConverter.ConvertPropertyValue(toProp.PropertyType, current));
                    }

                    continue;
                }

                if (toProp.PropertyType == typeof(Animation) && fromProp.PropertyType != typeof(Animation))
                {
                    EffectNodeCalculator.SetSubPropertyDirect(to, toProp, value);
                    continue;
                }

                if (toProp.CanWrite)
                {
                    SetProperty(to, toProp, toNode,
                        PropertyValueTypeConverter.ConvertPropertyValue(toProp.PropertyType, value));
                    continue;
                }

                if (value == null) continue;

                var currentTarget = toProp.GetValue(to);
                if (currentTarget == null) continue;

                var copyFromMethod = toProp.PropertyType.GetMethod("CopyFrom", [value.GetType()]);
                copyFromMethod?.Invoke(currentTarget, [value]);
            }
            catch (Exception ex)
            {
#if DEBUG
                var inner = ex.InnerException != null ? $" / Inner: {ex.InnerException}" : "";
                Debug.WriteLine(
                    $"[CustomEditorPort:{portId:X8}] CopyProperties: {fromProp.Name} のコピーに失敗: {ex.Message}{inner}");
#endif
            }
        }
    }

    private static void SetProperty(object to, PropertyInfo toProp, NodeLogic? toNode, object? value)
    {
        if (toNode != null && toNode.Inputs.TryGetValue(toProp.Name, out var toPort))
        {
            toPort.SetValue(value);
            return;
        }

        toProp.SetValue(to, value);
    }

    internal void SyncShadowToReal()
    {
        CopyBackFromShadow();
    }

    private void CopyBackFromShadow()
    {
        if (_shadowInstance == null || _shadowRealOwner == null) return;

#if DEBUG
        Debug.WriteLine(
            $"[CustomEditorPort:{GetHashCode():X8}] CopyBackFromShadow: {_shadowInstance.GetType().Name} -> {_shadowRealOwner.GetType().Name}");
#endif
        CopyProperties(_shadowInstance, _shadowRealOwner, GetHashCode());
    }

    private void RefreshShadowFromReal()
    {
        if (_shadowInstance == null || _shadowRealOwner == null) return;

#if DEBUG
        Debug.WriteLine(
            $"[CustomEditorPort:{GetHashCode():X8}] RefreshShadowFromReal: {_shadowRealOwner.GetType().Name} -> {_shadowInstance.GetType().Name}");
#endif
        CopyProperties(_shadowRealOwner, _shadowInstance, GetHashCode());
    }

    private void ShowError(PortViewModel port, Exception ex)
    {
        var message = $"{port.EditorAttribute?.GetType().Name} の表示に失敗しました: {ex.Message}";
#if DEBUG
        Debug.WriteLine($@"[CustomEditorPort] {message}{Environment.NewLine}{ex}");
#endif
        HostPresenter.Content = new TextBlock
        {
            Text = message,
            Foreground = Brushes.Red,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
            ToolTip = ex.ToString()
        };
    }
}