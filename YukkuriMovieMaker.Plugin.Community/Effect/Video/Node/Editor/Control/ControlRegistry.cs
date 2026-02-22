using System.Windows;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Attributes;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.ViewModel;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control;

/// <summary>
///     コントロール生成ファクトリのデリゲート
/// </summary>
public delegate FrameworkElement ControlFactory(PropertyControlBaseAttribute attribute, PortViewModel port);

/// <summary>
///     属性とコントロールのマッピングを管理
/// </summary>
public static class ControlRegistry
{
    private static readonly Dictionary<string, ControlFactory> Factories = new();
    private static readonly Lock Lock = new();

    /// <summary>
    ///     コントロールファクトリを登録
    /// </summary>
    public static void Register(string controlType, ControlFactory factory)
    {
        if (string.IsNullOrEmpty(controlType))
            throw new ArgumentNullException(nameof(controlType));

        if (factory == null)
            throw new ArgumentNullException(nameof(factory));

        lock (Lock)
        {
            Factories[controlType] = factory;
        }
    }

    /// <summary>
    ///     コントロールファクトリを登録解除
    /// </summary>
    public static void Unregister(string controlType)
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
    public static FrameworkElement? CreateControl(
        PropertyControlBaseAttribute attribute,
        PortViewModel port)
    {
        if (attribute == null! || port == null!)
            return null;

        lock (Lock)
        {
            if (Factories.TryGetValue(attribute.ControlType, out var factory)) return factory(attribute, port);
        }

        return null;
    }

    /// <summary>
    ///     指定されたコントロールタイプが登録されているか確認
    /// </summary>
    public static bool IsRegistered(string controlType)
    {
        lock (Lock)
        {
            return Factories.ContainsKey(controlType);
        }
    }

    /// <summary>
    ///     登録されているすべてのコントロールタイプを取得
    /// </summary>
    public static IEnumerable<string> GetRegisteredTypes()
    {
        lock (Lock)
        {
            return Factories.Keys.ToList();
        }
    }
}