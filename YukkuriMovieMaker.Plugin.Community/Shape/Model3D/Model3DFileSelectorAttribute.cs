using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Shape.Model3D.Parsers;
using YukkuriMovieMaker.Settings;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Model3D;

[AttributeUsage(AttributeTargets.Property)]
internal sealed class Model3DFileSelectorAttribute : PropertyEditorAttribute2
{
    public const string CustomFileGroupKey = "Community.Model3D";

    private static readonly string FilterPattern = BuildFilterPattern();
    private static readonly IList<IFileSelectorThumbnailLoader> ThumbnailLoaders = new List<IFileSelectorThumbnailLoader>
    {
        new Model3DThumbnailLoader(),
    };

    private static readonly Lock RegisterLock = new();
    private static volatile bool _registered;

    public Model3DFileSelectorAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create() => new FileSelector();

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        EnsureCustomGroupRegistered();

        var editor = (FileSelector)control;
        editor.CustomFileGroupKey = CustomFileGroupKey;
        editor.FileType = FileType.None;
        editor.ShowThumbnail = true;
        editor.ThumbnailLoaders = ThumbnailLoaders;
        editor.Filter = FilterPattern;
        editor.FilterName = Texts.FilterName;

        var currentItemProperty = itemProperties[0];
        var file = (string?)currentItemProperty.PropertyInfo.GetValue(currentItemProperty.PropertyOwner);
        editor.DirectoryPath = string.IsNullOrEmpty(file) ? null : Path.GetDirectoryName(file);

        var targetProperties = GetTargetProperties(itemProperties).ToArray();
        editor.SetBinding(FileSelector.ValueProperty, ItemPropertiesBinding.Create2(targetProperties));
    }

    public override void ClearBindings(FrameworkElement control)
    {
        BindingOperations.ClearBinding(control, FileSelector.ValueProperty);
    }

    private static IEnumerable<ItemProperty> GetTargetProperties(ItemProperty[] itemProperties)
    {
        foreach (var itemProperty in itemProperties)
        {
            if (itemProperty.PropertyInfo.GetCustomAttribute<Model3DFileSelectorAttribute>() is null)
                continue;
            yield return itemProperty;
        }
    }

    private static string BuildFilterPattern()
        => string.Join(';', Model3DLoader.SupportedExtensions.Order(StringComparer.Ordinal).Select(extension => $"*{extension}"));

    private static void EnsureCustomGroupRegistered()
    {
        if (_registered) return;

        lock (RegisterLock)
        {
            if (_registered) return;

            FileSettings.Default.Groups.RegisterCustomGroup(
                key: CustomFileGroupKey,
                fileType: FileType.None,
                customFilter: $"{Texts.FilterName}|{FilterPattern}");

            _registered = true;
        }
    }
}
