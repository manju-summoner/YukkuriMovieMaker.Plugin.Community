using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Windows.UI.Input.Inking;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenToolViewModel : Bindable, IDisposable
    {
        readonly DisposeCollector disposer = new();
        public PenMode PenMode { get => penMode; private set => Set(ref penMode, value); }
        PenMode penMode = PenMode.Pen;

        public DrawingAttributes Pen
        {
            get => PenMode switch
            {
                PenMode.Pen => CreatePen(),
                PenMode.Highlighter => CreateHighlighter(),
                PenMode.Eraser => CreateEraser(),
                PenMode.Select => new DrawingAttributes(),
                _ => throw new InvalidOperationException(),
            };
        }

        public InkCanvasEditingMode EditingMode { get => editingMode; set => Set(ref editingMode, value); }
        InkCanvasEditingMode editingMode = InkCanvasEditingMode.Ink;

        public Color StrokeColor
        {
            get => PenMode switch
            {
                PenMode.Pen => PenSettings.Default.PenStyle.StrokeColor,
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeColor,
                PenMode.Eraser => Colors.Transparent,
                PenMode.Select => Colors.Transparent,
                _ => throw new InvalidOperationException(),
            };
            set
            {
                switch (PenMode)
                {
                    case PenMode.Pen:
                        PenSettings.Default.PenStyle.StrokeColor = value;
                        OnPropertyChanged(nameof(Pen));
                        break;
                    case PenMode.Highlighter:
                        PenSettings.Default.HighlighterStyle.StrokeColor = value;
                        OnPropertyChanged(nameof(Pen));
                        break;
                    case PenMode.Eraser:
                    case PenMode.Select:
                        OnPropertyChanged(nameof(Pen));
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                OnPropertyChanged();
            }
        }

        public double StrokeThickness
        {
            get => PenMode switch
            {
                PenMode.Pen => PenSettings.Default.PenStyle.StrokeThickness,
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeThickness,
                PenMode.Eraser => PenSettings.Default.EraserStyle.StrokeThickness,
                PenMode.Select => 0,
                _ => throw new InvalidOperationException(),
            };
            set
            {
                switch (PenMode)
                {
                    case PenMode.Pen:
                        PenSettings.Default.PenStyle.StrokeThickness = value;
                        OnPropertyChanged(nameof(Pen));
                        break;
                    case PenMode.Highlighter:
                        PenSettings.Default.HighlighterStyle.StrokeThickness = value;
                        OnPropertyChanged(nameof(Pen));
                        break;
                    case PenMode.Eraser:
                        PenSettings.Default.EraserStyle.StrokeThickness = value;
                        OnPropertyChanged(nameof(Pen));
                        break;
                    case PenMode.Select:
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                OnPropertyChanged();
            }
        }

        public ICommand SelectPenCommand { get; }
        public ICommand SelectHighlighterCommand { get; }
        public ICommand SelectEraserCommand { get; }
        public ICommand SelectEraserByPointCommand { get; }
        public ICommand SelectEraserByStrokeCommand { get; }
        public ICommand EnableSelectionMode { get; }
        public ICommand MouseWheelCommand { get; }
        public ActionCommand UndoCommand { get; }
        public ActionCommand RedoCommand { get; }
        public ICommand SaveImageCommand { get; }
        public ICommand ExportIsfCommand { get; }
        public ICommand ImportIsfCommand { get; }
        public ICommand TogglePenPressure { get; }
        public ICommand ToggleHighlighterPressure { get; }

        public StrokeCollection Strokes { get; } = [];

        public bool Undoable => undoHistory.Count > 0;
        public bool Redoable => redoHistory.Count > 0;
        bool isUndoRedoing = false;
        const int undoredoCapacity = 100;
        readonly List<StrokeCollection> undoHistory = [];
        readonly List<StrokeCollection> redoHistory = [];
        StrokeCollection currentHistory = [];

        public BitmapSource Bitmap { get => bitmap; set => Set(ref bitmap, value); }
        BitmapSource bitmap;

        readonly IEditorInfo info;
        readonly ITimelineSourceAndDevices source;

        public PenToolViewModel(IEditorInfo info, IEnumerable<Stroke> strokes)
        {
            this.info = info;
            source = info.CreateTimelineVideoSource();
            disposer.Collect(source);

            SelectPenCommand = new ActionCommand(_=>true, _=> SelectPen());
            SelectHighlighterCommand = new ActionCommand(_=>true, _=> SelectHighlight());
            SelectEraserCommand = new ActionCommand(_=>true, _=> SelectEraser());
            EnableSelectionMode = new ActionCommand(_=>true, _=> SelectSelect());
            MouseWheelCommand = new ActionCommand(_=>true, x=> 
            {
                if(x is not MouseWheelEventArgs e)
                    return;
                if(e.Delta > 0)
                    StrokeThickness += 1;
                else
                    StrokeThickness -= 1;
            });
            UndoCommand = new ActionCommand(_ => Undoable, _ => 
            {
                isUndoRedoing = true;
                try
                {
                    var strokes = undoHistory.Last();
                    undoHistory.Remove(strokes);
                    Strokes.Clear();
                    foreach(var stroke in strokes)
                        Strokes.Add(stroke.Clone());
                    redoHistory.Add(currentHistory);
                    currentHistory = strokes;
                    if(redoHistory.Count > undoredoCapacity)
                        redoHistory.RemoveAt(0);
                }
                finally
                {
                   isUndoRedoing = false;
                }
                OnPropertyChanged(nameof(Undoable));
                OnPropertyChanged(nameof(Redoable));
                UndoCommand?.RaiseCanExecuteChanged();
                RedoCommand?.RaiseCanExecuteChanged();
            });
            RedoCommand = new ActionCommand(_ => Redoable, _ => 
            {
                isUndoRedoing = true;
                try
                {
                    var strokes = redoHistory.Last();
                    redoHistory.Remove(strokes);
                    Strokes.Clear();
                    foreach(var stroke in strokes)
                        Strokes.Add(stroke.Clone());
                    undoHistory.Add(currentHistory);
                    currentHistory = strokes;
                    if(undoHistory.Count > undoredoCapacity)
                        undoHistory.RemoveAt(0);
                }
                finally
                {
                   isUndoRedoing = false;
                }
                OnPropertyChanged(nameof(Undoable));
                OnPropertyChanged(nameof(Redoable));
                UndoCommand?.RaiseCanExecuteChanged();
                RedoCommand?.RaiseCanExecuteChanged();
            });
            SelectEraserByPointCommand = new ActionCommand(_=>true, _=>
            {
                PenSettings.Default.EraserStyle.Mode = EraserMode.Point;
                SelectEraser();
            });
            SelectEraserByStrokeCommand = new ActionCommand(_=>true, _=>
            {
                PenSettings.Default.EraserStyle.Mode = EraserMode.Line;
                SelectEraser();
            });
            SaveImageCommand = new ActionCommand(_=>true, _=>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG|*.png;",
                    DefaultExt = ".png",
                };
                if(dialog.ShowDialog() == true)
                {
                    var canvas = new InkCanvas
                    {
                        Background = Brushes.Transparent,
                        Strokes = Strokes,
                    };
                    var bitmap = new RenderTargetBitmap(info.VideoInfo.Width, info.VideoInfo.Height, 96, 96, PixelFormats.Pbgra32);
                    bitmap.Render(canvas);

                    using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    encoder.Save(stream);
                }
            });
            ExportIsfCommand = new ActionCommand(_=>true, _=>
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Ink Serialized Format|*.isf;",
                    DefaultExt = ".isf",
                };
                if(dialog.ShowDialog() == true)
                {
                    using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Create);
                    Strokes.Save(stream);
                }
            });
            ImportIsfCommand = new ActionCommand(_=>true, _=>
            {
                var dialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "Ink Serialized Format|*.isf;",
                    DefaultExt = ".isf",
                };
                if(dialog.ShowDialog() == true)
                {
                    using var stream = new System.IO.FileStream(dialog.FileName, System.IO.FileMode.Open);
                    isUndoRedoing = true;
                    try
                    {
                        Strokes.Clear();
                        Strokes.Add(new StrokeCollection(stream));
                    }
                    finally
                    {
                        isUndoRedoing = false;
                    }
                    undoHistory.Add(currentHistory);
                    if (undoHistory.Count > undoredoCapacity)
                        undoHistory.RemoveAt(0);
                    redoHistory.Clear();
                    currentHistory = new StrokeCollection(Strokes.Select(x => x.Clone()));
                    OnPropertyChanged(nameof(Undoable));
                    OnPropertyChanged(nameof(Redoable));
                    UndoCommand?.RaiseCanExecuteChanged();
                    RedoCommand?.RaiseCanExecuteChanged();
                }
            });
            TogglePenPressure = new ActionCommand(_=>true, _=>
            {
                PenSettings.Default.PenStyle.IsPressure = !PenSettings.Default.PenStyle.IsPressure;
                SelectPen();
            });
            ToggleHighlighterPressure = new ActionCommand(_=>true, _=>
            {
                PenSettings.Default.HighlighterStyle.IsPressure = !PenSettings.Default.HighlighterStyle.IsPressure;
                SelectPen();
            });

            foreach(var stroke in strokes)
                Strokes.Add(stroke.Clone());
            currentHistory = [.. strokes];
            
            Strokes.StrokesChanged += (_, _) => 
            {
                if (isUndoRedoing)
                    return;
                var strokes = new StrokeCollection(Strokes.Select(x => x.Clone()));
                undoHistory.Add(currentHistory);
                if (undoHistory.Count > undoredoCapacity)
                    undoHistory.RemoveAt(0);
                redoHistory.Clear();
                currentHistory = strokes;
                OnPropertyChanged(nameof(Undoable));
                OnPropertyChanged(nameof(Redoable));
                UndoCommand?.RaiseCanExecuteChanged();
                RedoCommand?.RaiseCanExecuteChanged();
            };

            Render();
        }

        [MemberNotNull(nameof(bitmap))]
        void Render()
        {
            TimeSpan time = info.ItemPosition.Time < TimeSpan.Zero
                ? info.ItemPosition.Time
                : info.ItemDuration.Time < info.ItemPosition.Time
                ? info.VideoInfo.GetTimeFrom(info.ItemPosition.Frame + info.ItemDuration.Frame - 1)
                : info.TimelinePosition.Time;
            source.Update(time, Player.Video.TimelineSourceUsage.Paused);
            bitmap = Bitmap = source.RenderBitmapSource();
        }

        void SelectPen()
        {
            PenMode = PenMode.Pen;
            EditingMode = InkCanvasEditingMode.Ink;
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(Pen));
        }

        void SelectHighlight()
        {
            PenMode = PenMode.Highlighter;
            EditingMode = InkCanvasEditingMode.Ink;
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(Pen));
        }

        void SelectEraser()
        {
            PenMode = PenMode.Eraser;
            EditingMode = PenSettings.Default.EraserStyle.Mode is EraserMode.Line ? InkCanvasEditingMode.EraseByStroke : InkCanvasEditingMode.EraseByPoint;
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(Pen));
        }

        void SelectSelect()
        {
            PenMode = PenMode.Select;
            EditingMode = InkCanvasEditingMode.Select;
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(Pen));
        }

        static DrawingAttributes CreatePen()
        {
            var size = PenSettings.Default.PenStyle.StrokeThickness;
            var color = PenSettings.Default.PenStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size,
                Height = size,
                IsHighlighter = false,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.PenStyle.IsPressure,
                StylusTip = StylusTip.Ellipse,
            };
        }

        static DrawingAttributes CreateHighlighter()
        {
            var size = PenSettings.Default.HighlighterStyle.StrokeThickness;
            var color = PenSettings.Default.HighlighterStyle.StrokeColor;
            return new DrawingAttributes
            {
                Color = color,
                Width = size / 2,
                Height = size,
                IsHighlighter = true,
                FitToCurve = true,
                IgnorePressure = !PenSettings.Default.HighlighterStyle.IsPressure,
                StylusTip = StylusTip.Rectangle,
            };
        }

        static DrawingAttributes CreateEraser()
        {
            var size = PenSettings.Default.EraserStyle.StrokeThickness;
            var color = Colors.Transparent;
            return new DrawingAttributes
            {
                Color = color,
                Width = size,
                Height = size,
                IsHighlighter = false,
                FitToCurve = true,
                IgnorePressure = false,
                StylusTip = StylusTip.Rectangle,
            };
        }

        #region IDisposable Support
        private bool disposedValue;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // マネージド状態を破棄します (マネージド オブジェクト)
                    disposer.Dispose();
                }

                // アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // // 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~PenToolViewModel()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
