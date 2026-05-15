using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Settings;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Lut;

[AttributeUsage(AttributeTargets.Property)]
public sealed class LutFileSelectorAttribute : PropertyEditorAttribute2
{
    public const string CustomFileGroupKey = "Community.Lut";

    const string FilterPattern = "*.cube;*.look";

    static readonly IList<IFileSelectorThumbnailLoader> thumbnailLoaders = new List<IFileSelectorThumbnailLoader>
    {
        new LutThumbnailLoader(),
    };
    static readonly Lock registerLock = new();
    static volatile bool registered;

    public LutFileSelectorAttribute()
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
        editor.ThumbnailLoaders = thumbnailLoaders;
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

    static IEnumerable<ItemProperty> GetTargetProperties(ItemProperty[] itemProperties)
    {
        foreach (var itemProperty in itemProperties)
        {
            if (itemProperty.PropertyInfo.GetCustomAttribute<LutFileSelectorAttribute>() is null)
                continue;
            yield return itemProperty;
        }
    }

    static void EnsureCustomGroupRegistered()
    {
        if (registered) return;
        lock (registerLock)
        {
            if (registered) return;
            FileSettings.Default.Groups.RegisterCustomGroup(
                key: CustomFileGroupKey,
                fileType: FileType.None,
                customFilter: $"{Texts.FilterName}|{FilterPattern}");
            registered = true;
        }
    }
}
