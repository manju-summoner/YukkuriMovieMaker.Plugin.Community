using Microsoft.Xaml.Behaviors;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    internal class NavigationInputBehavior : Behavior<FrameworkElement>
    {
        public ICommand BackCommand
        {
            get => (ICommand)GetValue(BackCommandProperty);
            set => SetValue(BackCommandProperty, value);
        }
        public static readonly DependencyProperty BackCommandProperty =
            DependencyProperty.Register(nameof(BackCommand), typeof(ICommand), typeof(NavigationInputBehavior), new PropertyMetadata(null));

        public ICommand ForwardCommand
        {
            get => (ICommand)GetValue(ForwardCommandProperty);
            set => SetValue(ForwardCommandProperty, value);
        }
        public static readonly DependencyProperty ForwardCommandProperty =
            DependencyProperty.Register(nameof(ForwardCommand), typeof(ICommand), typeof(NavigationInputBehavior), new PropertyMetadata(null));

        const int WM_SYSKEYDOWN = 0x0104;
        const int VK_LEFT = 0x25;
        const int VK_RIGHT = 0x27;

        bool isPanelActive;
        bool isHookRegistered;
        Window? parentWindow;
        MouseButtonEventHandler? windowMouseDownHandler;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
            AssociatedObject.Loaded += OnLoaded;
            AssociatedObject.Unloaded += OnUnloaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
            AssociatedObject.Loaded -= OnLoaded;
            AssociatedObject.Unloaded -= OnUnloaded;
            UnregisterHandlers();
            base.OnDetaching();
        }

        void OnLoaded(object sender, RoutedEventArgs e) => RegisterHandlers();
        void OnUnloaded(object sender, RoutedEventArgs e) => UnregisterHandlers();

        void RegisterHandlers()
        {
            UnregisterHandlers();

            parentWindow = Window.GetWindow(AssociatedObject);
            if (parentWindow is not null)
            {
                windowMouseDownHandler = OnWindowPreviewMouseDown;
                parentWindow.AddHandler(UIElement.PreviewMouseDownEvent, windowMouseDownHandler, handledEventsToo: true);
                parentWindow.Deactivated += OnParentWindowDeactivated;
            }

            ComponentDispatcher.ThreadPreprocessMessage += OnThreadPreprocessMessage;
            isHookRegistered = true;
        }

        void UnregisterHandlers()
        {
            if (isHookRegistered)
            {
                ComponentDispatcher.ThreadPreprocessMessage -= OnThreadPreprocessMessage;
                isHookRegistered = false;
            }

            if (parentWindow is not null)
            {
                parentWindow.Deactivated -= OnParentWindowDeactivated;
                if (windowMouseDownHandler is not null)
                    parentWindow.RemoveHandler(UIElement.PreviewMouseDownEvent, windowMouseDownHandler);
                parentWindow = null;
                windowMouseDownHandler = null;
            }

            isPanelActive = false;
        }

        void OnParentWindowDeactivated(object? sender, System.EventArgs e) => isPanelActive = false;

        void OnWindowPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isPanelActive = AssociatedObject.IsMouseOver;
        }

        void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.XButton1)
            {
                isPanelActive = true;
                ExecuteIfPossible(BackCommand);
                e.Handled = true;
            }
            else if (e.ChangedButton == MouseButton.XButton2)
            {
                isPanelActive = true;
                ExecuteIfPossible(ForwardCommand);
                e.Handled = true;
            }
        }

        void OnThreadPreprocessMessage(ref MSG msg, ref bool handled)
        {
            if (handled) return;
            if (msg.message != WM_SYSKEYDOWN) return;

            var isEffectivelyActive = isPanelActive || AssociatedObject.IsKeyboardFocusWithin;
            if (!isEffectivelyActive) return;

            var vk = (int)msg.wParam;
            if (vk == VK_LEFT)
            {
                AssociatedObject.Dispatcher.BeginInvoke(() => ExecuteIfPossible(BackCommand));
                handled = true;
            }
            else if (vk == VK_RIGHT)
            {
                AssociatedObject.Dispatcher.BeginInvoke(() => ExecuteIfPossible(ForwardCommand));
                handled = true;
            }
        }

        static void ExecuteIfPossible(ICommand? command)
        {
            if (command?.CanExecute(null) == true)
                command.Execute(null);
        }
    }
}
