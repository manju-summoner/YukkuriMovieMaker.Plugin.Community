using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    public partial class Vst3EditorWindow : Window
    {
        static readonly IntPtr DpiAwarenessContextSystemAware = (IntPtr)(-2);

        readonly Vst3EditorSession session;
        readonly Vst3ViewHost viewHost;
        readonly bool canResize;
        bool isUserResizing;
        int targetClientWidth;
        int targetClientHeight;
        int fitAttempts;

        internal Vst3EditorWindow(Vst3EditorSession session, string title)
        {
            InitializeComponent();
            this.session = session;
            Title = title;
            canResize = session.CanResizeView;
            ResizeMode = canResize ? ResizeMode.CanResize : ResizeMode.NoResize;

            var (width, height) = session.GetViewSize();
            viewHost = new Vst3ViewHost(session);
            SetViewHostSize(width, height);
            rootGrid.Children.Add(viewHost);

            session.ViewResizeRequested += OnViewResizeRequested;
            ContentRendered += OnContentRendered;
            Closed += OnWindowClosed;
        }

        internal void ShowEditor()
        {
            var previous = SetThreadDpiAwarenessContext(DpiAwarenessContextSystemAware);
            try
            {
                new WindowInteropHelper(this).EnsureHandle();
                Show();
            }
            finally
            {
                if (previous != IntPtr.Zero)
                    SetThreadDpiAwarenessContext(previous);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource source)
                source.AddHook(WindowProc);
        }

        IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WmEnterSizeMove = 0x0231;
            const int WmExitSizeMove = 0x0232;
            const int WmSizing = 0x0214;
            const int WmszLeft = 1;
            const int WmszTop = 3;
            const int WmszTopLeft = 4;
            const int WmszTopRight = 5;
            const int WmszBottomLeft = 7;
            if (msg == WmEnterSizeMove)
            {
                isUserResizing = true;
                return IntPtr.Zero;
            }
            if (msg == WmExitSizeMove)
            {
                isUserResizing = false;
                return IntPtr.Zero;
            }
            if (msg != WmSizing || SizeToContent != SizeToContent.Manual)
                return IntPtr.Zero;

            var rect = Marshal.PtrToStructure<Win32Rect>(lParam);
            GetWindowRect(hwnd, out var windowRect);
            GetClientRect(hwnd, out var clientRect);
            var chromeWidth = (windowRect.Right - windowRect.Left) - clientRect.Right;
            var chromeHeight = (windowRect.Bottom - windowRect.Top) - clientRect.Bottom;
            var proposedWidth = Math.Max(1, rect.Right - rect.Left - chromeWidth);
            var proposedHeight = Math.Max(1, rect.Bottom - rect.Top - chromeHeight);
            var (width, height) = session.CheckSizeConstraint(proposedWidth, proposedHeight);
            if (width == clientRect.Right && height == clientRect.Bottom
                && (proposedWidth != width || proposedHeight != height)
                && clientRect.Right > 0 && clientRect.Bottom > 0)
            {
                var ratioWidth = (double)proposedWidth / clientRect.Right;
                var ratioHeight = (double)proposedHeight / clientRect.Bottom;
                var ratio = Math.Abs(ratioWidth - 1) >= Math.Abs(ratioHeight - 1) ? ratioWidth : ratioHeight;
                (width, height) = session.CheckSizeConstraint(
                    Math.Max(1, (int)Math.Round(clientRect.Right * ratio)),
                    Math.Max(1, (int)Math.Round(clientRect.Bottom * ratio)));
            }

            var edge = (int)wParam;
            if (edge is WmszLeft or WmszTopLeft or WmszBottomLeft)
                rect.Left = rect.Right - (width + chromeWidth);
            else
                rect.Right = rect.Left + width + chromeWidth;
            if (edge is WmszTop or WmszTopLeft or WmszTopRight)
                rect.Top = rect.Bottom - (height + chromeHeight);
            else
                rect.Bottom = rect.Top + height + chromeHeight;

            Marshal.StructureToPtr(rect, lParam, false);
            handled = true;
            return (IntPtr)1;
        }

        void OnContentRendered(object? sender, EventArgs e)
        {
            ContentRendered -= OnContentRendered;
            SizeToContent = SizeToContent.Manual;
            MinWidth = 0;
            MinHeight = 0;
            MaxWidth = double.PositiveInfinity;
            MaxHeight = double.PositiveInfinity;
            if (canResize)
            {
                viewHost.Width = double.NaN;
                viewHost.Height = double.NaN;
            }
            var (width, height) = session.GetViewSize();
            FitClient(width, height);
        }

        void OnViewResizeRequested(int width, int height)
        {
            if (isUserResizing)
                return;
            if (SizeToContent == SizeToContent.WidthAndHeight)
            {
                SetViewHostSize(width, height);
                return;
            }
            FitClient(width, height);
        }

        void FitClient(int pixelWidth, int pixelHeight)
        {
            targetClientWidth = pixelWidth;
            targetClientHeight = pixelHeight;
            fitAttempts = 0;
            AdjustClient();
        }

        void AdjustClient()
        {
            if (PresentationSource.FromVisual(this) is not HwndSource source)
                return;
            var hwnd = source.Handle;
            GetWindowRect(hwnd, out var outer);
            GetClientRect(hwnd, out var client);
            var clientWidth = client.Right - client.Left;
            var clientHeight = client.Bottom - client.Top;
            if (Math.Abs(clientWidth - targetClientWidth) <= 1 && Math.Abs(clientHeight - targetClientHeight) <= 1)
                return;
            var chromeWidth = (outer.Right - outer.Left) - clientWidth;
            var chromeHeight = (outer.Bottom - outer.Top) - clientHeight;
            SetWindowPos(hwnd, IntPtr.Zero, 0, 0,
                Math.Max(1, targetClientWidth + chromeWidth), Math.Max(1, targetClientHeight + chromeHeight),
                SwpNoMove | SwpNoZOrder | SwpNoActivate);
            if (fitAttempts++ >= 4 || isUserResizing || SizeToContent != SizeToContent.Manual)
                return;
            Dispatcher.BeginInvoke(AdjustClient, System.Windows.Threading.DispatcherPriority.Loaded);
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

        [StructLayout(LayoutKind.Sequential)]
        struct Win32Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        const uint SwpNoMove = 0x0002;
        const uint SwpNoZOrder = 0x0004;
        const uint SwpNoActivate = 0x0010;

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hwnd, out Win32Rect rect);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetClientRect(IntPtr hwnd, out Win32Rect rect);

        [DllImport("user32")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32")]
        static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);

        sealed class Vst3ViewHost(Vst3EditorSession session) : HwndHost
        {
            const int WsChild = 0x40000000;
            const int WsVisible = 0x10000000;
            const int WsClipChildren = 0x02000000;

            int lastWidth;
            int lastHeight;

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
                var width = Math.Max(1, (int)rcBoundingBox.Width);
                var height = Math.Max(1, (int)rcBoundingBox.Height);
                if (width == lastWidth && height == lastHeight)
                    return;
                lastWidth = width;
                lastHeight = height;
                session.ResizeView(width, height);
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
