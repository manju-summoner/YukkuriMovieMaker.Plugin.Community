using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Snapshot;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Localize;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Nodes.Func;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.ValueTypes;
using YukkuriMovieMaker.Plugin.Effects;
using AlphaMode = Vortice.DCommon.AlphaMode;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node;

[VideoEffect(nameof(TextUi.Node), [VideoEffectCategories.Filtering], ["Node"], IsAviUtlSupported = false,
    ResourceType = typeof(TextUi))]
public sealed class NodeEffect : VideoEffectBase
{
    private readonly Dispatcher _uiDispatcher = Application.Current.Dispatcher;
    private GraphSnapshot _graph = new();
    public override string Label => TextUi.Node;

    [Display(Name = nameof(TextUi.NodeEditor), GroupName = nameof(TextUi.Node),
        ResourceType = typeof(TextUi))]
    [OpenNodeEditor]
    public GraphSnapshot Graph
    {
        get => _graph;
        set
        {
            Set(ref _graph, value);
            if (InternalGraph is null) return;
            var tempGraph = Serializer.Restore(value);
            InternalGraph.UpdateGraph(tempGraph);
            InvokeGraphUpdated();
        }
    }

    public GraphSnapshot InternalGraphSnapshot
    {
        get => _graph;
        set => Set(ref _graph, value, nameof(Graph));
    }

    [JsonIgnore]
    internal NodeGraph? InternalGraph
    {
        get;
        set
        {
            if (field != null)
                UnSubscribeChildUndoRedoable(field.PreviewNotifier);
            field = value;
            if (field != null)
                SubscribeChildUndoRedoable(field.PreviewNotifier);
        }
    }

    public event EventHandler? GraphUpdated;

    internal void InvokeGraphUpdated()
    {
        if (_uiDispatcher.CheckAccess())
            GraphUpdated?.Invoke(this, EventArgs.Empty);
        else
            _uiDispatcher.BeginInvoke(() => GraphUpdated?.Invoke(this, EventArgs.Empty));
    }

    protected override IEnumerable<IAnimatable> GetAnimatables()
    {
        return [];
    }

    public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex,
        ExoOutputDescription exoOutputDescription)
    {
        return [""];
    }

    public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
    {
        return new Processor(devices, this);
    }

    private static IEnumerable<(InputPort Port, string Path)> EnumerateFilePathPorts(NodeGraph graph)
    {
        foreach (var node in graph.Nodes.Values)
        {
            foreach (var prop in node.GetType().GetProperties())
            {
                if (!Attribute.IsDefined(prop, typeof(FileSelectorAttribute))) continue;
                if (!node.Inputs.TryGetValue(prop.Name, out var port)) continue;
                if (port.LocalValue is string { Length: > 0 } path)
                    yield return (port, path);
            }

            foreach (var entry in node.SubGraphs.Values.SelectMany(EnumerateFilePathPorts))
                yield return entry;
        }
    }

    public override IEnumerable<string> GetFiles()
    {
        if (InternalGraph is not null)
            return EnumerateFilePathPorts(InternalGraph)
                .Select(p => p.Path)
                .Distinct()
                .ToList();

        try
        {
            var tempGraph = Serializer.Restore(_graph);
            return EnumerateFilePathPorts(tempGraph)
                .Select(p => p.Path)
                .Distinct()
                .ToList();
        }
        catch (Exception)
        {
            return base.GetFiles();
        }
    }

    public override void ReplaceFile(string from, string to)
    {
        if (InternalGraph is null)
        {
            base.ReplaceFile(from, to);
            return;
        }

        var matches = EnumerateFilePathPorts(InternalGraph)
            .Where(p => p.Path == from)
            .ToList();

        if (matches.Count == 0)
            return;

        InternalGraph.BeginEdit();
        try
        {
            foreach (var match in matches)
                match.Port.SetValue(to);
        }
        finally
        {
            InternalGraph.EndEdit();
        }
    }
}

public sealed class Processor : IVideoEffectProcessor
{
    private readonly IGraphicsDevicesAndContext _devices;
    private readonly Lock _lock = new();
    private readonly NodeEffect _nodeEffect;
    private ID2D1Image? _affineOutput;

    private AffineTransform2D? _affineTransform;
    private ID2D1Bitmap1? _blankBitmap;

    private ID2D1Image? _currentInputImage;

    private bool _hasError;

    private volatile ArgumentsNode _inputNode = null!;
    private bool _isEvaluating;
    private ID2D1Image? _outputImage;
    private volatile ReturnNode _outputNode = null!;

    public Processor(IGraphicsDevicesAndContext devices, NodeEffect effect)
    {
        _devices = devices;
        _nodeEffect = effect;

        InitializeGraph();
        CreateBlankBitmap();

        _nodeEffect.GraphUpdated += OnGraphUpdated;
    }

    public ID2D1Image Output => _outputImage ?? _blankBitmap!;

    public DrawDescription Update(EffectDescription effectDescription)
    {
        lock (_lock)
        {
            try
            {
                if (_isEvaluating)
                    return effectDescription.DrawDescription;

                _isEvaluating = true;

                if (_nodeEffect.InternalGraph == null! || _inputNode == null! || _outputNode == null!)
                    return effectDescription.DrawDescription;

                // 評価開始
                var context = new EvaluationContext(_devices, effectDescription);

                _inputNode.InjectArguments(new Dictionary<string, object?>
                {
                    ["InputImage"] = new ImageWrapper { Image = _currentInputImage },
                    ["FrameIndex"] = effectDescription.ItemPosition.Frame
                });

                var outputDict = _outputNode.ExtractReturns(context).GetAwaiter().GetResult();
                var outputImage = (outputDict["OutputImage"] as ImageWrapper)?.Image;

                if (outputImage == null || outputImage.NativePointer == IntPtr.Zero)
                    throw new InvalidOperationException(TextUi.OutputImageIsNull);

                _outputImage = outputImage;
                _hasError = false;

                ApplyAffineTransform(outputImage);

                return effectDescription.DrawDescription;
            }
            catch (Exception ex)
            {
                if (!_hasError) Debug.WriteLine($"[Processor] Error: {ex.Message}");

                _hasError = true;

                SetBlankImage();
                ApplyAffineTransform(_blankBitmap!);

                return effectDescription.DrawDescription;
            }
            finally
            {
                _isEvaluating = false;
            }
        }
    }

    public void SetInput(ID2D1Image? input)
    {
        lock (_lock)
        {
            _currentInputImage = input;
        }
    }

    public void ClearInput()
    {
        lock (_lock)
        {
            _currentInputImage = null;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_nodeEffect.InternalGraph != null!)
                _nodeEffect.InternalGraph.Committed -= OnGraphCommitted;

            _nodeEffect.GraphUpdated -= OnGraphUpdated;

            // このProcessorが使用していたノードのD2Dリソースを解放する。
            // ノードはD2Dリソースを遅延初期化するため、次回Calculate時に再生成される。
            if (_nodeEffect.InternalGraph != null)
                foreach (var node in _nodeEffect.InternalGraph.Nodes.Values)
                    node.Dispose();

            ClearInput();

            _affineTransform?.SetInput(0, null, true);
            _affineTransform?.Dispose();
            _affineTransform = null;

            _affineOutput?.Dispose();
            _affineOutput = null;
            _outputImage = null;

            _blankBitmap?.Dispose();
            _blankBitmap = null;
        }
    }

    /// <summary>
    ///     グラフの初期化
    /// </summary>
    private void InitializeGraph()
    {
        if (_nodeEffect.Graph.Nodes.Count > 0)
        {
            _nodeEffect.InternalGraph = Serializer.Restore(_nodeEffect.Graph);

            _inputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault() ??
                         new ArgumentsNode(
                             new PortDefinition("InputImage", typeof(ImageWrapper)),
                             new PortDefinition("FrameIndex", typeof(int))
                         )
                         {
                             Id = Guid.NewGuid()
                         };
            _outputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault() ??
                          new ReturnNode(
                              new PortDefinition("OutputImage", typeof(ImageWrapper))
                          )
                          {
                              Id = Guid.NewGuid()
                          };
        }
        else
        {
            _nodeEffect.InternalGraph = new NodeGraph();

            _inputNode = new ArgumentsNode(
                new PortDefinition("InputImage", typeof(ImageWrapper)),
                new PortDefinition("FrameIndex", typeof(int))
            )
            {
                Id = Guid.NewGuid()
            };

            _outputNode = new ReturnNode(
                new PortDefinition("OutputImage", typeof(ImageWrapper))
            )
            {
                Id = Guid.NewGuid()
            };

            _nodeEffect.InternalGraph.AddNode(_inputNode);
            _nodeEffect.InternalGraph.AddNode(_outputNode);

            _nodeEffect.InternalGraph.SetVisualState(_inputNode.Id, 100, 100);
            _nodeEffect.InternalGraph.SetVisualState(_outputNode.Id, 500, 100);

            _nodeEffect.InternalGraph.Connect(_inputNode.Id, "InputImage", _outputNode.Id, "OutputImage");

            _nodeEffect.Graph = Serializer.Create(_nodeEffect.InternalGraph);

            _inputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ArgumentsNode>().First();
            _outputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ReturnNode>().First();
        }

        if (_nodeEffect.InternalGraph != null!)
        {
            _nodeEffect.InternalGraph.Committed -= OnGraphCommitted;
            _nodeEffect.InternalGraph.Committed += OnGraphCommitted;
        }

        _nodeEffect.InvokeGraphUpdated();
    }

    private void OnGraphUpdated(object? sender, EventArgs e)
    {
        if (_nodeEffect.InternalGraph == null!) return;

        _nodeEffect.InternalGraph.Committed -= OnGraphCommitted;
        _nodeEffect.InternalGraph.Committed += OnGraphCommitted;

        _inputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ArgumentsNode>().FirstOrDefault()
                     ?? _inputNode;
        _outputNode = _nodeEffect.InternalGraph.Nodes.Values.OfType<ReturnNode>().FirstOrDefault()
                      ?? _outputNode;
    }

    private async void OnGraphCommitted(object? sender, CommittedEventArgs e)
    {
        try
        {
            if (_nodeEffect.InternalGraph == null!) return;

            lock (_lock)
            {
                _nodeEffect.InternalGraph.InvalidateAll();
            }

            _nodeEffect.InternalGraphSnapshot = await Serializer.CreateAsync(_nodeEffect.InternalGraph);
        }
        catch (Exception exception)
        {
            Debug.WriteLine(exception.Message);
        }
    }

    private void CreateBlankBitmap()
    {
        var bitmapProperties = new BitmapProperties1(
            new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied),
            96,
            96,
            BitmapOptions.Target
        );

        _blankBitmap = _devices.DeviceContext.CreateBitmap(
            new SizeI(1, 1),
            IntPtr.Zero,
            0,
            bitmapProperties
        );
    }

    private void SetBlankImage()
    {
        if (_blankBitmap == null) CreateBlankBitmap();

        var deviceContext = _devices.DeviceContext;
        var previousTarget = deviceContext.Target;
        deviceContext.Target = _blankBitmap;
        deviceContext.BeginDraw();
        deviceContext.Clear(new Color(0, 0, 0, 0));
        deviceContext.EndDraw();
        deviceContext.Target = previousTarget;

        _outputImage = _blankBitmap;
    }

    private void ApplyAffineTransform(ID2D1Image input)
    {
        _affineTransform ??= new AffineTransform2D(_devices.DeviceContext)
        {
            BorderMode = BorderMode.Soft,
            TransformMatrix = Matrix3x2.Identity
        };

        _affineTransform.SetInput(0, input, true);

        _affineOutput?.Dispose();
        _affineOutput = _affineTransform.Output;
        _outputImage = _affineOutput;
    }
}