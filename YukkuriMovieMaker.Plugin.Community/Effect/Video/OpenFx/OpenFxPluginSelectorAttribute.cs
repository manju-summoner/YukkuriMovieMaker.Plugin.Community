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

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// プラグイン一覧に表示する対応コンテキストの種別
    /// </summary>
    internal enum OpenFxPluginListKind
    {
        /// <summary>フィルターコンテキスト対応（映像エフェクト用）</summary>
        Filter,
        /// <summary>トランジションコンテキスト対応（場面切り替え用）</summary>
        Transition,
        /// <summary>ジェネレーターコンテキスト対応（図形用）</summary>
        Generator,
    }

    internal class OpenFxPluginSelectorAttribute(OpenFxPluginListKind kind = OpenFxPluginListKind.Filter) : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new OpenFxPluginSelector(kind);
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not OpenFxPluginSelector editor)
                return;
            // エディタコントロールは属性の型単位でプール再利用されるため、生成時の種別が
            // そのまま残らないよう、バインドのたびに使用先の種別を設定し直す
            // （図形と場面切り替えを行き来すると、先に表示した方の一覧で固定される不具合の対策）
            editor.Kind = kind;
            editor.ItemProperties = itemProperties;
            editor.UpdateDisplay();
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not OpenFxPluginSelector editor)
                return;
            editor.ItemProperties = null;
        }
    }

    sealed class OpenFxPluginItem(OpenFxPluginInfo info)
    {
        public OpenFxPluginInfo Info { get; } = info;
        public string DisplayName => Info.DisplayName;
        public string Identifier => Info.Identifier;
        public string BinaryPath => Info.BinaryPath;
        public string Name => Info.Name;

        public bool IsFavorite
        {
            get => OpenFxSettings.Default.FavoritePluginIds.Contains(Identifier);
            set
            {
                var settings = OpenFxSettings.Default;
                var favoritePluginIds = settings.FavoritePluginIds;
                var updatedFavoritePluginIds = value
                    ? favoritePluginIds.Contains(Identifier)
                        ? favoritePluginIds
                        : favoritePluginIds.Add(Identifier)
                    : favoritePluginIds.Remove(Identifier);
                if (ReferenceEquals(favoritePluginIds, updatedFavoritePluginIds))
                    return;
                settings.FavoritePluginIds = updatedFavoritePluginIds;
                settings.Save();
            }
        }
    }

    /// <summary>
    /// OFXプラグインを選択するコンボボックス。
    /// Vst3PluginSelectorと同じ操作感で、一覧は起動時スキャンのキャッシュを表示し、
    /// 右側の更新ボタンを押したときだけ再スキャンする。
    /// </summary>
    internal class OpenFxPluginSelector : Grid, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public ItemProperty[]? ItemProperties { get; set; }

        OpenFxPluginListKind kind;
        readonly ComboBox comboBox;
        readonly Button reloadButton;
        bool isUpdatingDisplay;
        bool isReloading;

        /// <summary>
        /// 一覧に表示する対応コンテキストの種別。
        /// コントロールは属性の型単位でプール再利用されるため、バインドのたびに設定し直される
        /// </summary>
        internal OpenFxPluginListKind Kind
        {
            get => kind;
            set
            {
                if (kind == value)
                    return;
                kind = value;
                // 旧種別で絞り込んだ一覧を破棄する（次のドロップダウン表示時に現在の種別で再構築される）
                comboBox.ItemsSource = null;
            }
        }

        public OpenFxPluginSelector(OpenFxPluginListKind kind = OpenFxPluginListKind.Filter)
        {
            this.kind = kind;
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
                ToolTip = Texts.OpenFxEffectReloadPluginsToolTip,
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
            favoriteButton.SetBinding(ToggleButton.IsCheckedProperty, new Binding(nameof(OpenFxPluginItem.IsFavorite)) { Mode = BindingMode.TwoWay });
            favoriteButton.SetValue(ToolTipProperty, LocalizationTexts.FavoriteButtonToolTip);
            grid.AppendChild(favoriteButton);

            var textBlock = new FrameworkElementFactory(typeof(TextBlock));
            textBlock.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            textBlock.SetBinding(TextBlock.TextProperty, new Binding(nameof(OpenFxPluginItem.DisplayName)));
            textBlock.SetValue(Grid.ColumnProperty, 1);
            grid.AppendChild(textBlock);

            return new DataTemplate { VisualTree = grid };
        }

        public void UpdateDisplay()
        {
            var host = GetTargetHosts().FirstOrDefault();
            if (host is null)
                return;
            isUpdatingDisplay = true;
            try
            {
                if (string.IsNullOrEmpty(host.PluginId))
                {
                    comboBox.SelectedItem = null;
                    return;
                }
                var items = (comboBox.ItemsSource as IEnumerable<OpenFxPluginItem>)?.ToList() ?? [];
                var current = items.FirstOrDefault(x => IsSameId(x.Identifier, host.PluginId) && IsSameId(x.BinaryPath, host.PluginPath));
                if (current is null)
                {
                    // スキャン前・アンインストール済みでも現在の選択を表示するための仮の情報
                    current = new OpenFxPluginItem(new OpenFxPluginInfo(host.PluginPath, host.PluginId, 0, 0, host.PluginName, string.Empty, true, true, true));
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
            var plugins = OpenFxPluginScanner.CachedPlugins;
            if (plugins is null)
            {
                // 起動時スキャンが完了していない場合は完了を待つ（キャッシュ済みなら即返る）
                try
                {
                    plugins = await Task.Run(() => OpenFxPluginScanner.GetEffectPlugins());
                }
                catch (Exception ex)
                {
                    Log.Default.Write("OFXプラグインのスキャンに失敗しました。", ex);
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
                var plugins = await Task.Run(() => OpenFxPluginScanner.GetEffectPlugins(refresh: true));
                ApplyItems(plugins);
            }
            catch (Exception ex)
            {
                Log.Default.Write("OFXプラグインの再スキャンに失敗しました。", ex);
            }
            finally
            {
                reloadButton.Content = Application.Current.FindResource(IconKeys.Refresh);
                isReloading = false;
            }
        }

        void ApplyItems(IReadOnlyList<OpenFxPluginInfo> plugins)
        {
            var selected = comboBox.SelectedItem as OpenFxPluginItem;
            // 一覧には使用先のコンテキストへ対応するプラグインだけを表示する
            var items = plugins
                .Where(x => kind switch
                {
                    OpenFxPluginListKind.Transition => x.SupportsTransition,
                    OpenFxPluginListKind.Generator => x.SupportsGenerator,
                    _ => x.SupportsFilter,
                })
                .Select(x => new OpenFxPluginItem(x))
                .ToList();
            // 現在の選択がスキャン結果に含まれない場合（アンインストール済み等）も表示は維持する
            if (selected is not null && !items.Any(x => IsSameId(x.Identifier, selected.Identifier) && IsSameId(x.BinaryPath, selected.BinaryPath)))
                items.Insert(0, selected);
            items = items.OrderByDescending(x => x.IsFavorite).ToList();
            isUpdatingDisplay = true;
            try
            {
                comboBox.ItemsSource = items;
                if (selected is not null)
                    comboBox.SelectedItem = items.FirstOrDefault(x => IsSameId(x.Identifier, selected.Identifier) && IsSameId(x.BinaryPath, selected.BinaryPath));
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
            if (comboBox.SelectedItem is not OpenFxPluginItem item)
                return;
            var info = item.Info;

            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var host in GetTargetHosts())
                host.SelectPlugin(info);
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        IEnumerable<IOpenFxPluginHost> GetTargetHosts()
            => ItemProperties?.Select(x => x.PropertyOwner).OfType<IOpenFxPluginHost>() ?? [];

        /// <summary>
        /// BinaryPath・Identifierの同一性判定（Windowsのパスは大文字小文字を区別しない）
        /// </summary>
        internal static bool IsSameId(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
