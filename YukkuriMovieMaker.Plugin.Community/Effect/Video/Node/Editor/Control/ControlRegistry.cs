using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

/// <summary>
///     コントロール生成ファクトリのデリゲート
/// </summary>
public delegate DataTemplate ControlFactory(PropertyControlBaseAttribute attribute);

/// <summary>
///     属性とコントロールのマッピングを管理
/// </summary>
public static class ControlRegistry
{
    private static readonly Dictionary<Type, ControlFactory> Factories = new();
    private static readonly Lock Lock = new();

    /// <summary>
    ///     コントロールファクトリを登録
    /// </summary>
    public static void Register<T>(ControlFactory factory) where T : PortControlBase
    {
        ArgumentNullException.ThrowIfNull(typeof(T));
        ArgumentNullException.ThrowIfNull(factory);

        lock (Lock)
        {
            Factories[typeof(T)] = factory;
        }
    }

    /// <summary>
    ///     コントロールファクトリを登録解除
    /// </summary>
    public static void Unregister(Type controlType)
    {
        lock (Lock)
        {
            Factories.Remove(controlType);
        }
    }

    /// <summary>
    ///     すべての登録をクリア
    /// </summary>
    public static void Clear()
    {
        lock (Lock)
        {
            Factories.Clear();
        }
    }

    /// <summary>
    ///     属性からコントロールを作成
    /// </summary>
    public static DataTemplate? CreateControl(PropertyControlBaseAttribute attribute)
    {
        if (attribute == null!)
            return null;

        lock (Lock)
        {
            if (Factories.TryGetValue(attribute.ControlType, out var factory)) return factory(attribute);
        }

        return null;
    }

    /// <summary>
    ///     指定されたコントロールタイプが登録されているか確認
    /// </summary>
    public static bool IsRegistered(Type controlType)
    {
        lock (Lock)
        {
            return Factories.ContainsKey(controlType);
        }
    }

    /// <summary>
    ///     登録されているすべてのコントロールタイプを取得
    /// </summary>
    public static IEnumerable<Type> GetRegisteredTypes()
    {
        lock (Lock)
        {
            return Factories.Keys.ToList();
        }
    }
}