namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public sealed class AddressBarBreadcrumbSegment(AddressBarBreadcrumbSegmentKind kind, string displayText, string? fullPath)
    {
        public AddressBarBreadcrumbSegmentKind Kind { get; } = kind;
        public string DisplayText { get; } = displayText ?? string.Empty;
        public string? FullPath { get; } = fullPath;

        public static AddressBarBreadcrumbSegment CreateEllipsis() => new(AddressBarBreadcrumbSegmentKind.Ellipsis, "...", null);
    }
}
