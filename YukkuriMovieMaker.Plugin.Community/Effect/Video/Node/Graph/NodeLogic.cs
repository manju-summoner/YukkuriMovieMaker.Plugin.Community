using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Events;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph.Port;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Graph;

public abstract class NodeLogic : IDisposable
{
    private readonly Dictionary<string, (InputsContainer container, PropertyChangedEventHandler handler)>
        _dynamicHandlers = new();

    public readonly Dictionary<string, InputPort> Inputs = new();
    public readonly Dictionary<string, OutputPort> Outputs = new();
    public readonly Dictionary<string, NodeGraph> SubGraphs = new();

    private bool _isEvaluated;

    protected NodeLogic()
    {
        InitializePorts();
    }

    public Guid Id { get; set; }

    protected EvaluationContext? EvaluationContext { get; private set; }

    /// <summary>
    ///     このノードが保持するDirect2Dリソースを解放します。
    ///     サブクラスはオーバーライドしてD2Dフィールドをnullにし、base.Dispose()を呼ぶこと。
    ///     複数回呼ばれても安全（冪等）に実装すること。
    /// </summary>
    public virtual void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private void InitializePorts()
    {
        var props = GetType().GetProperties();

        foreach (var prop in props)
        {
            if (Attribute.IsDefined(prop, typeof(InputPortAttribute)))
            {
                var attr = (InputPortAttribute)prop.GetCustomAttributes(typeof(InputPortAttribute), true).First();

                if (attr.IsDynamic && typeof(InputsContainer).IsAssignableFrom(prop.PropertyType))
                {
                    if (prop.GetValue(this) is InputsContainer container)
                        SubscribeDynamicContainer(prop.Name, container, container.GetType());
                }
                else if (!attr.IsDynamic)
                {
                    Inputs.Add(prop.Name, new InputPort(this, prop.PropertyType));
                }
            }

            if (Attribute.IsDefined(prop, typeof(OutputPortAttribute)))
                Outputs.Add(prop.Name, new OutputPort(this, prop.PropertyType));

            if (Attribute.IsDefined(prop, typeof(SubGraphAttribute)) && prop.GetValue(this) is NodeGraph subGraph)
                SubGraphs.Add(prop.Name, subGraph);
        }
    }

    private void SubscribeDynamicContainer(string propName, InputsContainer container, Type containerType)
    {
        if (_dynamicHandlers.TryGetValue(propName, out var old))
        {
            old.container.PropertyChanged -= old.handler;
            _dynamicHandlers.Remove(propName);
        }

        foreach (var subProp in containerType.GetProperties().OrderBy(p => p.MetadataToken))
        {
            if (!Attribute.IsDefined(subProp, typeof(InputPortAttribute))) continue;
            var key = $"{propName}.{subProp.Name}";
            if (!Inputs.ContainsKey(key))
                Inputs[key] = new InputPort(this, subProp.PropertyType);
        }

        PropertyChangedEventHandler handler = (sender, args) =>
        {
            if (args.PropertyName == null) return;
            var key = $"{propName}.{args.PropertyName}";
            if (!Inputs.TryGetValue(key, out var port)) return;
            var subProp = containerType.GetProperty(args.PropertyName);
            if (subProp == null) return;
            port.SetValue(subProp.GetValue(sender));
        };

        container.PropertyChanged += handler;
        _dynamicHandlers[propName] = (container, handler);
    }

    public void UpdateSubGraphs()
    {
        var props = GetType().GetProperties();

        foreach (var prop in props)
            if (Attribute.IsDefined(prop, typeof(SubGraphAttribute)) &&
                prop.PropertyType == typeof(NodeGraph))
            {
                if (prop.GetValue(this) is NodeGraph subGraph)
                    SubGraphs[prop.Name] = subGraph;
                else
                    SubGraphs.Remove(prop.Name);
            }
    }

    /// <summary>
    ///     復元後など、VMなしで動的ポートの Inputs を現在のコンテナ状態に同期する。
    /// </summary>
    internal void SyncDynamicInputs()
    {
        foreach (var prop in GetType().GetProperties())
        {
            if (!Attribute.IsDefined(prop, typeof(InputPortAttribute))) continue;
            var attr = (InputPortAttribute)prop.GetCustomAttributes(typeof(InputPortAttribute), true).First();
            if (!attr.IsDynamic || !typeof(InputsContainer).IsAssignableFrom(prop.PropertyType)) continue;
            if (prop.GetValue(this) is not InputsContainer container) continue;
            SwapDynamicContainer(prop.Name, container);
        }
    }

    public async Task EvaluateInternal(EvaluationContext? context = null)
    {
        if (_isEvaluated) return;
        EvaluationContext = context;
        var success = false;
        try
        {
            await Calculate().ConfigureAwait(false);
            success = true;
        }
        catch (NullReferenceException)
        {
            if (context != null) throw;
        }
        finally
        {
            EvaluationContext = null;
            if (success) _isEvaluated = true;
        }
    }

    public void Invalidate()
    {
        if (!_isEvaluated) return;
        _isEvaluated = false;
        foreach (var output in Outputs.Values) output.Invalidate();
    }

    protected void InvalidateForce()
    {
        _isEvaluated = false;
        foreach (var output in Outputs.Values) output.Invalidate();
    }

    protected async Task<T?> GetInputAsync<T>([CallerMemberName] string name = null!)
    {
        var value = await Inputs[name].GetValue(EvaluationContext).ConfigureAwait(false);
        if (value is null) return default;
        if (value is T typed) return typed;
        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    protected T? GetInput<T>([CallerMemberName] string name = null!)
    {
        var value = Inputs[name].GetValue(EvaluationContext).GetAwaiter().GetResult();
        if (value is null) return default;
        if (value is T typed) return typed;
        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    protected void SetInput<T>(T value, [CallerMemberName] string name = null!)
    {
        Inputs[name].SetValue(value);
    }

    protected internal void SetDynamicContainer(InputsContainer newContainer, [CallerMemberName] string name = null!)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            NeedToReinitializeInputPorts?.Invoke(this, new NeedToReinitializeInputPortsEvent(name, newContainer));
        else
            Application.Current.Dispatcher.BeginInvoke(() =>
                NeedToReinitializeInputPorts?.Invoke(this, new NeedToReinitializeInputPortsEvent(name, newContainer)));
    }

    internal void SwapDynamicContainer(string name, InputsContainer newContainer)
    {
        var prefix = name + ".";
        foreach (var key in Inputs.Keys.Where(k => k.StartsWith(prefix)).ToList())
            Inputs.Remove(key);
        SubscribeDynamicContainer(name, newContainer, newContainer.GetType());
    }

    public event EventHandler<NeedToReinitializeInputPortsEvent>? NeedToReinitializeInputPorts;

    internal void InvokeNeedToReinitializeInputPorts(object? sender, NeedToReinitializeInputPortsEvent @event)
    {
        NeedToReinitializeInputPorts?.Invoke(sender, @event);
    }

    protected void SetOutput(object? value, [CallerMemberName] string name = null!)
    {
        Outputs[name].SetValue(value);
    }

    protected async Task<T?> GetOutputAsync<T>([CallerMemberName] string name = null!)
    {
        var value = await Outputs[name].GetValue().ConfigureAwait(false);
        if (value is null) return default;
        if (value is T typed) return typed;
        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    protected T? GetOutput<T>([CallerMemberName] string name = null!)
    {
        var value = Outputs[name].GetValue().GetAwaiter().GetResult();
        if (value is null) return default;
        if (value is T typed) return typed;
        try
        {
            return (T?)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return default;
        }
    }

    protected internal virtual void OnInputValueChanged(string portName, object? value)
    {
    }

    protected abstract Task Calculate();
}