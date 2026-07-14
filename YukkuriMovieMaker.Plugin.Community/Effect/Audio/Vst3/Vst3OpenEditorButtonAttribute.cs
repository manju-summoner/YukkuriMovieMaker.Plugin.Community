using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal class Vst3OpenEditorButtonAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new Vst3OpenEditorButton();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not Vst3OpenEditorButton editor)
                return;
            editor.ItemProperties = itemProperties;
            Vst3EditorSession.OnControlAttached(editor);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not Vst3OpenEditorButton editor)
                return;
            Vst3EditorSession.OnControlDetached(editor);
            editor.ItemProperties = null;
        }
    }

    /// <summary>
    /// VST3プラグインのエディターをモードレスで開くボタン。
    /// エディターの寿命・状態保存・Undo連携はVst3EditorSessionが管理する
    /// </summary>
    internal class Vst3OpenEditorButton : Button, IPropertyEditorControl2
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        IEditorInfo? editorInfo;

        public Vst3OpenEditorButton()
        {
            Content = Texts.Vst3EffectOpenEditorButtonName;
            Click += OnClick;
        }

        public void SetEditorInfo(IEditorInfo info)
        {
            editorInfo = info;
        }

        /// <summary>
        /// 最新のIEditorInfoスナップショット。
        /// PropertiesEditorの再構築でボタンは差し替わるため、セッション側から現在接続中のボタンの値を引くのに使う
        /// </summary>
        internal IEditorInfo? EditorInfo => editorInfo;

        internal void RaiseBeginEdit() => BeginEdit?.Invoke(this, EventArgs.Empty);
        internal void RaiseEndEdit() => EndEdit?.Invoke(this, EventArgs.Empty);

        internal IEnumerable<Vst3AudioEffect> GetTargetEffects()
            => ItemProperties?.Select(x => x.PropertyOwner).OfType<Vst3AudioEffect>() ?? [];

        void OnClick(object sender, RoutedEventArgs e)
        {
            var effects = GetTargetEffects().ToArray();
            var effect = effects.FirstOrDefault();
            if (effect is null || string.IsNullOrEmpty(effect.PluginPath) || string.IsNullOrEmpty(effect.ClassId))
                return;
            if (Vst3EditorSession.TryActivate(effect))
                return;

            var owner = Window.GetWindow(this);
            Vst3Plugin? plugin = null;
            Vst3View? view = null;
            Vst3EditorWindow? window = null;
            try
            {
                using (var module = Vst3Module.Open(effect.PluginPath))
                    plugin = module.CreatePlugin(effect.ClassId);
                plugin.SetState(effect.ComponentState, effect.ControllerState);
                plugin.Setup(editorInfo?.VideoInfo.Hz ?? 48000, 4096);

                view = plugin.CreateView();
                if (view is null)
                {
                    plugin.Dispose();
                    MessageBox.Show(owner, Texts.Vst3EffectNoEditorMessage, Texts.Vst3EffectName);
                    return;
                }

                var matchingEffects = effects.Where(x =>
                    string.Equals(x.PluginPath, effect.PluginPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.ClassId, effect.ClassId, StringComparison.OrdinalIgnoreCase)).ToArray();
                var parameterForwarder = new Vst3EditorParameterForwarder(matchingEffects);
                //エディター位置は先頭の選択項目を基準にしているため、メーター・音声もそのエフェクトだけを対象にする。
                //IEditorInfoはスナップショットかつボタンは再構築で差し替わるため、常にセッション経由で最新を引く
                var meterForwarder = new Vst3EditorMeterForwarder(
                    [effect],
                    () => (Vst3EditorSession.GetCurrentEditorInfo(effect) ?? editorInfo)?.ItemPosition.Time ?? TimeSpan.MaxValue);
                var audioFeeder = editorInfo is null
                    ? null
                    : new Vst3EditorAudioFeeder(() => Vst3EditorSession.GetCurrentEditorInfo(effect) ?? editorInfo, effect, editorInfo.VideoInfo.Hz);
                window = new Vst3EditorWindow(plugin, view, parameterForwarder, meterForwarder, audioFeeder)
                {
                    Owner = owner,
                    Title = string.IsNullOrEmpty(effect.PluginName) ? Texts.Vst3EffectName : effect.PluginName,
                };
                _ = new Vst3EditorSession(effect, this, plugin, window, matchingEffects);
                window.Closed += (_, _) =>
                {
                    // HwndHost配下のネイティブビューを破棄すると、キーボードフォーカスが
                    // 切断済み要素に残り、フォーカスを取らないタイムライン背景からの
                    // RoutedCommandが最初のMouseDownで実行できない。親の有効な要素へ戻す。
                    FocusHelper.FocusWindowContent((DependencyObject?)owner ?? this);
                };
                window.Show();
            }
            catch (Exception ex)
            {
                Log.Default.Write($"VST3エディターの表示に失敗しました。path={effect.PluginPath}", ex);
                MessageBox.Show(owner, Texts.Vst3EffectLoadErrorMessage, Texts.Vst3EffectName);
                if (window is not null)
                {
                    // セッション登録済みの場合はClose経由でプラグイン等を破棄する
                    window.Close();
                }
                else
                {
                    view?.Dispose();
                    plugin?.Dispose();
                }
            }
        }

        internal static void ApplyStateToMatchingEffects(
            IEnumerable<Vst3AudioEffect> effects,
            string pluginPath,
            string classId,
            byte[]? componentState,
            byte[]? controllerState)
        {
            foreach (var target in effects)
            {
                if (!string.Equals(target.PluginPath, pluginPath, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(target.ClassId, classId, StringComparison.OrdinalIgnoreCase))
                    continue;
                target.ComponentState = componentState;
                target.ControllerState = controllerState;
            }
        }
    }
}
