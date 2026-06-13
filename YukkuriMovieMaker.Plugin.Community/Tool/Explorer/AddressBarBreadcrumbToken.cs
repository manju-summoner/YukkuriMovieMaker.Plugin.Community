namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public sealed class AddressBarBreadcrumbToken
    {
        AddressBarBreadcrumbToken(AddressBarBreadcrumbTokenKind kind, AddressBarBreadcrumbSegment? segment, string? leftPath)
        {
            Kind = kind;
            Segment = segment;
            LeftPath = leftPath;
        }

        public AddressBarBreadcrumbTokenKind Kind { get; }
        public AddressBarBreadcrumbSegment? Segment { get; }

        /// <summary>
        /// 区切り用。左側フォルダのパス。nullの場合は非表示。
        /// </summary>
        public string? LeftPath { get; }

        public static AddressBarBreadcrumbToken CreateSegment(AddressBarBreadcrumbSegment segment)
            => new(AddressBarBreadcrumbTokenKind.Segment, segment, null);

        public static AddressBarBreadcrumbToken CreateSeparator(string? leftPath)
            => new(AddressBarBreadcrumbTokenKind.Separator, null, leftPath);
    }
}
