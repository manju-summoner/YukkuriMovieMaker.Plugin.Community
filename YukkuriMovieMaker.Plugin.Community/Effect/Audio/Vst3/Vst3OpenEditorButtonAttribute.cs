using System;
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
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not Vst3OpenEditorButton editor)
                return;
            editor.ItemProperties = null;
        }
    }

    /// <summary>
    /// VST3プラグインのエディターを開くボタン。
    /// エディターを閉じたときにプラグインの状態をアイテムへ保存する。
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

        void OnClick(object sender, RoutedEventArgs e)
        {
            var effects = ItemProperties?.Select(x => x.PropertyOwner).OfType<Vst3AudioEffect>().ToArray() ?? [];
            var effect = effects.FirstOrDefault();
            if (effect is null || string.IsNullOrEmpty(effect.PluginPath) || string.IsNullOrEmpty(effect.ClassId))
                return;

            var owner = Window.GetWindow(this);
            Vst3Plugin? plugin = null;
            Vst3View? view = null;
            try
            {
                using (var module = Vst3Module.Open(effect.PluginPath))
                    plugin = module.CreatePlugin(effect.ClassId);
                plugin.SetState(effect.ComponentState, effect.ControllerState);
                plugin.Setup(editorInfo?.VideoInfo.Hz ?? 48000, 4096);

                view = plugin.CreateView();
                if (view is null)
                {
                    MessageBox.Show(owner, Texts.Vst3EffectNoEditorMessage, Texts.Vst3EffectName);
                    return;
                }

                var parameterForwarder = new Vst3EditorParameterForwarder(effects.Where(x =>
                    string.Equals(x.PluginPath, effect.PluginPath, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(x.ClassId, effect.ClassId, StringComparison.OrdinalIgnoreCase)));
                var window = new Vst3EditorWindow(plugin, view, parameterForwarder)
                {
                    Owner = owner,
                    Title = string.IsNullOrEmpty(effect.PluginName) ? Texts.Vst3EffectName : effect.PluginName,
                };
                window.ShowDialog();

                var (componentState, controllerState) = plugin.GetState();
                BeginEdit?.Invoke(this, EventArgs.Empty);
                ApplyStateToMatchingEffects(
                    ItemProperties!.Select(x => x.PropertyOwner).OfType<Vst3AudioEffect>(),
                    effect.PluginPath,
                    effect.ClassId,
                    componentState,
                    controllerState);
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Log.Default.Write($"VST3エディターの表示に失敗しました。path={effect.PluginPath}", ex);
                MessageBox.Show(owner, Texts.Vst3EffectLoadErrorMessage, Texts.Vst3EffectName);
            }
            finally
            {
                view?.Dispose();
                plugin?.Dispose();
                // HwndHost配下のネイティブビューを破棄すると、キーボードフォーカスが
                // 切断済み要素に残り、フォーカスを取らないタイムライン背景からの
                // RoutedCommandが最初のMouseDownで実行できない。親の有効な要素へ戻す。
                FocusHelper.FocusWindowContent((DependencyObject?)owner ?? this);
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
