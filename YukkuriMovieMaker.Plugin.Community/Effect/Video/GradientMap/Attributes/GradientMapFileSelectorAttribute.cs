using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Services;
using YukkuriMovieMaker.Settings;
using YukkuriMovieMaker.Views.Converters;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.GradientMap.Attributes;

/// <summary>
/// グラデーションマップ専用の FileSelector 属性。組み込み <see cref="FileSelector"/> を、
/// .grd/.png/.jpg/.jpeg/.bmp/.gif/.tiff 向けに設定して使用する。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class GradientMapFileSelectorAttribute : PropertyEditorAttribute2
{
    public const string CustomFileGroupKey = "Community.GradientMap";

    const string FilterName = "Gradient Files (*.grd;*.png;*.jpg)";
    const string FilterPattern = "*.grd;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tiff";

    static readonly IList<IFileSelectorThumbnailLoader> thumbnailLoaders = new List<IFileSelectorThumbnailLoader>
    {
        new GradientMapThumbnailLoader(),
    };
    static readonly Lock registerLock = new();
    static bool registered;

    public GradientMapFileSelectorAttribute()
    {
        PropertyEditorSize = PropertyEditorSize.FullWidth;
    }

    public override FrameworkElement Create()
    {
        return new FileSelector();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
    {
        EnsureCustomGroupRegistered();

        var editor = (FileSelector)control;
        editor.CustomFileGroupKey = CustomFileGroupKey;
        editor.FileType = FileType.None;
        editor.ShowThumbnail = true;
        editor.ThumbnailLoaders = thumbnailLoaders;
        editor.Filter = FilterPattern;
        editor.FilterName = FilterName;

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
            if (itemProperty.PropertyInfo.GetCustomAttribute<GradientMapFileSelectorAttribute>() is null)
                continue;
            yield return itemProperty;
        }
    }

    static void EnsureCustomGroupRegistered()
    {
        if (registered)
            return;
        lock (registerLock)
        {
            if (registered)
                return;
            FileSettings.Default.Groups.RegisterCustomGroup(
                key: CustomFileGroupKey,
                fileType: FileType.None,
                customFilter: $"{FilterName}|{FilterPattern}");
            registered = true;
        }
    }
}
