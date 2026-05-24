using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public partial class EffectTabManagerControl : UserControl, IPropertyEditorControl
{
    public event EventHandler? BeginEdit;
    public event EventHandler? EndEdit;

    private TabDragDropService? _dragDropService;
    private ItemProperty[] _containerItemProperties = [];
    private ContainerEffect? _boundContainerEffect;
    private static readonly PropertyInfo ContainerEffectEffectsProperty =
        typeof(ContainerEffect).GetProperty(nameof(ContainerEffect.Effects))
            ?? throw new InvalidOperationException("ContainerEffect.Effects not found");

    public EffectTabManagerControl()
    {
        InitializeComponent();
        DataContextChanged += EffectTabManagerControl_DataContextChanged;
        EffectSelector.BeginEdit += OnEffectSelectorBeginEdit;
        EffectSelector.EndEdit += OnEffectSelectorEndEdit;
    }

    public void SetProperties(ItemProperty[] itemProperties)
    {
        if (DataContext is IDisposable old)
            old.Dispose();
        _containerItemProperties = itemProperties;
        DataContext = new EffectTabManagerViewModel(itemProperties);
    }

    public void ClearProperties()
    {
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
        DataContext = null;
        _containerItemProperties = [];
        ClearEffectSelectorBinding();
    }

    private void EffectTabManagerControl_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EffectTabManagerViewModel oldVm)
        {
            oldVm.BeginEdit -= OnBeginEdit;
            oldVm.EndEdit -= OnEndEdit;
            oldVm.ConfirmationRequested -= OnConfirmationRequested;
            oldVm.TemplateDialogRequested -= OnTemplateDialogRequested;
        }

        _dragDropService?.Dispose();
        _dragDropService = null;

        if (e.NewValue is EffectTabManagerViewModel newVm)
        {
            newVm.BeginEdit += OnBeginEdit;
            newVm.EndEdit += OnEndEdit;
            newVm.ConfirmationRequested += OnConfirmationRequested;
            newVm.TemplateDialogRequested += OnTemplateDialogRequested;
            _dragDropService = new TabDragDropService(TabListBox, newVm);

            SetEffectSelectorBinding();
        }
        else
        {
            ClearEffectSelectorBinding();
        }
    }

    private void SetEffectSelectorBinding()
    {
        if (_containerItemProperties.Length == 0)
        {
            ClearEffectSelectorBinding();
            return;
        }

        // VideoEffectSelector には VM 上の SelectedTab.Model 1 個ぶんの ItemProperty だけを渡す。
        // マルチセレクト中の他 Container への分配は EffectTabManagerViewModel.NotifyEffectsEdited
        // 経由で PersistState がまとめて行う（ApplyToAllSelectedContainers が deep clone も担当する）。
        var item = _containerItemProperties[0].Item;
        var containerEffect = (ContainerEffect)_containerItemProperties[0].PropertyOwner;

        if (_boundContainerEffect is not null)
            _boundContainerEffect.PropertyChanged -= OnContainerEffectPropertyChanged;

        _boundContainerEffect = containerEffect;
        _boundContainerEffect.PropertyChanged += OnContainerEffectPropertyChanged;

        var itemProp = new ItemProperty(item, containerEffect, ContainerEffectEffectsProperty, null);

        EffectSelector.SetBinding(
            VideoEffectSelector.EffectsProperty,
            new Binding(nameof(ContainerEffect.Effects)) { Source = containerEffect, Mode = BindingMode.TwoWay });
        EffectSelector.ItemProperties = [itemProp];
        EffectSelector.SelectFirstItem();
    }

    private void ClearEffectSelectorBinding()
    {
        if (_boundContainerEffect is not null)
        {
            _boundContainerEffect.PropertyChanged -= OnContainerEffectPropertyChanged;
            _boundContainerEffect = null;
        }

        EffectSelector.ItemProperties = null;
        BindingOperations.ClearBinding(EffectSelector, VideoEffectSelector.EffectsProperty);
    }

    private void OnContainerEffectPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ContainerEffect.Effects))
            EffectSelector.SelectFirstItem();
    }

    private void OnEffectSelectorBeginEdit(object? sender, EventArgs e)
        => BeginEdit?.Invoke(this, EventArgs.Empty);

    private void OnEffectSelectorEndEdit(object? sender, EventArgs e)
    {
        // VideoEffectSelector が EffectTab.Effects を直接書き換えても ContainerEffect.Tabs の参照は
        // 変わらないので、Tabs を新インスタンスとして代入し直して Label 更新と外部監視を発火させる。
        // マルチセレクト時の他 Container への deep clone もここで実施される。
        if (DataContext is EffectTabManagerViewModel vm)
            vm.NotifyEffectsEdited();

        EndEdit?.Invoke(this, EventArgs.Empty);
    }

    private void OnBeginEdit(object? sender, EventArgs e) => BeginEdit?.Invoke(this, EventArgs.Empty);

    private void OnEndEdit(object? sender, EventArgs e) => EndEdit?.Invoke(this, EventArgs.Empty);

    private void OnConfirmationRequested(object? sender, ConfirmationEventArgs e)
    {
        e.Confirmed = MessageBox.Show(e.Message, e.Title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void OnTemplateDialogRequested(object? sender, TemplateDialogEventArgs e)
    {
        var window = new TemplateNameWindow(e.InitialName, e.IsEditMode)
        {
            Owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? Application.Current.MainWindow
        };
        window.ShowDialog();
        e.Result = window.Result;
        e.TemplateName = window.TemplateName ?? string.Empty;
    }

    private void RenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not EffectTabItemViewModel tab) return;

        if (vm.CommitEditCommand.CanExecute(tab))
            vm.CommitEditCommand.Execute(tab);
    }

    private void RenameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (sender is not FrameworkElement fe) return;
        if (fe.DataContext is not EffectTabItemViewModel tab) return;

        if (e.Key == Key.Enter)
        {
            if (vm.CommitEditCommand.CanExecute(tab))
                vm.CommitEditCommand.Execute(tab);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            if (vm.CancelEditCommand.CanExecute(tab))
                vm.CancelEditCommand.Execute(tab);
            e.Handled = true;
        }
    }

    private void TabListBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Handled) return;

        e.Handled = true;
        var eventArg = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
        {
            RoutedEvent = MouseWheelEvent,
            Source = sender
        };
        (VisualTreeHelper.GetParent((DependencyObject)sender) as UIElement)?.RaiseEvent(eventArg);
    }

    private void ClearStashesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EffectTabManagerViewModel vm) return;
        if (vm.ClearStashesCommand.CanExecute(null))
            vm.ClearStashesCommand.Execute(null);
    }
}
