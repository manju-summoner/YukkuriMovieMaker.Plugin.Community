using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    public partial class Vst3EditorWindow : Window
    {
        readonly Vst3EditorSession session;
        readonly Vst3ViewHost viewHost;

        internal Vst3EditorWindow(Vst3EditorSession session, string title)
        {
            InitializeComponent();
            this.session = session;
            Title = title;
            ResizeMode = session.CanResizeView ? ResizeMode.CanResize : ResizeMode.NoResize;

            var (width, height) = session.GetViewSize();
            viewHost = new Vst3ViewHost(session);
            SetViewHostSize(width, height);
            rootGrid.Children.Add(viewHost);

            session.ViewResizeRequested += OnViewResizeRequested;
            ContentRendered += OnContentRendered;
            Closed += OnWindowClosed;
        }

        void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;
            var (width, height) = session.GetViewSize();
            SetViewHostSize(width, height);
            if (ResizeMode != ResizeMode.CanResize)
                return;
            Dispatcher.BeginInvoke(() =>
            {
                Width = ActualWidth;
                Height = ActualHeight;
                SizeToContent = SizeToContent.Manual;
                viewHost.Width = double.NaN;
                viewHost.Height = double.NaN;
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        void OnViewResizeRequested(int width, int height)
        {
            if (SizeToContent == SizeToContent.WidthAndHeight)
            {
                SetViewHostSize(width, height);
                return;
            }
            var scale = GetDpiScale();
            var chromeWidth = ActualWidth - viewHost.ActualWidth;
            var chromeHeight = ActualHeight - viewHost.ActualHeight;
            Width = width / scale.X + chromeWidth;
            Height = height / scale.Y + chromeHeight;
        }

        void OnWindowClosed(object? sender, EventArgs e)
        {
            session.ViewResizeRequested -= OnViewResizeRequested;
        }

        void SetViewHostSize(int pixelWidth, int pixelHeight)
        {
            var scale = GetDpiScale();
            viewHost.Width = pixelWidth / scale.X;
            viewHost.Height = pixelHeight / scale.Y;
        }

        (double X, double Y) GetDpiScale()
        {
            var dpi = VisualTreeHelper.GetDpi(this);
            return (dpi.DpiScaleX, dpi.DpiScaleY);
        }

        sealed class Vst3ViewHost(Vst3EditorSession session) : HwndHost
        {
            const int WsChild = 0x40000000;
            const int WsVisible = 0x10000000;
            const int WsClipChildren = 0x02000000;

            protected override HandleRef BuildWindowCore(HandleRef hwndParent)
            {
                var (width, height) = session.GetViewSize();
                var hwnd = CreateWindowExW(
                    0, "static", string.Empty, WsChild | WsVisible | WsClipChildren,
                    0, 0, width, height,
                    hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (hwnd == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create the plugin view window.");
                session.AttachView(hwnd);
                return new HandleRef(this, hwnd);
            }

            protected override void DestroyWindowCore(HandleRef hwnd)
            {
                session.DetachView();
                DestroyWindow(hwnd.Handle);
            }

            protected override void OnWindowPositionChanged(Rect rcBoundingBox)
            {
                base.OnWindowPositionChanged(rcBoundingBox);
                session.ResizeView(Math.Max(1, (int)rcBoundingBox.Width), Math.Max(1, (int)rcBoundingBox.Height));
            }

            [DllImport("user32", CharSet = CharSet.Unicode, SetLastError = true)]
            static extern IntPtr CreateWindowExW(
                int exStyle, string className, string windowName, int style,
                int x, int y, int width, int height,
                IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

            [DllImport("user32")]
            [return: MarshalAs(UnmanagedType.Bool)]
            static extern bool DestroyWindow(IntPtr hwnd);
        }
    }
}
