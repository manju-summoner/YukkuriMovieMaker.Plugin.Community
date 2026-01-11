using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public partial class ExplorerAddressBar : UserControl
    {
        readonly ExplorerAddressBarViewModel viewModel;

        public event EventHandler? Navigated;

        bool isApplyingValueInternally;

        public ExplorerAddressBar()
        {
            InitializeComponent();

            viewModel = new ExplorerAddressBarViewModel();
            RootGrid.DataContext = viewModel;
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;

            viewModel.MaxFileSystemSuggestions = Math.Max(0, MaxFileSystemSuggestions);
            viewModel.MaxExternalSuggestions = Math.Max(0, MaxExternalSuggestions);
            viewModel.SetExternalSuggestions(ExternalSuggestions);

            viewModel.SetCurrentValue(Value);
        }

        #region DependencyProperties

        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(
                nameof(Value),
                typeof(string),
                typeof(ExplorerAddressBar),
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public string Value
        {
            get => (string)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value ?? string.Empty);
        }

        static void OnValueChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            var control = (ExplorerAddressBar)dependencyObject;
            if (control.isApplyingValueInternally)
                return;

            control.viewModel.SetCurrentValue(eventArgs.NewValue as string);
            control.UpdateLayoutDependentState();
        }

        public static readonly DependencyProperty ExternalSuggestionsProperty =
            DependencyProperty.Register(
                nameof(ExternalSuggestions),
                typeof(IEnumerable<AddressBarSuggestion>),
                typeof(ExplorerAddressBar),
                new PropertyMetadata(null, OnExternalSuggestionsChanged));

        public IEnumerable<AddressBarSuggestion>? ExternalSuggestions
        {
            get => (IEnumerable<AddressBarSuggestion>?)GetValue(ExternalSuggestionsProperty);
            set => SetValue(ExternalSuggestionsProperty, value);
        }

        static void OnExternalSuggestionsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            var control = (ExplorerAddressBar)dependencyObject;
            control.viewModel.SetExternalSuggestions(eventArgs.NewValue as IEnumerable<AddressBarSuggestion>);
        }

        public static readonly DependencyProperty UpdateValueEvenIfPathDoesNotExistProperty =
            DependencyProperty.Register(
                nameof(UpdateValueEvenIfPathDoesNotExist),
                typeof(bool),
                typeof(ExplorerAddressBar),
                new PropertyMetadata(true));

        public bool UpdateValueEvenIfPathDoesNotExist
        {
            get => (bool)GetValue(UpdateValueEvenIfPathDoesNotExistProperty);
            set => SetValue(UpdateValueEvenIfPathDoesNotExistProperty, value);
        }

        public static readonly DependencyProperty MaxFileSystemSuggestionsProperty =
            DependencyProperty.Register(
                nameof(MaxFileSystemSuggestions),
                typeof(int),
                typeof(ExplorerAddressBar),
                new PropertyMetadata(50, OnSuggestionLimitsChanged));

        public int MaxFileSystemSuggestions
        {
            get => (int)GetValue(MaxFileSystemSuggestionsProperty);
            set => SetValue(MaxFileSystemSuggestionsProperty, value);
        }

        public static readonly DependencyProperty MaxExternalSuggestionsProperty =
            DependencyProperty.Register(
                nameof(MaxExternalSuggestions),
                typeof(int),
                typeof(ExplorerAddressBar),
                new PropertyMetadata(50, OnSuggestionLimitsChanged));

        public int MaxExternalSuggestions
        {
            get => (int)GetValue(MaxExternalSuggestionsProperty);
            set => SetValue(MaxExternalSuggestionsProperty, value);
        }

        static void OnSuggestionLimitsChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
        {
            var control = (ExplorerAddressBar)dependencyObject;
            control.viewModel.MaxFileSystemSuggestions = Math.Max(0, control.MaxFileSystemSuggestions);
            control.viewModel.MaxExternalSuggestions = Math.Max(0, control.MaxExternalSuggestions);
            control.viewModel.RefreshSuggestions();
        }

        #endregion

        void OnLoaded(object sender, RoutedEventArgs e)
        {
            viewModel.SetFontInfo(FontFamily, FontSize, FontStyle, FontWeight, FontStretch);
            UpdateLayoutDependentState();
        }

        void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateLayoutDependentState();
        }

        void UpdateLayoutDependentState()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            viewModel.PixelsPerDip = dpi.PixelsPerDip;

            viewModel.AvailableWidth = ActualWidth;
            viewModel.UpdateBreadcrumbTokens();
        }

        void OnBreadcrumbPanelPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // メニューや候補ポップアップ操作中は編集開始しない（誤爆防止）
            if (MenuPopup.IsOpen || SuggestionsPopup.IsOpen)
                return;

            // ボタン（セグメント/区切り/ドライブ）上のクリックは編集開始しない
            if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) != null)
                return;

            BeginEdit();
            e.Handled = true;
        }

        void BeginEdit()
        {
            viewModel.BeginEdit();

            Dispatcher.BeginInvoke(new Action(() =>
            {
                EditTextBox.Focus();
                EditTextBox.SelectAll();
            }));
        }

        void CancelEdit()
        {
            viewModel.CancelEdit();
        }
        void OnEditTextBoxLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // 編集してないなら何もしない
            if (!viewModel.IsEditing)
                return;

            // フォーカス移動先がサジェスト/メニューの中ならキャンセルしない
            if (IsDescendantOf(e.NewFocus as DependencyObject, SuggestionsListBox) ||
                IsDescendantOf(e.NewFocus as DependencyObject, MenuListBox))
            {
                return;
            }

            // フォーカス移動が完了してから最終判定する（ここが重要）
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (!viewModel.IsEditing)
                    return;

                // アドレスバー内にフォーカスが残っているならキャンセルしない
                if (IsKeyboardFocusWithin)
                    return;

                // ここまで来たら、サジェストが出ていても編集キャンセルして抜ける
                MenuPopup.IsOpen = false;
                CancelEdit();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        static bool IsDescendantOf(DependencyObject? node, DependencyObject? ancestor)
        {
            if (node == null || ancestor == null)
                return false;

            var current = node;
            while (current != null)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;

                // Popup配下だと VisualTree だけで辿れないことがあるので LogicalTree も併用
                var parent = VisualTreeHelper.GetParent(current);
                current = parent ?? LogicalTreeHelper.GetParent(current);
            }

            return false;
        }

        void CommitEditFromSuggestionOrText()
        {
            // 候補があり選択中ならそれを採用
            if (viewModel.IsSuggestionsOpen && viewModel.SelectedSuggestion != null)
            {
                ApplyNewValueAndEndEdit(viewModel.ResolveSuggestionToValue(viewModel.SelectedSuggestion));
                return;
            }

            var resolved = viewModel.ResolveInputTextToValue(viewModel.EditText);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                // 空はキャンセル扱い
                CancelEdit();
                return;
            }

            if (!UpdateValueEvenIfPathDoesNotExist && !ExplorerAddressBarViewModel.ExistsDirectory(resolved))
            {
                // 存在確認モードなら更新しない（編集継続）
                return;
            }

            ApplyNewValueAndEndEdit(resolved);
        }

        void ApplyNewValueAndEndEdit(string newValue)
        {
            isApplyingValueInternally = true;
            try
            {
                if (!string.Equals(Value, newValue, StringComparison.OrdinalIgnoreCase))
                {
                    SetCurrentValue(ValueProperty, newValue);
                    Navigated?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                isApplyingValueInternally = false;
            }

            viewModel.SetCurrentValue(Value);
            viewModel.EndEditAfterCommit();
            UpdateLayoutDependentState();
        }

        void OnEditTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                if (viewModel.IsSuggestionsOpen && viewModel.Suggestions.Count > 0)
                {
                    viewModel.MoveSuggestionSelection(-1);
                    SuggestionsListBox.ScrollIntoView(viewModel.SelectedSuggestion);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Down)
            {
                if (viewModel.IsSuggestionsOpen && viewModel.Suggestions.Count > 0)
                {
                    viewModel.MoveSuggestionSelection(+1);
                    SuggestionsListBox.ScrollIntoView(viewModel.SelectedSuggestion);
                    e.Handled = true;
                }
                return;
            }

            if (e.Key == Key.Enter)
            {
                CommitEditFromSuggestionOrText();
                e.Handled = true;
            }
        }

        void OnEditTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            viewModel.RefreshSuggestions();
        }

        void OnSuggestionsListBoxMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (viewModel.SelectedSuggestion == null)
                return;

            ApplyNewValueAndEndEdit(viewModel.ResolveSuggestionToValue(viewModel.SelectedSuggestion));
        }

        async void OnDriveMenuButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement anchor)
                return;

            viewModel.MenuEntries.Clear();
            OpenMenuPopup(anchor);

            await viewModel.LoadDriveMenuAsync();
        }

        void OnEllipsisButtonClick(object sender, RoutedEventArgs e)
        {
            if (!viewModel.CanOpenEllipsisMenu)
                return;

            if (sender is not FrameworkElement anchor)
                return;

            viewModel.LoadEllipsisMenu();
            OpenMenuPopup(anchor);
        }

        async void OnSeparatorButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement anchor)
                return;

            if (sender is Button button && button.CommandParameter is string leftPath && !string.IsNullOrWhiteSpace(leftPath))
            {
                viewModel.MenuEntries.Clear();
                OpenMenuPopup(anchor);
                await viewModel.LoadSubfolderMenuAsync(leftPath);
            }
        }

        void OnBreadcrumbSegmentButtonClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not AddressBarBreadcrumbToken token)
                return;

            var segment = token.Segment;
            if (segment == null)
                return;

            if (segment.Kind == AddressBarBreadcrumbSegmentKind.Ellipsis)
            {
                OnEllipsisButtonClick(sender, e);
                return;
            }

            if (string.IsNullOrWhiteSpace(segment.FullPath))
                return;

            ApplyNewValueAndEndEdit(ExplorerAddressBarViewModel.NormalizeOutputPath(segment.FullPath));
        }

        void OnMenuListBoxMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (MenuListBox.SelectedItem is not AddressBarMenuEntry menuEntry)
                return;

            if (string.IsNullOrWhiteSpace(menuEntry.FullPath))
                return;

            MenuPopup.IsOpen = false;
            ApplyNewValueAndEndEdit(ExplorerAddressBarViewModel.NormalizeOutputPath(menuEntry.FullPath));
        }

        void OpenMenuPopup(FrameworkElement anchor)
        {
            MenuPopup.PlacementTarget = anchor;
            MenuPopup.IsOpen = true;
        }

        static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
        {
            var current = start;
            while (current != null)
            {
                if (current is T match)
                    return match;

                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
