using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    /// <summary>
    /// VST3プラグインのエディタビューをHwndHostで表示するウィンドウ。
    /// IPlugViewのサイズは物理ピクセルなので、ウィンドウサイズも物理ピクセル基準
    /// （AdjustWindowRectExForDpi＋SetWindowPos）で管理する。
    /// 高DPI対応（IPlugViewContentScaleSupport）のプラグインにはスケールを通知して
    /// ネイティブ解像度で描画させる。非対応のプラグインは等倍（物理ピクセル）表示になる
    /// （プラグイン内蔵のズーム機能があればresizeView経由でウィンドウが追従する）。
    /// 表示中は定期的に無音プロセスを回し、GUIでの編集をプラグインの状態へ反映する。
    /// </summary>
    internal sealed class Vst3EditorWindow : Window
    {
        readonly Vst3Plugin plugin;
        readonly Vst3View view;
        readonly Vst3ViewHost host;
        readonly Vst3EditorParameterForwarder parameterForwarder;
        readonly Vst3EditorMeterForwarder meterForwarder;
        readonly Vst3EditorAudioFeeder? audioFeeder;
        readonly Action<uint, double, long> fedMeterForward;
        readonly DispatcherTimer pumpTimer;
        readonly bool isContentScaleSupported;

        /// <summary>
        /// スケール適用前のビューの基準サイズ
        /// </summary>
        readonly (int Width, int Height) baseSize;

        /// <summary>
        /// エディター操作の定期転送後に発生する。引数は転送したパラメーター変更の件数
        /// （Vst3EditorSessionが編集の区切り検出に使用する）
        /// </summary>
        public event Action<int>? ParameterForwarded;

        public Vst3EditorWindow(
            Vst3Plugin plugin,
            Vst3View view,
            Vst3EditorParameterForwarder parameterForwarder,
            Vst3EditorMeterForwarder meterForwarder,
            Vst3EditorAudioFeeder? audioFeeder = null)
        {
            this.plugin = plugin;
            this.view = view;
            this.parameterForwarder = parameterForwarder;
            this.meterForwarder = meterForwarder;
            this.audioFeeder = audioFeeder;
            fedMeterForward = (paramId, normalizedValue, _) => this.plugin.SetControllerParameter(paramId, normalizedValue);
            isContentScaleSupported = view.IsContentScaleSupported;
            baseSize = view.GetSize();

            // 派生型は暗黙スタイルの対象にならないため、テーマのWindowスタイル
            // （Loaded時のタイトルバー配色適用）と背景を明示的に適用する
            SetResourceReference(StyleProperty, typeof(Window));
            SetResourceReference(BackgroundProperty, SystemColors.ControlBrushKey);

            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = view.CanResize ? ResizeMode.CanResize : ResizeMode.NoResize;
            ShowInTaskbar = false;
            UseLayoutRounding = true;

            host = new Vst3ViewHost(view, OnPluginResizeRequested);
            host.ViewAttached += OnViewAttached;
            Content = host;

            pumpTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(33) };
            pumpTimer.Tick += (_, _) =>
            {
                if (plugin.IsDisposed)
                    return;
                // 再生・シークで位置が動いた場合は実音声を処理させ、波形等のGUI表示を追従させる
                var fed = this.audioFeeder?.Feed(plugin) == true;
                var parameterCount = parameterForwarder.PumpAndForward(plugin, pump: !fed);
                if (fed)
                {
                    // 実音声を処理した直後の出力パラメーター（メーター等）をそのままGUIへ反映する
                    plugin.DrainMeterParameterChanges(fedMeterForward);
                }
                else
                {
                    meterForwarder.Apply(plugin);
                }
                ParameterForwarded?.Invoke(parameterCount);
            };

            SourceInitialized += OnSourceInitialized;
            Loaded += (_, _) => pumpTimer.Start();
        }

        void OnSourceInitialized(object? sender, EventArgs e)
        {
            // スケール通知前の暫定サイズ。スケール非対応プラグインは等倍のまま表示する
            var scale = isContentScaleSupported ? VisualTreeHelper.GetDpi(this).DpiScaleX : 1.0;
            SetClientPixelSize(
                (int)Math.Round(baseSize.Width * scale),
                (int)Math.Round(baseSize.Height * scale));
        }

        void OnViewAttached()
        {
            // アタッチ完了後（レイアウト中）なので、サイズ確定はディスパッチャ経由で行う
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (view.IsDisposed)
                    return;
                ApplyContentScale(VisualTreeHelper.GetDpi(this).DpiScaleX);
            });
        }

        protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
        {
            base.OnDpiChanged(oldDpi, newDpi);
            // スケール非対応プラグインは物理ピクセル固定なのでDPI変更に反応する必要がない
            if (view.IsDisposed || !isContentScaleSupported)
                return;
            ApplyContentScale(newDpi.DpiScaleX);
        }

        /// <summary>
        /// DPIスケールをビューへ反映し、ウィンドウサイズを合わせる
        /// </summary>
        void ApplyContentScale(double scale)
        {
            int width;
            int height;
            if (isContentScaleSupported)
            {
                view.SetContentScale(scale);
                (width, height) = view.GetSize();
            }
            else
            {
                // 暫定ウィンドウに対するレイアウト経由のonSizeでビューのサイズが
                // 変わっていることがあるため、基準サイズへ確定させる（冪等）
                (width, height) = view.OnSize(baseSize.Width, baseSize.Height);
            }
            SetClientPixelSize(width, height);
        }

        /// <summary>
        /// プラグイン都合のリサイズ要求（IPlugFrame::resizeView。ビュー座標＝ウィンドウのピクセル座標）
        /// </summary>
        void OnPluginResizeRequested(int width, int height)
        {
            SetClientPixelSize(width, height);
        }

        /// <summary>
        /// クライアント領域が指定の物理ピクセルサイズになるようウィンドウをリサイズする
        /// </summary>
        void SetClientPixelSize(int width, int height)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero || width <= 0 || height <= 0)
                return;
            var style = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_STYLE);
            var exStyle = NativeMethods.GetWindowLongW(hwnd, NativeMethods.GWL_EXSTYLE);
            var dpi = (uint)Math.Round(96 * VisualTreeHelper.GetDpi(this).DpiScaleX);
            var rect = new NativeMethods.RECT { Left = 0, Top = 0, Right = width, Bottom = height };
            NativeMethods.AdjustWindowRectExForDpi(ref rect, style, false, exStyle, dpi);
            NativeMethods.SetWindowPos(
                hwnd, IntPtr.Zero, 0, 0,
                rect.Right - rect.Left, rect.Bottom - rect.Top,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (e.Cancel)
                return;
            pumpTimer.Stop();
            // 最後の編集をプロセッサ状態へ反映してからビューを外す
            if (!plugin.IsDisposed)
            {
                parameterForwarder.PumpAndForward(plugin);
                meterForwarder.Apply(plugin);
            }
            view.Dispose();
        }

        protected override void OnClosed(EventArgs e)
        {
            // オーナーウィンドウの破棄経由で閉じられた場合はOnClosingを経由しないため、ここでも停止・保存・破棄する。
            // また、Closedイベントのハンドラー（Vst3EditorSessionのプラグイン破棄）より先にビューと子ウィンドウを
            // 破棄しないと、アンロード済みモジュールのビュー破棄処理を呼んでクラッシュする
            pumpTimer.Stop();
            if (!plugin.IsDisposed && !view.IsDisposed)
                parameterForwarder.PumpAndForward(plugin);
            view.Dispose();
            audioFeeder?.Dispose();
            meterForwarder.Dispose();
            host.Dispose();
            base.OnClosed(e);
        }

        static class NativeMethods
        {
            public const int GWL_STYLE = -16;
            public const int GWL_EXSTYLE = -20;
            public const uint SWP_NOMOVE = 0x0002;
            public const uint SWP_NOZORDER = 0x0004;
            public const uint SWP_NOACTIVATE = 0x0010;

            [StructLayout(LayoutKind.Sequential)]
            public struct RECT
            {
                public int Left;
                public int Top;
                public int Right;
                public int Bottom;
            }

            [DllImport("user32.dll")]
            public static extern int GetWindowLongW(IntPtr hwnd, int index);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool AdjustWindowRectExForDpi(ref RECT rect, int style, [MarshalAs(UnmanagedType.Bool)] bool menu, int exStyle, uint dpi);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int width, int height, uint flags);

        }
    }

    /// <summary>
    /// IPlugViewをアタッチする子ウィンドウを提供するHwndHost
    /// </summary>
    internal sealed class Vst3ViewHost(Vst3View view, Action<int, int> onPluginResizeRequested) : HwndHost
    {
        int lastWidth = -1;
        int lastHeight = -1;

        /// <summary>
        /// ビューが子ウィンドウへアタッチされた直後に発生する
        /// </summary>
        public event Action? ViewAttached;

        protected override HandleRef BuildWindowCore(HandleRef hwndParent)
        {
            var (width, height) = view.GetSize();
            lastWidth = width;
            lastHeight = height;

            var hwnd = NativeMethods.CreateWindowExW(
                0, "STATIC", string.Empty,
                NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE | NativeMethods.WS_CLIPCHILDREN,
                0, 0, Math.Max(width, 1), Math.Max(height, 1),
                hwndParent.Handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (hwnd == IntPtr.Zero)
                throw new InvalidOperationException("VST3エディター用の子ウィンドウを作成できませんでした。");
            if (!view.IsDisposed)
            {
                view.Attach(hwnd, (w, h) =>
                {
                    lastWidth = w;
                    lastHeight = h;
                    onPluginResizeRequested(w, h);
                });
                ViewAttached?.Invoke();
            }
            return new HandleRef(this, hwnd);
        }

        protected override void DestroyWindowCore(HandleRef hwnd)
        {
            // ビューがアタッチされたままHWNDを壊さないよう、先にremovedを呼ぶ
            if (!view.IsDisposed)
                view.Dispose();
            NativeMethods.DestroyWindow(hwnd.Handle);
        }

        protected override void OnWindowPositionChanged(Rect rcBoundingBox)
        {
            base.OnWindowPositionChanged(rcBoundingBox);
            if (view.IsDisposed)
                return;
            // rcBoundingBoxはウィンドウのピクセル座標（DPI非対応ウィンドウでは仮想化された96dpi座標）
            var width = (int)Math.Round(rcBoundingBox.Width);
            var height = (int)Math.Round(rcBoundingBox.Height);
            if (width <= 0 || height <= 0 || (width == lastWidth && height == lastHeight))
                return;
            // アスペクト比固定等のプラグインは要求と異なるサイズを返すため、確定値でウィンドウを追従させる
            var (appliedWidth, appliedHeight) = view.OnSize(width, height);
            lastWidth = appliedWidth;
            lastHeight = appliedHeight;
            if (appliedWidth != width || appliedHeight != height)
                onPluginResizeRequested(appliedWidth, appliedHeight);
        }

        static class NativeMethods
        {
            public const int WS_CHILD = 0x40000000;
            public const int WS_VISIBLE = 0x10000000;
            public const int WS_CLIPCHILDREN = 0x02000000;

            [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr CreateWindowExW(
                int exStyle, string className, string windowName, int style,
                int x, int y, int width, int height,
                IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool DestroyWindow(IntPtr hwnd);
        }
    }
}
