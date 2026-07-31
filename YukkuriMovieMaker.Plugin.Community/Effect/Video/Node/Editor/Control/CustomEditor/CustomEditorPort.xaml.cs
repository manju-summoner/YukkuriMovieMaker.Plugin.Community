using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor;

/// <summary>
///     ポートとして固定に実装されていない、任意のプラグイン製コントロールをノードのポートとして表示するための
///     汎用ホスト。
///     IPropertyEditorControl を実装したコントロールを、PropertyEditorAttribute2 を継承した属性クラスを介して
///     生成・バインドする。
///     NumberPort / TextPort などと異なり、コントロールの型ごとに ControlRegistry へ個別登録する必要はない。
///     DataContext（= PortViewModel）が持つ EditorAttribute を見て、その場でコントロールを生成する。
/// </summary>
public partial class CustomEditorPort : PortControlBase
{
    private PropertyEditorAttribute2? _appliedAttribute;
    private FrameworkElement? _hostedControl;

    // 「本物の効果／パラメータ型」のシャドウ（影武者）インスタンス。MeshDeformationEditorViewModel の
    // ように、渡された item/propertyOwner を具体的な型へ明示的にキャストするカスタムエディタに対応する。
    // 動的生成したノード型そのものではキャストが必ず失敗するため、元の型のインスタンスを別途用意し、
    // 現在値をコピーしたうえで渡す。編集完了時（EndEdit）に、その内容をノード側へコピーし戻す。
    private object? _shadowInstance;
    private object? _shadowRealOwner;
    private IPortValueHost? _valueHost;

    public CustomEditorPort()
    {
        InitializeComponent();

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

        // YMM4本体の仕様どおり、コントロールは IPropertyEditorControl を実装している必要がある。
        if (control is not IPropertyEditorControl editorControl)
        {
            ShowError(port,
                new InvalidOperationException(
                    $"{control.GetType().FullName} は IPropertyEditorControl を実装していません。"));
            return;
        }

        // PropertyEditorAttribute2.SetBindings には、可能な限り「実際にこのプロパティを所有している
        // 本物の型のインスタンス」を渡す。多くのカスタムエディタ（特にダイアログを開いて編集する類の
        // もの）は、渡された item/propertyOwner に対して「編集中フラグを立てる」「他のプロパティも
        // 読む」「特定の具体的な型へキャストする」（例: MeshDeformationEditorViewModel が
        // MeshDeformationEffect へキャストする）など、1つの値の読み書きに留まらない操作をすることが
        // あり、動的生成したノード型そのものではこれらが壊れてしまう。
        //
        // そのため、以下の優先順位で item/propertyOwner を決める：
        //   1. 元の効果／パラメータ型のインスタンスを別途新しく用意し（シャドウ）、ノード側の
        //      現在値をコピーして渡す（型が一致するので最も互換性が高い）。
        //      編集完了（EndEdit）時に、シャドウの内容をノード側へコピーし戻す。
        //   2. シャドウが作れなかった場合は、ノード／コンテナの実インスタンスをそのまま渡す
        //      （型は動的生成型のままだが、値の読み書き自体は正しく行える）。
        //   3. どちらも無理な場合のみ、合成ラッパー（PortValueHost<T>）にフォールバックする。
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
            propertyInfo = hostType.GetProperty(nameof(PortValueHost<object>.Value))!;
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

        // コントロールが IPropertyEditorControl2 を実装していて、かつ
        // OpenNodeEditorButton.SetEditorInfo 経由で実際の IEditorInfo が伝播してきている場合のみ渡す。
        // Node Editor パネルを単独で操作しているだけの場合など、EditorInfo が無い状況では呼ばない
        // （コントロール側が「一度も呼ばれない」前提を許容できないケースに配慮し、
        // 意味のある値が無いなら呼ばない方が安全）。
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

        // EndEdit が一度も発火しないままコントロールが破棄されるケースへの保険として、
        // ここでも念のためシャドウの内容をノード側へコピーし戻しておく。
        CopyBackFromShadow();

        if (_hostedControl != null && _appliedAttribute != null)
            try
            {
                _appliedAttribute.ClearBindings(_hostedControl);
            }
            catch
            {
                // ClearBindings 側の実装不備でここが例外を投げても、ポートの表示自体は継続させる。
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
        // 前回の EndEdit 以降に外部要因（Undo/Redo・別のポートからの編集等）で値が変わっている
        // 可能性があるため、編集を始める前にシャドウへ最新値を取り込み直す。
        RefreshShadowFromReal();
        BeginEditCommand?.Execute(null);
    }

    private void OnControlEndEdit(object? sender, EventArgs e)
    {
        // ノード側へ書き戻してから EndEditCommand（Undo/Redo境界の確定）を呼ぶ。
        // シャドウ自体はコントロールにバインドされたまま保持し続ける（null にしない）。
        // 同じコントロールで複数回 BeginEdit/EndEdit が起きることは普通にあるため、
        // ここで null にしてしまうと2回目以降の編集が書き戻されずに失われてしまう。
        CopyBackFromShadow();
        EndEditCommand?.Execute(null);
    }

    /// <summary>
    ///     元の型のインスタンス（シャドウ）を新しく作り、現在のノード側の値をコピーする。
    ///     Animation型などコピーできないプロパティがあっても、そこだけ諦めて続行する
    ///     （このためのプロパティ単位の try/catch）。
    /// </summary>
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

        CopyProperties(realOwner, shadow);
        return shadow;
    }

    /// <summary>
    ///     動的生成した型（ノード本体／コンテナ）が静的フィールド _originalOwnerType として
    ///     埋め込んでいる「元の効果／パラメータ型」を取得する。手書きの NodeLogic クラスにはこの
    ///     フィールドが存在しないため、その場合は null（＝シャドウは使わず実インスタンスを渡す）。
    /// </summary>
    private static Type? ResolveOriginalOwnerType(object owner)
    {
        var field = owner.GetType().GetField("_originalOwnerType", BindingFlags.NonPublic | BindingFlags.Static);
        return field?.GetValue(null) as Type;
    }

    /// <summary>
    ///     InputImage / Output（画像入出力ポート）を除く、名前が一致する public プロパティを
    ///     片方向にコピーする。個々のプロパティでの失敗（型の不一致、Animation型など）は無視して
    ///     次のプロパティへ進む。
    /// </summary>
    private static void CopyProperties(object from, object to)
    {
        var fromProps = from.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var toType = to.GetType();

        foreach (var fromProp in fromProps)
        {
            if (fromProp.Name is "InputImage" or "Output") continue;
            if (!fromProp.CanRead) continue;

            var toProp = toType.GetProperty(fromProp.Name, BindingFlags.Public | BindingFlags.Instance);
            if (toProp == null || !toProp.CanWrite) continue;

            try
            {
                var value = fromProp.GetValue(from);
                toProp.SetValue(to, value);
            }
            catch
            {
                // 個別のプロパティのコピー失敗は無視してよい（型が食い違う場合は諦める）。
            }
        }
    }

    private void CopyBackFromShadow()
    {
        if (_shadowInstance == null || _shadowRealOwner == null) return;

        CopyProperties(_shadowInstance, _shadowRealOwner);
    }

    /// <summary>
    ///     ノード側（本物）の現在値をシャドウへ取り込み直す。BeginEdit のたびに呼ぶことで、
    ///     前回の EndEdit 以降に外部から値が変わっていても（Undo/Redo・SyncFromGraph による
    ///     再構築を伴わない書き換え等）、シャドウが古い値のまま編集を始めてしまうのを防ぐ。
    /// </summary>
    private void RefreshShadowFromReal()
    {
        if (_shadowInstance == null || _shadowRealOwner == null) return;

        CopyProperties(_shadowRealOwner, _shadowInstance);
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