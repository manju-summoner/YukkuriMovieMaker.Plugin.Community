using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenSettings : SettingsBase<PenSettings>
    {
        public const double MinLayerPanelWidth = 280;
        public const double MinLayerListHeight = 80;
        public const double MinLayerPropertiesHeight = 52;

        public override SettingsCategory Category => SettingsCategory.None;

        public override string Name => "Pen";

        public override bool HasSettingView => false;

        public override object SettingView => throw new NotImplementedException();

        public PenMode PenMode { get => penMode; set => Set(ref penMode, value); }
        PenMode penMode = PenMode.Pen;

        public PenSelectionKind SelectionKind { get => selectionKind; set => Set(ref selectionKind, value); }
        PenSelectionKind selectionKind = PenSelectionKind.Lasso;

        public double LayerPanelWidth { get => layerPanelWidth; set => Set(ref layerPanelWidth, Math.Max(MinLayerPanelWidth, value)); }
        double layerPanelWidth = 300;

        public double LayerPropertiesHeight { get => layerPropertiesHeight; set => Set(ref layerPropertiesHeight, Math.Max(MinLayerPropertiesHeight, value)); }
        double layerPropertiesHeight = 220;

        public PenStyleSettings PenStyle { get; } = new() { StrokeColor = Colors.White };
        public PenStyleSettings HighlighterStyle { get; } = new() { StrokeColor = Colors.Yellow };
        public PenStyleSettings PencilStyle { get; } = new() { StrokeColor = Colors.White, StrokeThickness = 6 };
        public EraserStyleSettings EraserStyle { get; } = new ();
        public FillStyleSettings FillStyle { get; } = new();

        public override void Initialize()
        {

        }
    }
}
