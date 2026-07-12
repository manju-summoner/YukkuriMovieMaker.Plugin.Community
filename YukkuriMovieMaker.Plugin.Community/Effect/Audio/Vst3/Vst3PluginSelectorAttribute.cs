using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal class Vst3PluginSelectorAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new Vst3PluginSelector();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not Vst3PluginSelector editor)
                return;
            editor.ItemProperties = itemProperties;
            editor.UpdateDisplay();
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not Vst3PluginSelector editor)
                return;
            editor.ItemProperties = null;
        }
    }

    /// <summary>
    /// VST3エフェクトプラグインを選択するコンボボックス。
    /// ドロップダウンを開いたときにVST3ディレクトリをスキャンする。
    /// </summary>
    internal class Vst3PluginSelector : ComboBox, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        bool isScanRequested;
        bool isUpdatingDisplay;

        public Vst3PluginSelector()
        {
            DisplayMemberPath = nameof(Vst3EffectPluginInfo.DisplayName);
            DropDownOpened += OnDropDownOpened;
            SelectionChanged += OnSelectionChanged;
        }

        public void UpdateDisplay()
        {
            var effect = GetTargetEffects().FirstOrDefault();
            if (effect is null)
                return;
            isUpdatingDisplay = true;
            try
            {
                if (string.IsNullOrEmpty(effect.ClassId))
                {
                    SelectedItem = null;
                    return;
                }
                var items = (ItemsSource as IEnumerable<Vst3EffectPluginInfo>)?.ToList() ?? [];
                var current = items.FirstOrDefault(x => x.ClassId == effect.ClassId && x.ModulePath == effect.PluginPath);
                if (current is null)
                {
                    current = new Vst3EffectPluginInfo(effect.PluginPath, effect.ClassId, effect.PluginName, string.Empty);
                    items.Insert(0, current);
                    ItemsSource = items;
                }
                SelectedItem = current;
            }
            finally
            {
                isUpdatingDisplay = false;
            }
        }

        async void OnDropDownOpened(object? sender, EventArgs e)
        {
            if (isScanRequested)
                return;
            isScanRequested = true;
            try
            {
                var plugins = await Task.Run(() => Vst3PluginScanner.GetEffectPlugins());
                var selected = SelectedItem as Vst3EffectPluginInfo;
                var items = plugins.ToList();
                // 現在の選択がスキャン結果に含まれない場合（アンインストール済み等）も表示は維持する
                if (selected is not null && !items.Any(x => x.ClassId == selected.ClassId && x.ModulePath == selected.ModulePath))
                    items.Insert(0, selected);
                isUpdatingDisplay = true;
                try
                {
                    ItemsSource = items;
                    if (selected is not null)
                        SelectedItem = items.FirstOrDefault(x => x.ClassId == selected.ClassId && x.ModulePath == selected.ModulePath);
                }
                finally
                {
                    isUpdatingDisplay = false;
                }
            }
            catch (Exception ex)
            {
                // 次回ドロップダウンで再試行できるようにする
                isScanRequested = false;
                Log.Default.Write("VST3プラグインのスキャンに失敗しました。", ex);
            }
        }

        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingDisplay || ItemProperties is null)
                return;
            if (SelectedItem is not Vst3EffectPluginInfo info)
                return;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var effect in GetTargetEffects())
            {
                if (effect.ClassId == info.ClassId && effect.PluginPath == info.ModulePath)
                    continue;
                effect.PluginPath = info.ModulePath;
                effect.ClassId = info.ClassId;
                effect.PluginName = info.Name;
                // 別プラグインの状態は引き継げないため破棄する
                effect.ComponentState = null;
                effect.ControllerState = null;
            }
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        IEnumerable<Vst3AudioEffect> GetTargetEffects()
            => ItemProperties?.Select(x => x.PropertyOwner).OfType<Vst3AudioEffect>() ?? [];
    }
}
