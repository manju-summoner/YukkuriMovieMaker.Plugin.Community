using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Command;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Utility;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.CustomEditor.ControlReplacement;

/// <summary>
///     CustomEditorPort がホストする外部コントロールツリーの中に、置換対象として登録済みのコントロールが現れたら、
///     こちら側の PortControlBase 派生コントロールへ差し替える。
/// </summary>
internal static class ControlReplacementEngine
{
    private static bool _initialized;
    private static readonly Lock InitLock = new();

    /// <summary>
    ///     置換ルール一覧。
    /// </summary>
    private static readonly List<ControlReplacementRule> Rules =
    [
        new()
        {
            // ColorPicker.Value と ColorPort.SelectedColor はどちらも Color 型。
            SourceTypeFullName = "YukkuriMovieMaker.Controls.ColorPicker",
            SourceValuePropertyName = "Value",
            TargetValueProperty = ColorPort.SelectedColorProperty,
            CreateReplacement = static () => new ColorPort(),
            NotifyPreviewOnEachChange = true,
            Description = "ColorPicker → ColorPort"
        },
        new()
        {
            SourceTypeFullName = "YukkuriMovieMaker.Controls.AnimationSlider",
            SourceValuePropertyName = "Animation",
            TargetValueProperty = NumberPort.ValueProperty,
            CreateReplacement = static () => new NumberPort(),
            SkipAutomaticValueBinding = true,
            CanReplace = static oldControl =>
            {
                var animation = ResolveAnimation(oldControl);
                if (animation == null) return false;
                var animationType = animation.GetType().GetProperty("AnimationType")?.GetValue(animation);
                return animationType?.ToString() == "なし";
            },
            Configure = static (oldControl, newControl) =>
            {
                if (newControl is not NumberPort numberPort) return;

                if (ResolveFirstAnimationValue(oldControl) is not var (initialValue, initialValueProp)) return;
                if (initialValueProp.GetValue(initialValue) is double initial)
                    numberPort.SetValue(NumberPort.ValueProperty, (float)initial);

                var animationDp = ResolveDependencyProperty(oldControl.GetType(), "Animation");
                if (animationDp != null)
                {
                    var animationDescriptor = DependencyPropertyDescriptor.FromProperty(
                        animationDp, oldControl.GetType());
                    if (animationDescriptor != null)
                    {
                        EventHandler readBackHandler = (_, _) =>
                        {
                            if (ResolveFirstAnimationValue(oldControl) is not var (value, valueProp)) return;
                            if (valueProp.GetValue(value) is double current)
                                numberPort.SetValue(NumberPort.ValueProperty, (float)current);
                        };
                        animationDescriptor.AddValueChanged(oldControl, readBackHandler);
                        numberPort.Unloaded += (_, _) =>
                            animationDescriptor.RemoveValueChanged(oldControl, readBackHandler);
                    }
#if DEBUG
                    else
                    {
                        Debug.WriteLine(
                            "[ControlReplacementEngine] AnimationSlider の Animation は DependencyProperty として解決できませんでした。");
                    }
#endif
                }

                var descriptor = DependencyPropertyDescriptor.FromProperty(
                    NumberPort.ValueProperty, typeof(NumberPort));
                if (descriptor != null)
                {
                    EventHandler writeBackHandler = (_, _) =>
                    {
                        if (ResolveFirstAnimationValue(oldControl) is not var (value, valueProp)) return;
                        var newValue = (double)(float)numberPort.GetValue(NumberPort.ValueProperty);
                        valueProp.SetValue(value, newValue);
#if DEBUG
                        Debug.WriteLine(
                            $"[ControlReplacementEngine] NumberPort write-back: {value.GetHashCode():X8}.Value = {newValue}");
#endif
                    };
                    descriptor.AddValueChanged(numberPort, writeBackHandler);
                    numberPort.Unloaded += (_, _) => descriptor.RemoveValueChanged(numberPort, writeBackHandler);
                }

                numberPort.Loaded += (_, _) =>
                {
                    var originalBegin = numberPort.BeginEditCommand;
                    var originalEnd = numberPort.EndEditCommand;

                    numberPort.BeginEditCommand = new RelayCommand(() =>
                    {
                        originalBegin?.Execute(null);
                        if (ResolveAnimation(oldControl) is { } animation)
                            animation.GetType().GetMethod("BeginEdit")?.Invoke(animation, null);
                    });
                    numberPort.EndEditCommand = new RelayCommand(() =>
                    {
                        originalEnd?.Execute(null);
                        if (ResolveAnimation(oldControl) is { } animation)
                            animation.GetType().GetMethod("EndEditAsync")?.Invoke(animation, null);
                    });
                };
            },
            Description = "AnimationSlider → NumberPort",
            NotifyPreviewOnEachChange = true
        },
        new()
        {
            SourceTypeFullName = "YukkuriMovieMaker.Controls.ToggleSlider",
            SourceValuePropertyName = "Value",
            TargetValueProperty = BoolPort.ValueProperty,
            CreateReplacement = static () => new BoolPort(),
            Description = "ToggleSlider → BoolPort"
        },
        new()
        {
            SourceTypeFullName = "YukkuriMovieMaker.Controls.FileSelector",
            SourceValuePropertyName = "Value",
            TargetValueProperty = FilePathPort.ValueProperty,
            CreateReplacement = static () => new FilePathPort(),
            Description = "FileSelector → FilePathPort"
        },
        new()
        {
            SourceTypeFullName = "YukkuriMovieMaker.Controls.DirectorySelector",
            SourceValuePropertyName = "Value",
            TargetValueProperty = FilePathPort.ValueProperty,
            CreateReplacement = static () => new FilePathPort(),
            Description = "DirectorySelector → FilePathPort"
        },
        new()
        {
            SourceTypeFullName = "YukkuriMovieMaker.Controls.TextEditor",
            SourceValuePropertyName = "Text",
            TargetValueProperty = TextPort.ValueProperty,
            CreateReplacement = static () => new TextPort(),
            Description = "TextEditor → TextPort"
        },
        new()
        {
            SourceTypeFullName = "YukkuriMovieMaker.Controls.TextBoxSlider",
            SourceValuePropertyName = "Value",
            TargetValueProperty = NumberPort.ValueProperty,
            CreateReplacement = static () => new NumberPort(),
            NotifyPreviewOnEachChange = true,
            Description = "TextBoxSlider → NumberPort"
        }
    ];

    private static object? ResolveAnimation(FrameworkElement animationSlider)
    {
        return animationSlider.GetType().GetProperty("Animation")?.GetValue(animationSlider);
    }

    private static (object Value, PropertyInfo ValueProperty)? ResolveFirstAnimationValue(
        FrameworkElement animationSlider)
    {
        if (ResolveAnimation(animationSlider) is not { } animation) return null;

        var valuesProperty = animation.GetType().GetProperty("Values", BindingFlags.Public | BindingFlags.Instance);
        if (valuesProperty?.GetValue(animation) is not IList values || values.Count == 0)
            return null;

        var firstValue = values[0];
        var valueProperty = firstValue?.GetType().GetProperty("Value");
        if (firstValue == null || valueProperty == null) return null;

        return (firstValue, valueProperty);
    }

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (InitLock)
        {
            if (_initialized) return;
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnAnyElementLoaded),
                true);
            _initialized = true;
        }
    }

    private static void OnAnyElementLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element) return;

        try
        {
            var typeFullName = element.GetType().FullName;
            if (typeFullName == null) return;

            var matched = Rules.FirstOrDefault(r => r.SourceTypeFullName == typeFullName);
            if (matched == null) return;
            if (!IsUnderCustomEditorPort(element)) return;

            element.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                try
                {
                    TryReplace(element, matched);
                }
                catch (Exception ex)
                {
#if DEBUG
                    Debug.WriteLine($"[ControlReplacementEngine] 置換失敗: {ex}");
#endif
                }
            }));
        }
        catch (Exception ex)
        {
#if DEBUG
            Debug.WriteLine($"[ControlReplacementEngine] 置換失敗: {ex}");
#endif
        }
    }

    private static bool IsUnderCustomEditorPort(DependencyObject element)
    {
        var current = element;
        while (current != null)
        {
            if (current is CustomEditorPort) return true;

            var parent = VisualTreeHelper.GetParent(current);
            if (parent == null && current is FrameworkElement fe)
                parent = fe.Parent;

            current = parent;
        }

        return false;
    }

    private static void NotifyPreviewUpdate(DependencyObject element)
    {
        if (!TryFindCustomEditorPort(element, out var customEditorPort, out var viewModel)) return;

        customEditorPort!.SyncShadowToReal();
        viewModel!.NotifyPreviewUpdateCommand.Execute(null);
    }

    private static void SyncNearestCustomEditorPort(DependencyObject element)
    {
        if (TryFindCustomEditorPort(element, out var customEditorPort, out _))
            customEditorPort!.SyncShadowToReal();
    }

    private static bool TryFindCustomEditorPort(DependencyObject element, out CustomEditorPort? customEditorPort,
        out PortViewModel? viewModel)
    {
        var current = element;
        while (current != null)
        {
            if (current is CustomEditorPort { DataContext: PortViewModel vm } cep)
            {
                customEditorPort = cep;
                viewModel = vm;
                return true;
            }

            var parent = VisualTreeHelper.GetParent(current);
            if (parent == null && current is FrameworkElement fe)
                parent = fe.Parent;

            current = parent;
        }

        customEditorPort = null;
        viewModel = null;
        return false;
    }

    private static void RelaxAncestorGridStarSizing(DependencyObject parent, FrameworkElement child)
    {
        var currentParent = parent;
        DependencyObject currentChild = child;
        var depth = 0;

        while (currentParent != null && depth < 12)
        {
            if (currentParent is Grid grid && currentChild is UIElement childElement)
            {
                RelaxRowDefinition(grid, Grid.GetRow(childElement));
                RelaxColumnDefinition(grid, Grid.GetColumn(childElement));
            }

            if (currentParent is CustomEditorPort) break;

            currentChild = currentParent;
            currentParent = VisualTreeHelper.GetParent(currentParent);
            depth++;
        }
    }

    private static void RelaxRowDefinition(Grid grid, int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= grid.RowDefinitions.Count) return;
        var rowDefinition = grid.RowDefinitions[rowIndex];
        if (rowDefinition.ReadLocalValue(RowDefinition.HeightProperty) == DependencyProperty.UnsetValue)
            rowDefinition.Height = GridLength.Auto;
    }

    private static void RelaxColumnDefinition(Grid grid, int columnIndex)
    {
        if (columnIndex < 0 || columnIndex >= grid.ColumnDefinitions.Count) return;
        var columnDefinition = grid.ColumnDefinitions[columnIndex];
        if (columnDefinition.ReadLocalValue(ColumnDefinition.WidthProperty) == DependencyProperty.UnsetValue)
            columnDefinition.Width = GridLength.Auto;
    }

    private static void TryReplace(FrameworkElement oldControl, ControlReplacementRule rule)
    {
        if (rule.CanReplace != null && !rule.CanReplace(oldControl)) return;

        var parent = VisualTreeHelper.GetParent(oldControl);
        if (parent == null) return;

        var newControl = rule.CreateReplacement();

        if (!rule.SkipAutomaticValueBinding)
            ApplyValueBinding(oldControl, newControl, rule);

        CopyLayoutProperties(oldControl, newControl);
        RelaxAncestorGridStarSizing(parent, oldControl);

        newControl.SetBinding(PortControlBase.BeginEditCommandProperty, new Binding("BeginEditCommand"));
        newControl.SetBinding(PortControlBase.EndEditCommandProperty, new Binding("EndEditCommand"));

        rule.Configure?.Invoke(oldControl, newControl);

        newControl.Loaded += OnLoadedWrapEditCommands;

        if (rule.NotifyPreviewOnEachChange)
        {
            var descriptor = DependencyPropertyDescriptor.FromProperty(
                rule.TargetValueProperty, newControl.GetType());
            if (descriptor != null)
            {
                EventHandler previewHandler = (_, _) => NotifyPreviewUpdate(newControl);
                descriptor.AddValueChanged(newControl, previewHandler);
                newControl.Unloaded += (_, _) => descriptor.RemoveValueChanged(newControl, previewHandler);
            }

            if (newControl is ColorPort colorPort)
            {
                EventHandler colorChangingHandler = (_, _) => NotifyPreviewUpdate(newControl);
                colorPort.ValueChanging += colorChangingHandler;
                newControl.Unloaded += (_, _) => colorPort.ValueChanging -= colorChangingHandler;
            }
        }

        if (!ReplaceInParent(parent, oldControl, newControl)) return;

        InvalidateAncestorsLayout(parent);
        ForceTopLevelLayoutRefresh(newControl);
        newControl.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            try
            {
                ForceTopLevelLayoutRefresh(newControl);
            }
            catch
            {
                // ignore
            }
        }));
        return;

        void OnLoadedWrapEditCommands(object sender, RoutedEventArgs e)
        {
            newControl.Loaded -= OnLoadedWrapEditCommands;

            var originalBegin = newControl.BeginEditCommand;
            var originalEnd = newControl.EndEditCommand;

            newControl.BeginEditCommand = new RelayCommand(() =>
            {
                originalBegin?.Execute(null);
                RaiseLegacyEditorEvent(oldControl, "BeginEdit");
            });
            newControl.EndEditCommand = new RelayCommand(() =>
            {
                SyncNearestCustomEditorPort(newControl);
                RaiseLegacyEditorEvent(oldControl, "EndEdit");
#if DEBUG
                Debug.WriteLine("[ControlReplacementEngine] EndEditCommand: sync -> commit");
#endif
                originalEnd?.Execute(null);
            });
        }
    }

    private static void RaiseLegacyEditorEvent(FrameworkElement oldControl, string eventName)
    {
        try
        {
            var field = oldControl.GetType().GetField(eventName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (field?.GetValue(oldControl) is EventHandler handler)
                handler.Invoke(oldControl, EventArgs.Empty);
        }
        catch
        {
            // ignore
        }
    }

    private static void ApplyValueBinding(FrameworkElement oldControl, FrameworkElement newControl,
        ControlReplacementRule rule)
    {
        var converter = rule.ConverterFactory?.Invoke(oldControl);
        var sourceValueProperty = ResolveDependencyProperty(oldControl.GetType(), rule.SourceValuePropertyName);
        if (sourceValueProperty == null) return;

        var expression = BindingOperations.GetBindingExpression(oldControl, sourceValueProperty);
        if (expression?.ParentBinding is { } sourceBinding)
        {
            var clonedBinding = CloneBinding(sourceBinding);
            if (converter != null) clonedBinding.Converter = converter;
            newControl.SetBinding(rule.TargetValueProperty, clonedBinding);
        }
        else
        {
            var currentValue = oldControl.GetValue(sourceValueProperty);
            if (converter != null)
                currentValue = converter.Convert(currentValue, rule.TargetValueProperty.PropertyType, null,
                    CultureInfo.InvariantCulture);
            else
                currentValue =
                    PropertyValueTypeConverter.ConvertPropertyValue(rule.TargetValueProperty.PropertyType,
                        currentValue);
            try
            {
                newControl.SetValue(rule.TargetValueProperty, currentValue);
            }
            catch (Exception ex)
            {
#if DEBUG
                Debug.WriteLine(
                    $"[ControlReplacementEngine] ApplyValueBinding: {oldControl.GetType().Name}.{rule.SourceValuePropertyName} "
                    + $"({currentValue?.GetType().FullName ?? "null"} = {currentValue}) -> "
                    + $"{rule.TargetValueProperty.OwnerType.Name}.{rule.TargetValueProperty.Name} "
                    + $"(期待型: {rule.TargetValueProperty.PropertyType.FullName}, converter: {converter?.GetType().Name ?? "なし"}) で失敗: {ex.Message}");
#endif
                throw;
            }
        }
    }

    private static void ForceTopLevelLayoutRefresh(FrameworkElement newControl)
    {
        FrameworkElement? topmost = null;
        DependencyObject? current = newControl;
        var depth = 0;
        while (current != null && depth < 64)
        {
            if (current is FrameworkElement fe) topmost = fe;
            current = VisualTreeHelper.GetParent(current);
            depth++;
        }

        topmost?.UpdateLayout();
    }

    private static void InvalidateAncestorsLayout(DependencyObject start)
    {
        var current = start;
        var depth = 0;
        while (current is UIElement element && depth < 8)
        {
            element.InvalidateMeasure();
            element.InvalidateArrange();
            current = VisualTreeHelper.GetParent(element);
            depth++;
        }
    }

    private static void CopyLayoutProperties(FrameworkElement oldControl, FrameworkElement newControl)
    {
        newControl.Margin = oldControl.Margin;
        newControl.HorizontalAlignment = oldControl.HorizontalAlignment;
        newControl.VerticalAlignment = oldControl.VerticalAlignment;
        newControl.ToolTip = oldControl.ToolTip;

        Grid.SetRow(newControl, Grid.GetRow(oldControl));
        Grid.SetColumn(newControl, Grid.GetColumn(oldControl));
        Grid.SetRowSpan(newControl, Grid.GetRowSpan(oldControl));
        Grid.SetColumnSpan(newControl, Grid.GetColumnSpan(oldControl));
    }

    private static bool ReplaceInParent(DependencyObject parent, FrameworkElement oldControl,
        FrameworkElement newControl)
    {
        switch (parent)
        {
            case Panel panel:
            {
                var index = panel.Children.IndexOf(oldControl);
                if (index < 0) return false;
                panel.Children.RemoveAt(index);
                panel.Children.Insert(index, newControl);
                return true;
            }
            case Decorator decorator when ReferenceEquals(decorator.Child, oldControl):
                decorator.Child = newControl;
                return true;
            case ContentPresenter presenter when ReferenceEquals(presenter.Content, oldControl):
                presenter.Content = newControl;
                return true;
            case ContentControl contentControl when ReferenceEquals(contentControl.Content, oldControl):
                contentControl.Content = newControl;
                return true;
            default:
                return false;
        }
    }

    private static DependencyProperty? ResolveDependencyProperty(Type type, string propertyName)
    {
        var fieldName = propertyName + "Property";
        var current = type;
        while (current != null)
        {
            var field = current.GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
            if (field?.GetValue(null) is DependencyProperty dp) return dp;
            current = current.BaseType;
        }

        return null;
    }

    private static Binding CloneBinding(Binding source)
    {
        var clone = new Binding
        {
            Mode = source.Mode,
            Converter = source.Converter,
            ConverterParameter = source.ConverterParameter,
            ConverterCulture = source.ConverterCulture,
            StringFormat = source.StringFormat,
            UpdateSourceTrigger = source.UpdateSourceTrigger,
            ValidatesOnDataErrors = source.ValidatesOnDataErrors,
            ValidatesOnExceptions = source.ValidatesOnExceptions,
            NotifyOnValidationError = source.NotifyOnValidationError
        };

        if (source.RelativeSource != null)
            clone.RelativeSource = source.RelativeSource;
        else if (!string.IsNullOrEmpty(source.ElementName))
            clone.ElementName = source.ElementName;
        else if (source.Source != null)
            clone.Source = source.Source;

        if (source.Path != null)
            clone.Path = source.Path;

        return clone;
    }
}