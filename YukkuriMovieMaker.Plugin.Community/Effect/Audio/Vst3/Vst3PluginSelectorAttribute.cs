using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using LocalizationTexts = YukkuriMovieMaker.Resources.Localization.Texts;

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

    sealed class Vst3PluginItem(Vst3EffectPluginInfo info)
    {
        public Vst3EffectPluginInfo Info { get; } = info;
        public string DisplayName => Info.DisplayName;
        public string ClassId => Info.ClassId;
        public string ModulePath => Info.ModulePath;
        public string Name => Info.Name;

        public bool IsFavorite
        {
            get => Vst3Settings.Default.FavoritePluginClassIds.Contains(ClassId);
            set
            {
                var settings = Vst3Settings.Default;
                var favoritePluginClassIds = settings.FavoritePluginClassIds;
                var updatedFavoritePluginClassIds = value
                    ? favoritePluginClassIds.Contains(ClassId)
                        ? favoritePluginClassIds
                        : favoritePluginClassIds.Add(ClassId)
                    : favoritePluginClassIds.Remove(ClassId);
                if (ReferenceEquals(favoritePluginClassIds, updatedFavoritePluginClassIds))
                    return;
                settings.FavoritePluginClassIds = updatedFavoritePluginClassIds;
                settings.Save();
            }
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
            Padding = new Thickness(0);
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Center;
            ItemContainerStyle = new Style(typeof(ComboBoxItem))
            {
                BasedOn = TryFindResource(typeof(ComboBoxItem)) as Style,
                Setters =
                {
                    new Setter(ComboBoxItem.PaddingProperty, new Thickness(0)),
                    new Setter(ComboBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                }
            };
            ItemTemplate = CreateItemTemplate();
            DropDownOpened += OnDropDownOpened;
            SelectionChanged += OnSelectionChanged;
        }

        static DataTemplate CreateItemTemplate()
        {
            var grid = new FrameworkElementFactory(typeof(Grid));
            grid.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            grid.SetValue(HeightProperty, 20d);

            var favoriteColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            favoriteColumn.SetValue(ColumnDefinition.WidthProperty, GridLength.Auto);
            grid.AppendChild(favoriteColumn);

            var nameColumn = new FrameworkElementFactory(typeof(ColumnDefinition));
            nameColumn.SetValue(ColumnDefinition.WidthProperty, new GridLength(1, GridUnitType.Star));
            grid.AppendChild(nameColumn);

            var favoriteButton = new FrameworkElementFactory(typeof(FavoriteButton));
            favoriteButton.SetValue(WidthProperty, 26d);
            favoriteButton.SetValue(HeightProperty, 16d);
            favoriteButton.SetValue(Grid.ColumnProperty, 0);
            favoriteButton.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(Vst3PluginItem.IsFavorite)) { Mode = BindingMode.TwoWay });
            favoriteButton.SetValue(ToolTipProperty, LocalizationTexts.FavoriteButtonToolTip);
            grid.AppendChild(favoriteButton);

            var textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(Vst3PluginItem.DisplayName)));
            textBlock.SetValue(Grid.ColumnProperty, 1);
            grid.AppendChild(textBlock);

            return new DataTemplate { VisualTree = grid };
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
                var items = (ItemsSource as IEnumerable<Vst3PluginItem>)?.ToList() ?? [];
                var current = items.FirstOrDefault(x => x.ClassId == effect.ClassId && x.ModulePath == effect.PluginPath);
                if (current is null)
                {
                    current = new Vst3PluginItem(new Vst3EffectPluginInfo(effect.PluginPath, effect.ClassId, effect.PluginName, string.Empty));
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
            {
                var selected = SelectedItem as Vst3PluginItem;
                var items = (ItemsSource as IEnumerable<Vst3PluginItem>)?.OrderByDescending(x => x.IsFavorite).ToList() ?? [];
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
                return;
            }
            isScanRequested = true;
            try
            {
                var plugins = await Task.Run(() => Vst3PluginScanner.GetEffectPlugins());
                var selected = SelectedItem as Vst3PluginItem;
                var items = plugins.Select(x => new Vst3PluginItem(x)).ToList();
                // 現在の選択がスキャン結果に含まれない場合（アンインストール済み等）も表示は維持する
                if (selected is not null && !items.Any(x => x.ClassId == selected.ClassId && x.ModulePath == selected.ModulePath))
                    items.Insert(0, selected);
                items = items.OrderByDescending(x => x.IsFavorite).ToList();
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
            if (SelectedItem is not Vst3PluginItem item)
                return;
            var info = item.Info;

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
