using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using FrameTime = YukkuriMovieMaker.Player.Video.FrameTime;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Views;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.ViewModels
{
    internal class PenEditorViewModel : Bindable, IDisposable
    {
        readonly DisposeCollector disposer = new();
        readonly IEditorInfo info;
        readonly ITimelineSourceAndDevices source;
        readonly PenShapeParameter document = new();
        readonly PenShapeSource documentSource;
        readonly PenPreviewRenderer previewRenderer;
        readonly PenPreviewRenderer fillRenderer;
        readonly PenThumbnailRenderer thumbnailRenderer;
        readonly TimelineItemSourceDescription documentDescription;
        readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

        const int ThumbnailWidth = 44;
        const int ThumbnailHeight = 26;
        const int DirtyMargin = 2;
        const string ClipboardFormat = "YukkuriMovieMaker.Plugin.Community.Shape.Pen.Strokes";

        static readonly Color EraserWetInkColor = Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF);

        readonly PenFillEngine fillEngine = new();

        readonly PenHistory history = new();
        readonly PenLayerVisibility visibility = new();

        ImmutableList<int> selectionIndices = [];
        ImmutableList<SerializableStroke>? transformSource;
        PenLayer? transformLayer;
        readonly List<PenOrderEntry> orderEntries = [];
        PenStrokeGeometry? wetStroke;
        StylusPointCollection? wetPoints;
        int wetPointCount;
        bool isWetDirty;

        int layerNumber;
        int folderNumber;
        bool isRenderQueued;
        bool isDocumentDirty;
        bool isOrderDirty;
        bool isDisposed;
        double viewZoom = 1;
        double viewDpiScale = 1;
        Point viewOrigin;
        Size viewSize;
        bool isRestoring;

        public IEditorInfo EditorInfo => info;

        public double CanvasWidth { get; }

        public double CanvasHeight { get; }

        public BitmapSource BackgroundImage { get => backgroundImage; private set => Set(ref backgroundImage, value); }
        BitmapSource backgroundImage;

        public BitmapSource? DocumentImage { get => documentImage; private set => Set(ref documentImage, value); }
        BitmapSource? documentImage;

        public ImmutableList<PenLayer> Layers => document.Layers;

        public ImmutableList<PenLayer> DisplayLayers { get => displayLayers; private set => Set(ref displayLayers, value); }
        ImmutableList<PenLayer> displayLayers = [];

        public PenLayer? ActiveLayer
        {
            get => activeLayer;
            set
            {
                if (!Set(ref activeLayer, value))
                    return;
                ClearSelection();
                UpdateCommands();
                OnPropertyChanged(nameof(IsLayerEditable));
                OnPropertyChanged(nameof(IsRangeSupported));
            }
        }
        PenLayer? activeLayer;

        public Color WetInkColor { get => wetInkColor; private set => Set(ref wetInkColor, value); }
        Color wetInkColor = PenSettings.Default.PenStyle.StrokeColor;

        public double WetInkThickness { get => wetInkThickness; private set => Set(ref wetInkThickness, value); }
        double wetInkThickness = PenSettings.Default.PenStyle.StrokeThickness;

        public bool WetInkUsesPressure { get => wetInkUsesPressure; private set => Set(ref wetInkUsesPressure, value); }
        bool wetInkUsesPressure = true;

        public double StabilizationStrength { get => stabilizationStrength; private set => Set(ref stabilizationStrength, value); }
        double stabilizationStrength;

        public bool IgnoresPressure { get => ignoresPressure; private set => Set(ref ignoresPressure, value); }
        bool ignoresPressure;

        public double TaperLength { get => taperLength; private set => Set(ref taperLength, value); }
        double taperLength;

        public PenMode Mode { get => mode; private set => Set(ref mode, value); }
        PenMode mode = PenSettings.Default.PenMode is PenMode.Select or PenMode.Order ? PenMode.Pen : PenSettings.Default.PenMode;

        public bool IsWetInkOverlay => ActiveTool is PenMode.Eraser;

        PenMode ActiveTool => isPenInverted ? PenMode.Eraser : mode;
        bool isPenInverted;

        public PenOrderBadge[] OrderBadges { get => orderBadges; private set => Set(ref orderBadges, value); }
        PenOrderBadge[] orderBadges = [];

        public double PreviewLength
        {
            get => previewLength;
            set
            {
                if (!Set(ref previewLength, Math.Clamp(value, 0, 100)))
                    return;
                ApplyPreviewLength();
            }
        }
        double previewLength = 100;

        public Color StrokeColor
        {
            get => mode switch
            {
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeColor,
                PenMode.Pencil => PenSettings.Default.PencilStyle.StrokeColor,
                PenMode.Fill => PenSettings.Default.FillStyle.StrokeColor,
                PenMode.Eraser => Colors.Transparent,
                _ => PenSettings.Default.PenStyle.StrokeColor,
            };
            set
            {
                switch (mode)
                {
                    case PenMode.Highlighter:
                        PenSettings.Default.HighlighterStyle.StrokeColor = value;
                        break;
                    case PenMode.Pencil:
                        PenSettings.Default.PencilStyle.StrokeColor = value;
                        break;
                    case PenMode.Fill:
                        PenSettings.Default.FillStyle.StrokeColor = value;
                        break;
                    case PenMode.Eraser:
                        return;
                    default:
                        PenSettings.Default.PenStyle.StrokeColor = value;
                        break;
                }
                RefreshTool();
            }
        }

        public double StrokeThickness
        {
            get => GetStrokeThickness(mode);
            set
            {
                switch (mode)
                {
                    case PenMode.Highlighter:
                        PenSettings.Default.HighlighterStyle.StrokeThickness = value;
                        break;
                    case PenMode.Pencil:
                        PenSettings.Default.PencilStyle.StrokeThickness = value;
                        break;
                    case PenMode.Eraser:
                        PenSettings.Default.EraserStyle.StrokeThickness = value;
                        break;
                    default:
                        PenSettings.Default.PenStyle.StrokeThickness = value;
                        break;
                }
                RefreshTool();
            }
        }

        public ActionCommand ImportIsfCommand { get; }

        public ActionCommand ExportIsfCommand { get; }

        public ActionCommand SaveImageCommand { get; }

        public ActionCommand UndoCommand { get; }

        public ActionCommand RedoCommand { get; }

        public ActionCommand SelectPenCommand { get; }

        public ActionCommand SelectHighlighterCommand { get; }

        public ActionCommand SelectPencilCommand { get; }

        public ActionCommand SelectEraserCommand { get; }

        public ActionCommand SelectSelectionCommand { get; }

        public ActionCommand SelectFillCommand { get; }

        public ActionCommand SelectOrderCommand { get; }

        public ActionCommand SelectByLassoCommand { get; }

        public ActionCommand SelectByRectangleCommand { get; }

        public ActionCommand DeleteSelectionCommand { get; }

        public ActionCommand ClearSelectionCommand { get; }

        public ActionCommand SelectAllCommand { get; }

        public ActionCommand DuplicateSelectionCommand { get; }

        public ActionCommand SelectionToNewLayerCommand { get; }

        public ActionCommand ApplySelectionColorCommand { get; }

        public ActionCommand ApplySelectionThicknessCommand { get; }

        public ActionCommand FlipSelectionHorizontalCommand { get; }

        public ActionCommand FlipSelectionVerticalCommand { get; }

        public ActionCommand CutSelectionCommand { get; }

        public ActionCommand CopySelectionCommand { get; }

        public ActionCommand PasteCommand { get; }

        public bool IsLayerEditable => activeLayer is { IsFolder: false } layer && !PenLayerTree.IsLocked(document.Layers, layer) && IsLayerVisible(layer);

        public bool IsRangeSupported => activeLayer is { IsFolder: false };

        public bool IsSelectionMode => mode is PenMode.Select;

        public bool IsFillMode => mode is PenMode.Fill;

        public bool IsOrderMode => mode is PenMode.Order;

        public bool IsRectangleSelection => PenSettings.Default.SelectionKind is PenSelectionKind.Rectangle;

        public Rect SelectionBounds { get => selectionBounds; private set => Set(ref selectionBounds, value); }
        Rect selectionBounds = Rect.Empty;

        public ActionCommand SelectEraserByPointCommand { get; }

        public ActionCommand SelectEraserByStrokeCommand { get; }

        public ActionCommand TogglePenPressure { get; }

        public ActionCommand ToggleHighlighterPressure { get; }

        public ActionCommand TogglePencilPressure { get; }

        public ActionCommand SetPenStabilizationCommand { get; }

        public ActionCommand SetHighlighterStabilizationCommand { get; }

        public ActionCommand SetPencilStabilizationCommand { get; }

        public ActionCommand SetPenTaperCommand { get; }

        public ActionCommand SetHighlighterTaperCommand { get; }

        public ActionCommand SetPencilTaperCommand { get; }

        public ActionCommand SetFillToleranceCommand { get; }

        public ActionCommand AddLayerCommand { get; }

        public ActionCommand AddFolderCommand { get; }

        public ActionCommand MoveIntoFolderCommand { get; }

        public ActionCommand MoveOutOfFolderCommand { get; }

        public ActionCommand ToggleExpandCommand { get; }

        public ActionCommand DuplicateLayerCommand { get; }

        public ActionCommand DeleteLayerCommand { get; }

        public ActionCommand MergeLayerCommand { get; }

        public ActionCommand MoveLayerUpCommand { get; }

        public ActionCommand MoveLayerDownCommand { get; }

        public PenEditorViewModel(IEditorInfo info, ImmutableList<PenLayer> layers, ImmutableList<SerializableStroke> strokes)
        {
            this.info = info;
            source = info.CreateTimelineVideoSource();
            disposer.Collect(source);

            CanvasWidth = info.VideoInfo.Width;
            CanvasHeight = info.VideoInfo.Height;

            previewRenderer = new PenPreviewRenderer(source.Devices);
            disposer.Collect(previewRenderer);
            fillRenderer = new PenPreviewRenderer(source.Devices);
            disposer.Collect(fillRenderer);
            thumbnailRenderer = new PenThumbnailRenderer(source.Devices);
            disposer.Collect(thumbnailRenderer);
            documentSource = new PenShapeSource(source.Devices, document);
            disposer.Collect(documentSource);

            var fps = info.VideoInfo.FPS;
            var screenSize = new System.Drawing.Size(info.VideoInfo.Width, info.VideoInfo.Height);
            var timelineDescription = new TimelineSourceDescription(
                screenSize,
                new FrameTime(0, fps),
                new FrameTime(1, fps),
                fps,
                TimelineSourceUsage.Paused,
                Guid.Empty,
                []);
            documentDescription = new TimelineItemSourceDescription(timelineDescription, 0, 1, 0);

            ImportIsfCommand = new ActionCommand(_ => true, _ => ImportIsf());
            ExportIsfCommand = new ActionCommand(_ => true, _ => ExportIsf());
            SaveImageCommand = new ActionCommand(_ => true, _ => SaveImage());

            UndoCommand = new ActionCommand(_ => history.CanUndo, _ => Undo());
            RedoCommand = new ActionCommand(_ => history.CanRedo, _ => Redo());

            SelectPenCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Pen));
            SelectHighlighterCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Highlighter));
            SelectPencilCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Pencil));
            SelectEraserCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Eraser));
            SelectSelectionCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Select));
            SelectFillCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Fill));
            SelectOrderCommand = new ActionCommand(_ => true, _ => SelectMode(PenMode.Order));
            SelectByLassoCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.SelectionKind = PenSelectionKind.Lasso;
                SelectMode(PenMode.Select);
            });
            SelectByRectangleCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.SelectionKind = PenSelectionKind.Rectangle;
                SelectMode(PenMode.Select);
            });
            DeleteSelectionCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => DeleteSelection());
            ClearSelectionCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => ClearSelection());
            SelectAllCommand = new ActionCommand(_ => !history.IsEditing && IsLayerEditable, _ => SelectAll());
            DuplicateSelectionCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => DuplicateSelection());
            SelectionToNewLayerCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => MoveSelectionToNewLayer());
            ApplySelectionColorCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => ApplySelectionColor());
            ApplySelectionThicknessCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => ApplySelectionThickness());
            FlipSelectionHorizontalCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => FlipSelection(true));
            FlipSelectionVerticalCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => FlipSelection(false));
            CutSelectionCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => CutSelection());
            CopySelectionCommand = new ActionCommand(_ => !history.IsEditing && !selectionIndices.IsEmpty, _ => CopySelection());
            PasteCommand = new ActionCommand(_ => !history.IsEditing && IsLayerEditable && HasClipboardStrokes(), _ => Paste());
            SelectEraserByPointCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.EraserStyle.Mode = EraserMode.Point;
                SelectMode(PenMode.Eraser);
            });
            SelectEraserByStrokeCommand = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.EraserStyle.Mode = EraserMode.Line;
                SelectMode(PenMode.Eraser);
            });
            TogglePenPressure = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.PenStyle.IsPressure = !PenSettings.Default.PenStyle.IsPressure;
                SelectMode(PenMode.Pen);
            });
            ToggleHighlighterPressure = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.HighlighterStyle.IsPressure = !PenSettings.Default.HighlighterStyle.IsPressure;
                SelectMode(PenMode.Highlighter);
            });
            TogglePencilPressure = new ActionCommand(_ => true, _ =>
            {
                PenSettings.Default.PencilStyle.IsPressure = !PenSettings.Default.PencilStyle.IsPressure;
                SelectMode(PenMode.Pencil);
            });
            SetPenStabilizationCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenStabilization value)
                    return;
                PenSettings.Default.PenStyle.Stabilization = value;
                SelectMode(PenMode.Pen);
            });
            SetHighlighterStabilizationCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenStabilization value)
                    return;
                PenSettings.Default.HighlighterStyle.Stabilization = value;
                SelectMode(PenMode.Highlighter);
            });
            SetPencilStabilizationCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenStabilization value)
                    return;
                PenSettings.Default.PencilStyle.Stabilization = value;
                SelectMode(PenMode.Pencil);
            });
            SetPenTaperCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenTaper value)
                    return;
                PenSettings.Default.PenStyle.Taper = value;
                SelectMode(PenMode.Pen);
            });
            SetHighlighterTaperCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenTaper value)
                    return;
                PenSettings.Default.HighlighterStyle.Taper = value;
                SelectMode(PenMode.Highlighter);
            });
            SetPencilTaperCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenTaper value)
                    return;
                PenSettings.Default.PencilStyle.Taper = value;
                SelectMode(PenMode.Pencil);
            });
            SetFillToleranceCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenFillTolerance value)
                    return;
                PenSettings.Default.FillStyle.Tolerance = value;
                SelectMode(PenMode.Fill);
            });

            AddLayerCommand = new ActionCommand(_ => true, _ => AddLayer());
            AddFolderCommand = new ActionCommand(_ => true, _ => AddFolder());
            MoveIntoFolderCommand = new ActionCommand(_ => PenLayerTree.FindTargetFolder(document.Layers, activeLayer) is not null, _ => MoveIntoFolder());
            MoveOutOfFolderCommand = new ActionCommand(_ => activeLayer is { } layer && layer.ParentId != Guid.Empty, _ => MoveOutOfFolder());
            ToggleExpandCommand = new ActionCommand(_ => true, x =>
            {
                if (x is not PenLayer layer || !layer.IsFolder)
                    return;

                layer.IsExpanded = !layer.IsExpanded;
                if (activeLayer is not null && PenLayerTree.IsCollapsed(document.Layers, activeLayer))
                    ActiveLayer = layer;
                UpdateDisplayLayers(document.Layers);
            });
            DuplicateLayerCommand = new ActionCommand(_ => activeLayer is not null, _ => DuplicateLayer());
            DeleteLayerCommand = new ActionCommand(_ => CanDeleteLayer(), _ => DeleteLayer());
            MergeLayerCommand = new ActionCommand(_ => PenLayerTree.FindMergeTarget(document.Layers, activeLayer) is not null, _ => MergeLayer());
            MoveLayerUpCommand = new ActionCommand(_ => CanMoveLayer(1), _ => MoveLayer(1));
            MoveLayerDownCommand = new ActionCommand(_ => CanMoveLayer(-1), _ => MoveLayer(-1));

            backgroundImage = BackgroundImage = RenderBackground();
            SetLayers(CreateInitialLayers(layers, strokes), null);
            RefreshTool();
            history.Reset(document.Layers);
            document.UndoRedoCommandCreated += OnDocumentChanged;
            UpdateDocumentImage(true);
        }

        public ImmutableList<SerializableStroke> CreateStrokeMirror()
        {
            var layers = document.Layers;
            visibility.Update(layers);
            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            for (var i = 0; i < layers.Count; i++)
            {
                if (visibility[i])
                    builder.AddRange(layers[i].Strokes);
            }
            return builder.ToImmutable();
        }

        bool IsLayerVisible(PenLayer layer)
        {
            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            if (index < 0)
                return false;

            visibility.Update(layers);
            return visibility[index];
        }

        void ImportIsf()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Ink Serialized Format|*.isf;",
                DefaultExt = ".isf",
            };
            if (dialog.ShowDialog() != true)
                return;

            ImportIsf(dialog.FileName);
        }

        void ImportIsf(string path)
        {
            if (!PenToolFile.TryLoadStrokes(path, Texts.LoadFileFailed, out var imported, out var errorMessage))
            {
                MessageBox.Show(errorMessage, Texts.PenToolWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            if (imported.Count == 0)
                return;

            var builder = ImmutableList.CreateBuilder<SerializableStroke>();
            foreach (var stroke in imported)
                builder.Add(PenStrokes.FromIsfStroke(stroke));

            var layer = new PenLayer { Name = CreateLayerName(), Strokes = builder.ToImmutable(), ParentId = activeLayer?.ParentId ?? Guid.Empty };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(PenLayerTree.Normalize(layers.Insert(index, layer)), layer);
        }

        void ExportIsf()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Ink Serialized Format|*.isf;",
                DefaultExt = ".isf",
            };
            if (dialog.ShowDialog() != true)
                return;

            ExportIsf(dialog.FileName);
        }

        void ExportIsf(string path)
        {
            var strokes = new StrokeCollection();
            foreach (var serializable in CreateStrokeMirror())
                strokes.Add(PenStrokes.ToIsfStroke(serializable));

            if (!PenToolFile.TryOpen(path, FileMode.Create, FileAccess.Write, Texts.SaveFileFailed, strokes.Save, out var errorMessage))
                MessageBox.Show(errorMessage, Texts.PenToolWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        void SaveImage()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "PNG|*.png;",
                DefaultExt = ".png",
            };
            if (dialog.ShowDialog() != true)
                return;

            var copy = new WriteableBitmap(RenderDocument());
            copy.Freeze();

            if (!PenToolFile.TryOpen(dialog.FileName, FileMode.Create, FileAccess.Write, Texts.SaveFileFailed, stream =>
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(copy));
                    encoder.Save(stream);
                }, out var errorMessage))
            {
                MessageBox.Show(errorMessage, Texts.PenToolWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        ImmutableList<PenLayer> CreateInitialLayers(ImmutableList<PenLayer> layers, ImmutableList<SerializableStroke> strokes)
        {
            if (!layers.IsEmpty)
            {
                layerNumber = layers.Count;
                folderNumber = layers.Count;
                var builder = ImmutableList.CreateBuilder<PenLayer>();
                foreach (var layer in layers)
                    builder.Add(layer.Clone(layer.Id));
                return PenLayerTree.Normalize(builder.ToImmutable());
            }

            return [new PenLayer { Name = CreateLayerName(), Strokes = strokes }];
        }

        public void BeginEditUnit()
        {
            if (history.BeginEdit())
                UpdateGestureCommands();
        }

        public void EndEditUnit()
        {
            if (!history.EndEdit())
                return;

            CommitSnapshot();
            UpdateGestureCommands();
        }

        void UpdateGestureCommands()
        {
            UpdateHistoryCommands();
            UpdateSelectionCommands();
        }

        void Undo()
        {
            if (history.Undo() is { } snapshot)
                Restore(snapshot);
        }

        void Redo()
        {
            if (history.Redo() is { } snapshot)
                Restore(snapshot);
        }

        void Restore(ImmutableList<PenLayer> snapshot)
        {
            var builder = ImmutableList.CreateBuilder<PenLayer>();
            foreach (var layer in snapshot)
                builder.Add(layer.Clone(layer.Id));
            var layers = PenLayerTree.Normalize(builder.ToImmutable());

            var activeId = activeLayer?.Id;
            var restoredActive = layers.Count == 0
                ? null
                : layers.FirstOrDefault(x => x.Id == activeId) ?? layers[^1];

            isRestoring = true;
            try
            {
                document.Layers = layers;
            }
            finally
            {
                isRestoring = false;
            }

            if (restoredActive is not null)
                PenLayerTree.ExpandAncestors(layers, restoredActive);
            UpdateDisplayLayers(layers);
            ActiveLayer = restoredActive;
            OnPropertyChanged(nameof(Layers));
            history.ClearDirty();
            UpdateCommands();
            UpdateHistoryCommands();
            InvalidateDocument();
        }

        void CommitSnapshot()
        {
            if (history.Commit(document.Layers))
                UpdateHistoryCommands();
        }

        void UpdateHistoryCommands()
        {
            UndoCommand.RaiseCanExecuteChanged();
            RedoCommand.RaiseCanExecuteChanged();
        }

        void SelectMode(PenMode value)
        {
            var wasOrderMode = IsOrderMode;
            PenSettings.Default.PenMode = value;
            Mode = value;
            ClearSelection();
            RefreshTool();

            if (wasOrderMode == IsOrderMode)
                return;
            if (IsOrderMode)
            {
                previewLength = 100;
                OnPropertyChanged(nameof(PreviewLength));
            }
            else
            {
                OrderBadges = [];
            }
            ApplyPreviewLength();
        }

        void ApplyPreviewLength()
        {
            SetDocumentLength(IsOrderMode ? previewLength : 100);
            isOrderDirty = true;
            QueueRender();
        }

        void SetDocumentLength(double value)
        {
            isRestoring = true;
            try
            {
                document.Length.Values[0].Value = value;
            }
            finally
            {
                isRestoring = false;
            }
        }

        public void PressOrderBadge(int layerIndex, int strokeIndex, bool isToggle)
        {
            var layers = document.Layers;
            if (layerIndex < 0 || layerIndex >= layers.Count)
                return;

            var layer = layers[layerIndex];
            if (strokeIndex < 0 || strokeIndex >= layer.Strokes.Count)
                return;

            if (!ReferenceEquals(layer, activeLayer))
            {
                if (PenLayerTree.IsCollapsed(layers, layer))
                {
                    PenLayerTree.ExpandAncestors(layers, layer);
                    UpdateDisplayLayers(layers);
                }
                ActiveLayer = layer;
                isToggle = false;
            }

            var indices = !isToggle
                ? ImmutableList.Create(strokeIndex)
                : selectionIndices.Contains(strokeIndex)
                    ? selectionIndices.Remove(strokeIndex)
                    : selectionIndices.Add(strokeIndex).Sort();
            SetSelection(indices, layer.Strokes);
        }

        public void PressOrderBackground() => ClearSelection();

        public void DropOrderBadge(int layerIndex, int strokeIndex)
        {
            var layer = activeLayer;
            var layers = document.Layers;
            if (layer is null || !IsLayerEditable || layerIndex < 0 || layerIndex >= layers.Count || !ReferenceEquals(layers[layerIndex], layer))
                return;

            var strokes = layer.Strokes;
            var selected = selectionIndices;
            if (selected.IsEmpty || strokeIndex < 0 || strokeIndex >= strokes.Count || selected.Contains(strokeIndex))
                return;

            var countBefore = 0;
            foreach (var index in selected)
            {
                if (index < strokeIndex)
                    countBefore++;
            }

            var moving = new SerializableStroke[selected.Count];
            for (var i = 0; i < selected.Count; i++)
                moving[i] = strokes[selected[i]];

            var builder = strokes.ToBuilder();
            for (var i = selected.Count - 1; i >= 0; i--)
                builder.RemoveAt(selected[i]);

            var insertAt = strokeIndex - countBefore + (countBefore == selected.Count ? 1 : 0);
            builder.InsertRange(insertAt, moving);

            var indices = ImmutableList.CreateBuilder<int>();
            for (var i = 0; i < moving.Length; i++)
                indices.Add(insertAt + i);

            BeginEditUnit();
            layer.Strokes = builder.ToImmutable();
            SetSelection(indices.ToImmutable(), layer.Strokes);
            EndEditUnit();
        }

        void UpdateOrderBadges()
        {
            if (!IsOrderMode)
            {
                if (orderBadges.Length > 0)
                    OrderBadges = [];
                return;
            }

            var layers = document.Layers;
            var badges = new PenOrderBadge[orderEntries.Count];
            var count = 0;
            foreach (var entry in orderEntries)
            {
                if (entry.LayerIndex >= layers.Count)
                    continue;
                var layer = layers[entry.LayerIndex];
                if (entry.StrokeIndex >= layer.Strokes.Count)
                    continue;
                var points = layer.Strokes[entry.StrokeIndex].StylusPoints;
                if (points.Length == 0)
                    continue;

                var isSelected = ReferenceEquals(layer, activeLayer) && selectionIndices.Contains(entry.StrokeIndex);
                badges[count] = new PenOrderBadge(entry.LayerIndex, entry.StrokeIndex, new Point(points[0].X, points[0].Y), entry.Number, entry.IsIndependent, entry.IsDrawn, isSelected, !PenLayerTree.IsLocked(layers, layer));
                count++;
            }
            if (count != badges.Length)
                Array.Resize(ref badges, count);
            OrderBadges = badges;
        }

        public void SelectByLasso(IReadOnlyList<Point> lassoPoints)
        {
            var layer = activeLayer;
            if (layer is null || lassoPoints.Count < 3)
            {
                ClearSelection();
                return;
            }

            var indices = PenStrokes.HitTest(layer.Strokes, lassoPoints);
            if (indices.IsEmpty)
            {
                ClearSelection();
                return;
            }

            SetSelection(indices, layer.Strokes);
        }

        void SetSelection(ImmutableList<int> indices, ImmutableList<SerializableStroke> strokes)
        {
            selectionIndices = indices;
            SelectionBounds = PenStrokes.GetBounds(strokes, indices);
            UpdateSelectionCommands();
        }

        void SelectAll()
        {
            if (!IsOrderMode)
                SelectMode(PenMode.Select);

            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || layer.Strokes.IsEmpty)
                return;

            var indices = ImmutableList.CreateBuilder<int>();
            for (var i = 0; i < layer.Strokes.Count; i++)
                indices.Add(i);
            SetSelection(indices.ToImmutable(), layer.Strokes);
        }

        void DuplicateSelection()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            layer.Strokes = PenStrokes.Duplicate(layer.Strokes, selectionIndices, out var duplicated);
            SetSelection(duplicated, layer.Strokes);
        }

        void MoveSelectionToNewLayer()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            BeginEditUnit();

            layer.Strokes = PenStrokes.Extract(layer.Strokes, selectionIndices, out var moved);
            ClearSelection();

            var created = new PenLayer { Name = CreateLayerName(), ParentId = layer.ParentId, Strokes = moved };
            var layers = document.Layers;
            SetLayers(PenLayerTree.Normalize(layers.Insert(layers.IndexOf(layer) + 1, created)), created);

            EndEditUnit();
        }

        public void BeginSelectionTransform()
        {
            BeginEditUnit();

            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            transformSource = layer.Strokes;
            transformLayer = layer;
        }

        public void TransformSelection(Matrix matrix)
        {
            var source = transformSource;
            var layer = transformLayer;
            if (source is null || layer is null || selectionIndices.IsEmpty || !ReferenceEquals(layer, activeLayer))
                return;

            layer.Strokes = PenStrokes.Transform(source, selectionIndices, matrix);
            SelectionBounds = PenStrokes.GetBounds(layer.Strokes, selectionIndices);
        }

        public void EndSelectionTransform()
        {
            transformSource = null;
            transformLayer = null;
            EndEditUnit();
        }

        void DeleteSelection()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            layer.Strokes = PenStrokes.Remove(layer.Strokes, selectionIndices);
            ClearSelection();
        }

        void ApplySelectionColor()
        {
            var color = StrokeColor;
            ApplyToSelection(stroke => PenStrokes.WithColor(stroke, color));
        }

        void ApplySelectionThickness()
        {
            var thickness = StrokeThickness;
            ApplyToSelection(stroke => PenStrokes.WithThickness(stroke, thickness));
        }

        void ApplyToSelection(Func<SerializableStroke, DrawingAttributes?> convert)
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null || selectionIndices.IsEmpty)
                return;

            if (PenStrokes.Apply(layer.Strokes, selectionIndices, convert) is not { } changed)
                return;

            layer.Strokes = changed;
            SetSelection(selectionIndices, layer.Strokes);
        }

        void FlipSelection(bool isHorizontal)
        {
            var bounds = selectionBounds;
            if (!IsLayerEditable || activeLayer is null || selectionIndices.IsEmpty || bounds.IsEmpty)
                return;

            BeginSelectionTransform();
            TransformSelection(PenStrokes.CreateFlip(bounds, isHorizontal));
            EndSelectionTransform();
        }

        void CutSelection()
        {
            if (!IsLayerEditable || activeLayer is null || selectionIndices.IsEmpty)
                return;

            CopySelection();
            DeleteSelection();
        }

        void CopySelection()
        {
            var layer = activeLayer;
            if (layer is null || selectionIndices.IsEmpty)
                return;

            var items = new List<SerializableStroke>(selectionIndices.Count);
            foreach (var index in selectionIndices)
            {
                if (index < layer.Strokes.Count)
                    items.Add(layer.Strokes[index]);
            }
            if (items.Count == 0)
                return;

            try
            {
                Clipboard.SetData(ClipboardFormat, JsonConvert.SerializeObject(items));
            }
            catch (ExternalException e)
            {
                Log.Default.Write(Texts.CopyFailed, e);
                MessageBox.Show(Texts.CopyFailed, Texts.PenToolWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static bool HasClipboardStrokes()
        {
            try
            {
                return Clipboard.ContainsData(ClipboardFormat);
            }
            catch (ExternalException)
            {
                return false;
            }
        }

        static List<SerializableStroke>? ReadClipboardStrokes()
        {
            string? text;
            try
            {
                text = Clipboard.GetData(ClipboardFormat) as string;
            }
            catch (ExternalException e)
            {
                Log.Default.Write(Texts.PasteFailed, e);
                MessageBox.Show(Texts.PasteFailed, Texts.PenToolWindowTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
            if (text is null)
                return null;

            try
            {
                var items = JsonConvert.DeserializeObject<List<SerializableStroke>>(text);
                return items is not null && items.TrueForAll(PenStrokes.IsWellFormed) ? items : null;
            }
            catch
            {
                return null;
            }
        }

        void Paste()
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return;
            if (ReadClipboardStrokes() is not { Count: > 0 } items)
                return;

            layer.Strokes = PenStrokes.Append(layer.Strokes, items, out var appended);
            SelectMode(PenMode.Select);
            SetSelection(appended, layer.Strokes);
        }

        void ClearSelection()
        {
            if (selectionIndices.IsEmpty && selectionBounds.IsEmpty)
                return;

            selectionIndices = [];
            SelectionBounds = Rect.Empty;
            UpdateSelectionCommands();
        }

        void UpdateSelectionCommands()
        {
            UpdateOrderBadges();
            DeleteSelectionCommand.RaiseCanExecuteChanged();
            ClearSelectionCommand.RaiseCanExecuteChanged();
            DuplicateSelectionCommand.RaiseCanExecuteChanged();
            SelectionToNewLayerCommand.RaiseCanExecuteChanged();
            ApplySelectionColorCommand.RaiseCanExecuteChanged();
            ApplySelectionThicknessCommand.RaiseCanExecuteChanged();
            FlipSelectionHorizontalCommand.RaiseCanExecuteChanged();
            FlipSelectionVerticalCommand.RaiseCanExecuteChanged();
            CutSelectionCommand.RaiseCanExecuteChanged();
            CopySelectionCommand.RaiseCanExecuteChanged();
            PasteCommand.RaiseCanExecuteChanged();
            SelectAllCommand.RaiseCanExecuteChanged();
        }

        public void SetPenInverted(bool value)
        {
            if (isPenInverted == value)
                return;
            isPenInverted = value;
            RefreshTool();
        }

        static double GetStrokeThickness(PenMode tool) => tool switch
        {
            PenMode.Highlighter => PenSettings.Default.HighlighterStyle.StrokeThickness,
            PenMode.Pencil => PenSettings.Default.PencilStyle.StrokeThickness,
            PenMode.Eraser => PenSettings.Default.EraserStyle.StrokeThickness,
            _ => PenSettings.Default.PenStyle.StrokeThickness,
        };

        void RefreshTool()
        {
            var tool = ActiveTool;
            var thickness = GetStrokeThickness(tool);
            WetInkThickness = thickness;
            WetInkUsesPressure = tool is not PenMode.Eraser;
            StabilizationStrength = tool switch
            {
                PenMode.Pen => PenSettings.Default.PenStyle.Stabilization.ToStrength(),
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.Stabilization.ToStrength(),
                PenMode.Pencil => PenSettings.Default.PencilStyle.Stabilization.ToStrength(),
                _ => 0,
            };
            IgnoresPressure = tool switch
            {
                PenMode.Pen => !PenSettings.Default.PenStyle.IsPressure,
                PenMode.Highlighter => !PenSettings.Default.HighlighterStyle.IsPressure,
                PenMode.Pencil => !PenSettings.Default.PencilStyle.IsPressure,
                _ => true,
            };
            TaperLength = tool switch
            {
                PenMode.Pen => PenSettings.Default.PenStyle.Taper.ToLength(thickness),
                PenMode.Highlighter => PenSettings.Default.HighlighterStyle.Taper.ToLength(thickness),
                PenMode.Pencil => PenSettings.Default.PencilStyle.Taper.ToLength(thickness),
                _ => 0,
            };
            WetInkColor = tool is PenMode.Eraser ? EraserWetInkColor : StrokeColor;
            OnPropertyChanged(nameof(IsWetInkOverlay));
            OnPropertyChanged(nameof(StrokeColor));
            OnPropertyChanged(nameof(StrokeThickness));
            OnPropertyChanged(nameof(IsSelectionMode));
            OnPropertyChanged(nameof(IsFillMode));
            OnPropertyChanged(nameof(IsOrderMode));
            OnPropertyChanged(nameof(IsRectangleSelection));
        }

        public void UpdateWetStroke(StylusPointCollection? points)
        {
            if (points is null)
            {
                if (ClearWetStroke())
                    QueueRender();
                return;
            }

            var geometry = ReferenceEquals(points, wetPoints) ? wetStroke : BeginWetStroke(points);
            if (geometry is null)
                return;

            for (; wetPointCount < points.Count; wetPointCount++)
            {
                var point = points[wetPointCount];
                geometry.Append(point.X, point.Y, point.PressureFactor);
            }
            isWetDirty = true;
        }

        PenStrokeGeometry? BeginWetStroke(StylusPointCollection points)
        {
            ClearWetStroke();
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return null;

            var geometry = new PenStrokeGeometry(CreateStrokeAttributes(), mode is PenMode.Pencil);
            wetStroke = geometry;
            wetPoints = points;
            wetPointCount = 0;
            documentSource.SetWetStroke(document.Layers.IndexOf(layer), geometry);
            CompositionTarget.Rendering += OnRendering;
            return geometry;
        }

        bool ClearWetStroke()
        {
            if (wetStroke is null)
                return false;

            CompositionTarget.Rendering -= OnRendering;
            documentSource.ClearWetStroke();
            wetStroke.Dispose();
            wetStroke = null;
            wetPoints = null;
            isWetDirty = false;
            return true;
        }

        void OnRendering(object? sender, EventArgs e)
        {
            if (!isWetDirty)
                return;

            isWetDirty = false;
            UpdateDocumentImage(false);
        }

        DrawingAttributes CreateStrokeAttributes() => mode switch
        {
            PenMode.Highlighter => PenStyleFactory.CreateHighlighter(),
            PenMode.Pencil => PenStyleFactory.CreatePencil(),
            _ => PenStyleFactory.CreatePen(),
        };

        public void AddStroke(StylusPointCollection stylusPoints, bool isEraser)
        {
            if (ClearWetStroke())
                QueueRender();
            if (stylusPoints.Count == 0)
                return;

            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return;

            if (isEraser || mode is PenMode.Eraser)
            {
                EraseStrokes(layer, stylusPoints);
                ClearSelection();
                return;
            }

            var stroke = new Stroke(stylusPoints, CreateStrokeAttributes());
            layer.Strokes = layer.Strokes.Add(new SerializableStroke(stroke) { IsPencil = mode is PenMode.Pencil });
        }

        public void Fill(Point point)
        {
            var layer = activeLayer;
            if (!IsLayerEditable || layer is null)
                return;

            var image = RenderDocument();
            var difference = PenSettings.Default.FillStyle.Tolerance.ToDifference();
            if (!fillEngine.TryFill(image, CreateStrokeMirror(), point, difference, out var points, out var figures))
                return;

            layer.Strokes = layer.Strokes.Add(new SerializableStroke(points, PenStyleFactory.CreateFill()) { FillFigures = figures });
        }

        static void EraseStrokes(PenLayer layer, StylusPointCollection stylusPoints)
        {
            var style = PenSettings.Default.EraserStyle;
            if (PenStrokes.Erase(layer.Strokes, stylusPoints, style.StrokeThickness, style.Mode is EraserMode.Line) is { } erased)
                layer.Strokes = erased;
        }

        void AddLayer()
        {
            var layer = new PenLayer { Name = CreateLayerName(), ParentId = activeLayer?.ParentId ?? Guid.Empty };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(PenLayerTree.Normalize(layers.Insert(index, layer)), layer);
        }

        void AddFolder()
        {
            var folder = new PenLayer { Name = CreateFolderName(), IsFolder = true, ParentId = activeLayer?.ParentId ?? Guid.Empty };
            var layers = document.Layers;
            var index = activeLayer is null ? layers.Count : layers.IndexOf(activeLayer) + 1;
            SetLayers(PenLayerTree.Normalize(layers.Insert(index, folder)), folder);
        }

        void MoveIntoFolder()
        {
            var layer = activeLayer;
            var folder = PenLayerTree.FindTargetFolder(document.Layers, layer);
            if (layer is null || folder is null)
                return;

            BeginEditUnit();
            layer.ParentId = folder.Id;
            SetLayers(PenLayerTree.Normalize(document.Layers), layer);
            EndEditUnit();
        }

        void MoveOutOfFolder()
        {
            var layer = activeLayer;
            if (layer is null || layer.ParentId == Guid.Empty)
                return;

            var layers = document.Layers;
            var folder = PenLayerTree.Find(layers, layer.ParentId);
            if (folder is null)
                return;

            BeginEditUnit();
            layer.ParentId = folder.ParentId;
            var next = layers.Remove(layer);
            SetLayers(PenLayerTree.Normalize(next.Insert(next.IndexOf(folder) + 1, layer)), layer);
            EndEditUnit();
        }

        void DuplicateLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var copies = ImmutableList.CreateBuilder<PenLayer>();
            var copy = PenLayerTree.CloneSubtree(layers, layer, layer.ParentId, copies);
            copy.Name = layer.IsFolder ? CreateFolderName() : CreateLayerName();

            SetLayers(PenLayerTree.Normalize(layers.InsertRange(layers.IndexOf(layer) + 1, copies)), copy);
        }

        void DeleteLayer()
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var index = layers.IndexOf(layer);
            if (index < 0)
                return;

            var builder = layers.ToBuilder();
            PenLayerTree.RemoveSubtree(builder, layer);
            if (builder.Count == 0)
                return;

            var next = PenLayerTree.Normalize(builder.ToImmutable());
            SetLayers(next, next[Math.Min(index, next.Count - 1)]);
        }

        void MergeLayer()
        {
            var layer = activeLayer;
            var lower = PenLayerTree.FindMergeTarget(document.Layers, layer);
            if (layer is null || lower is null)
                return;

            BeginEditUnit();
            lower.Strokes = lower.Strokes.AddRange(layer.Strokes);
            SetLayers(PenLayerTree.Normalize(document.Layers.Remove(layer)), lower);
            EndEditUnit();
        }

        bool CanDeleteLayer()
            => activeLayer is { } layer && document.Layers.Count > PenLayerTree.CountSubtree(document.Layers, layer);

        bool CanMoveLayer(int delta)
            => activeLayer is { } layer && PenLayerTree.FindSibling(document.Layers, layer, delta) is not null;

        void MoveLayer(int delta)
        {
            var layer = activeLayer;
            if (layer is null)
                return;

            var layers = document.Layers;
            var sibling = PenLayerTree.FindSibling(layers, layer, delta);
            if (sibling is null)
                return;

            var next = layers.Remove(layer);
            SetLayers(PenLayerTree.Normalize(next.Insert(next.IndexOf(sibling) + (delta > 0 ? 1 : 0), layer)), layer);
        }

        void SetLayers(ImmutableList<PenLayer> layers, PenLayer? active)
        {
            document.Layers = layers;
            var next = active ?? (layers.IsEmpty ? null : layers[^1]);
            if (next is not null)
                PenLayerTree.ExpandAncestors(layers, next);
            UpdateDisplayLayers(layers);
            ActiveLayer = next;
            OnPropertyChanged(nameof(Layers));
            UpdateCommands();
            InvalidateDocument();
        }

        void UpdateDisplayLayers(ImmutableList<PenLayer> layers)
            => DisplayLayers = PenLayerTree.CreateDisplayLayers(layers);

        string CreateLayerName()
        {
            layerNumber++;
            return string.Format(Texts.LayerDefaultName, layerNumber);
        }

        string CreateFolderName()
        {
            folderNumber++;
            return string.Format(Texts.FolderDefaultName, folderNumber);
        }

        void UpdateCommands()
        {
            SelectAllCommand.RaiseCanExecuteChanged();
            DuplicateLayerCommand.RaiseCanExecuteChanged();
            DeleteLayerCommand.RaiseCanExecuteChanged();
            MergeLayerCommand.RaiseCanExecuteChanged();
            MoveLayerUpCommand.RaiseCanExecuteChanged();
            MoveLayerDownCommand.RaiseCanExecuteChanged();
            MoveIntoFolderCommand.RaiseCanExecuteChanged();
            MoveOutOfFolderCommand.RaiseCanExecuteChanged();
        }

        void OnDocumentChanged(object? sender, YukkuriMovieMaker.UndoRedo.UndoRedoEventArgs e)
        {
            if (isRestoring)
                return;

            history.MarkDirty();
            if (!history.IsEditing)
                CommitSnapshot();
            OnPropertyChanged(nameof(IsLayerEditable));
            if (!IsLayerEditable)
                ClearSelection();
            UpdateSelectionCommands();
            InvalidateDocument();
        }

        public void SetView(double zoom, Point origin, Size size, double dpiScale)
        {
            if (viewZoom == zoom && viewOrigin == origin && viewSize == size && viewDpiScale == dpiScale)
                return;

            viewZoom = zoom;
            viewOrigin = origin;
            viewSize = size;
            viewDpiScale = dpiScale;
            QueueRender();
        }

        void InvalidateDocument()
        {
            isDocumentDirty = true;
            isOrderDirty = true;
            QueueRender();
        }

        void QueueRender()
        {
            if (isRenderQueued)
                return;

            isRenderQueued = true;
            dispatcher.InvokeAsync(() =>
            {
                isRenderQueued = false;
                if (isDisposed)
                    return;
                var isDirtyDocument = isDocumentDirty;
                isDocumentDirty = false;
                UpdateDocumentImage(isDirtyDocument);
            }, DispatcherPriority.Normal);
        }

        void UpdateDocumentImage(bool updatesThumbnails)
        {
            var scale = viewDpiScale;
            var width = (int)Math.Ceiling(viewSize.Width * scale);
            var height = (int)Math.Ceiling(viewSize.Height * scale);
            documentSource.PreviewScale = width > 0 && height > 0 ? viewZoom * scale : 1;
            documentSource.Update(documentDescription);
            if (IsOrderMode && isOrderDirty)
            {
                isOrderDirty = false;
                documentSource.CollectOrder(documentDescription, orderEntries);
                UpdateOrderBadges();
            }
            if (width > 0 && height > 0)
            {
                var isLocal = documentSource.TryGetWetChange(out var change);
                if (!isLocal || TryGetDirtyRect(change, width, height, out var dirty))
                    DocumentImage = previewRenderer.RenderView(documentSource.Output, width, height,
                        viewZoom * scale, new Point(viewOrigin.X * scale, viewOrigin.Y * scale),
                        CanvasWidth, CanvasHeight, 96 * scale, isLocal ? dirty : null);
            }
            if (updatesThumbnails)
                thumbnailRenderer.Update(document.Layers, ThumbnailWidth, ThumbnailHeight, CanvasWidth, CanvasHeight);
        }

        bool TryGetDirtyRect(in Vortice.RawRectF change, int width, int height, out Int32Rect dirty)
        {
            var scale = viewDpiScale;
            var offsetX = viewOrigin.X * scale + CanvasWidth / 2 * viewZoom * scale;
            var offsetY = viewOrigin.Y * scale + CanvasHeight / 2 * viewZoom * scale;
            var left = Math.Max(0, (int)Math.Floor(change.Left + offsetX) - DirtyMargin);
            var top = Math.Max(0, (int)Math.Floor(change.Top + offsetY) - DirtyMargin);
            var right = Math.Min(width, (int)Math.Ceiling(change.Right + offsetX) + DirtyMargin);
            var bottom = Math.Min(height, (int)Math.Ceiling(change.Bottom + offsetY) + DirtyMargin);
            dirty = new Int32Rect(left, top, right - left, bottom - top);
            return dirty.Width > 0 && dirty.Height > 0;
        }

        WriteableBitmap RenderDocument()
        {
            SetDocumentLength(100);
            documentSource.PreviewScale = 1;
            documentSource.Update(documentDescription);
            var image = fillRenderer.Render(documentSource.Output, info.VideoInfo.Width, info.VideoInfo.Height);
            SetDocumentLength(IsOrderMode ? previewLength : 100);
            return image;
        }

        BitmapSource RenderBackground()
        {
            var time = info.ItemPosition.Time < TimeSpan.Zero
                ? info.ItemPosition.Time
                : info.ItemDuration.Time < info.ItemPosition.Time
                ? info.VideoInfo.GetTimeFrom(info.ItemPosition.Frame + info.ItemDuration.Frame - 1)
                : info.TimelinePosition.Time;
            source.Update(time, TimelineSourceUsage.Paused);
            return source.RenderBitmapSource();
        }

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;
            ClearWetStroke();
            document.UndoRedoCommandCreated -= OnDocumentChanged;
            disposer.Dispose();
        }
    }
}
