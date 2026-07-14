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
using YukkuriMovieMaker.Resources.Icons;
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
    /// FontComboBoxと同じ操作感で、一覧は起動時スキャンのキャッシュを表示し、
    /// 右側の更新ボタンを押したときだけ再スキャンする。
    /// </summary>
    internal class Vst3PluginSelector : Grid, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        readonly ComboBox comboBox;
        readonly Button reloadButton;
        bool isUpdatingDisplay;
        bool isReloading;

        public Vst3PluginSelector()
        {
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });

            comboBox = new ComboBox
            {
                Padding = new Thickness(0),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                ItemContainerStyle = new Style(typeof(ComboBoxItem))
                {
                    BasedOn = TryFindResource(typeof(ComboBoxItem)) as Style,
                    Setters =
                    {
                        new Setter(ComboBoxItem.PaddingProperty, new Thickness(0)),
                        new Setter(ComboBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch),
                    }
                },
                ItemTemplate = CreateItemTemplate(),
            };
            comboBox.DropDownOpened += OnDropDownOpened;
            comboBox.SelectionChanged += OnSelectionChanged;
            SetColumn(comboBox, 0);
            Children.Add(comboBox);

            reloadButton = new Button
            {
                ToolTip = Texts.Vst3EffectReloadPluginsToolTip,
            };
            reloadButton.SetResourceReference(ContentControl.ContentProperty, IconKeys.Refresh);
            reloadButton.Click += OnReloadButtonClick;
            SetColumn(reloadButton, 1);
            Children.Add(reloadButton);
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
                    comboBox.SelectedItem = null;
                    return;
                }
                var items = (comboBox.ItemsSource as IEnumerable<Vst3PluginItem>)?.ToList() ?? [];
                var current = items.FirstOrDefault(x => IsSameId(x.ClassId, effect.ClassId) && IsSameId(x.ModulePath, effect.PluginPath));
                if (current is null)
                {
                    current = new Vst3PluginItem(new Vst3EffectPluginInfo(effect.PluginPath, effect.ClassId, effect.PluginName, string.Empty));
                    items.Insert(0, current);
                    comboBox.ItemsSource = items;
                }
                comboBox.SelectedItem = current;
            }
            finally
            {
                isUpdatingDisplay = false;
            }
        }

        async void OnDropDownOpened(object? sender, EventArgs e)
        {
            // 一覧の更新は更新ボタンで行う。ここでは起動時スキャンの結果を表示へ反映するだけ
            var plugins = Vst3PluginScanner.CachedPlugins;
            if (plugins is null)
            {
                // 起動時スキャンが完了していない場合は完了を待つ（キャッシュ済みなら即返る）
                try
                {
                    plugins = await Task.Run(() => Vst3PluginScanner.GetEffectPlugins());
                }
                catch (Exception ex)
                {
                    Log.Default.Write("VST3プラグインのスキャンに失敗しました。", ex);
                    return;
                }
            }
            ApplyItems(plugins);
        }

        async void OnReloadButtonClick(object sender, RoutedEventArgs e)
        {
            if (isReloading)
                return;
            isReloading = true;
            reloadButton.Content = Application.Current.FindResource(IconKeys.LoadingAnimation);
            try
            {
                var plugins = await Task.Run(() => Vst3PluginScanner.GetEffectPlugins(refresh: true));
                ApplyItems(plugins);
            }
            catch (Exception ex)
            {
                Log.Default.Write("VST3プラグインの再スキャンに失敗しました。", ex);
            }
            finally
            {
                reloadButton.Content = Application.Current.FindResource(IconKeys.Refresh);
                isReloading = false;
            }
        }

        void ApplyItems(IReadOnlyList<Vst3EffectPluginInfo> plugins)
        {
            var selected = comboBox.SelectedItem as Vst3PluginItem;
            var items = plugins.Select(x => new Vst3PluginItem(x)).ToList();
            // 現在の選択がスキャン結果に含まれない場合（アンインストール済み等）も表示は維持する
            if (selected is not null && !items.Any(x => IsSameId(x.ClassId, selected.ClassId) && IsSameId(x.ModulePath, selected.ModulePath)))
                items.Insert(0, selected);
            items = items.OrderByDescending(x => x.IsFavorite).ToList();
            isUpdatingDisplay = true;
            try
            {
                comboBox.ItemsSource = items;
                if (selected is not null)
                    comboBox.SelectedItem = items.FirstOrDefault(x => IsSameId(x.ClassId, selected.ClassId) && IsSameId(x.ModulePath, selected.ModulePath));
            }
            finally
            {
                isUpdatingDisplay = false;
            }
        }

        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingDisplay || ItemProperties is null)
                return;
            if (comboBox.SelectedItem is not Vst3PluginItem item)
                return;
            var info = item.Info;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var effect in GetTargetEffects())
            {
                // 大文字小文字違いを別プラグイン扱いすると保存済み状態を破棄してしまうため、比較は非区別で行う
                if (IsSameId(effect.ClassId, info.ClassId) && IsSameId(effect.PluginPath, info.ModulePath))
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

        /// <summary>
        /// ModulePath・ClassId（16進文字列）の同一性判定。Windowsのパスと16進表記は大文字小文字を区別しない
        /// </summary>
        internal static bool IsSameId(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
