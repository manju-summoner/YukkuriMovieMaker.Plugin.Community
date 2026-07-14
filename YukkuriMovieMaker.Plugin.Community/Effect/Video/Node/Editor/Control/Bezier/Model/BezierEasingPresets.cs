using System.Windows;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Node.Editor.Control.Bezier.Model
{
    // カテゴリ定数
    public static class EasingCategory
    {
        public const string Linear = "Linear";
        public const string Sine = "Sine";
        public const string Quad = "Quad";
        public const string Cubic = "Cubic";
        public const string Quart = "Quart";
        public const string Quint = "Quint";
        public const string Expo = "Expo";
        public const string Circ = "Circ";
        public const string Back = "Back";
        public const string Bounce = "Bounce";
        public const string Elastic = "Elastic";
    }

    // ---- 抽象プリセット基底 ----
    public abstract class BezierEasingPresetBase
    {
        protected BezierEasingPresetBase(string name, string category)
        {
            Name = name;
            Category = category;
        }

        public string Name { get; }
        public string Category { get; }
        public abstract void Apply(BezierCurve curve);
    }

    // ---- シンプル（従来の2制御点）プリセット ----
    public class SimpleBezierEasingPreset : BezierEasingPresetBase
    {
        public SimpleBezierEasingPreset(string name, string category, Point p1, Point p2)
            : base(name, category)
        {
            P1 = p1;
            P2 = p2;
        }

        public Point P1 { get; }
        public Point P2 { get; }

        public override void Apply(BezierCurve curve)
        {
            var extraNodes = curve.Nodes.Where(n => !n.IsFixed).ToList();
            foreach (var n in extraNodes) curve.RemoveNode(n);

            var start = curve.Nodes.First(n => n.Position == new Point(0, 0));
            var end = curve.Nodes.First(n => n.Position == new Point(1, 1));

            start.Type = BezierNodeType.Corner;
            end.Type = BezierNodeType.Corner;

            start.OutHandle.Offset = P1 - new Point(0, 0);
            end.InHandle.Offset = P2 - new Point(1, 1);
        }
    }

    // ---- 複数ノード用の定義レコード ----
    public record BezierNodeDefinition(
        Point Position,
        Vector InOffset,
        Vector OutOffset,
        BezierNodeType NodeType
    );

    // ---- 複数ノードプリセット ----
    public class ComplexBezierEasingPreset : BezierEasingPresetBase
    {
        private readonly IReadOnlyList<BezierNodeDefinition> _nodeDefs;

        public ComplexBezierEasingPreset(string name, string category,
            IEnumerable<BezierNodeDefinition> nodeDefs)
            : base(name, category)
        {
            _nodeDefs = nodeDefs.ToList().AsReadOnly();
        }

        public override void Apply(BezierCurve curve)
        {
            // 全ノードをクリアし、固定ノードのみ再設定
            curve.Nodes.Clear();
            curve.Nodes.Add(new BezierNode(new Point(0, 0), true));
            curve.Nodes.Add(new BezierNode(new Point(1, 1), true));

            foreach (var def in _nodeDefs)
            {
                var node = new BezierNode(def.Position)
                {
                    Type = def.NodeType,
                    InHandle =
                    {
                        Offset = def.InOffset
                    },
                    OutHandle =
                    {
                        Offset = def.OutOffset
                    }
                };
                curve.AddNode(node);
            }
        }

        /// <summary>
        ///     キーフレーム配列から Catmull-Rom → 3次ベジェ へ変換し、プリセットを生成します。
        /// </summary>
        public static ComplexBezierEasingPreset FromKeyframes(
            string name, string category, params Point[] keyframes)
        {
            if (keyframes.Length < 2)
                throw new ArgumentException("少なくとも2つのキーフレームが必要です。");

            var defs = new List<BezierNodeDefinition>();
            var n = keyframes.Length;

            for (var i = 0; i < n; i++)
            {
                if (keyframes[i] == new Point(0, 0) || keyframes[i] == new Point(1, 1))
                    continue;

                var p0 = i > 0 ? keyframes[i - 1] : keyframes[i];
                var p1 = keyframes[i];
                var p2 = i < n - 1 ? keyframes[i + 1] : keyframes[i];

                var inOffset = (p1 - p0) / 6;
                var outOffset = (p2 - p1) / 6;

                defs.Add(new BezierNodeDefinition(p1, inOffset, outOffset, BezierNodeType.Smooth));
            }

            return new ComplexBezierEasingPreset(name, category, defs);
        }
    }

    // ====================================================
    // プリセット一覧
    // ====================================================
    public static class BezierEasingPresets
    {
        public static readonly IReadOnlyList<BezierEasingPresetBase> All =
        [
            // ---- Linear ----
            new SimpleBezierEasingPreset("linear", EasingCategory.Linear, new Point(0.25, 0.25), new Point(0.75, 0.75)),

            // ---- Sine ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Sine, new Point(0.12, 0), new Point(0.39, 0)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Sine, new Point(0.61, 1), new Point(0.88, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Sine, new Point(0.37, 0), new Point(0.63, 1)),

            // ---- Quad ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Quad, new Point(0.11, 0), new Point(0.5, 0)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Quad, new Point(0.5, 1), new Point(0.89, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Quad, new Point(0.45, 0), new Point(0.55, 1)),

            // ---- Cubic ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Cubic, new Point(0.32, 0), new Point(0.67, 0)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Cubic, new Point(0.33, 1), new Point(0.68, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Cubic, new Point(0.65, 0), new Point(0.35, 1)),

            // ---- Quart ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Quart, new Point(0.5, 0), new Point(0.75, 0)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Quart, new Point(0.25, 1), new Point(0.5, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Quart, new Point(0.76, 0), new Point(0.24, 1)),

            // ---- Quint ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Quint, new Point(0.64, 0), new Point(0.78, 0)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Quint, new Point(0.22, 1), new Point(0.36, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Quint, new Point(0.83, 0), new Point(0.17, 1)),

            // ---- Expo ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Expo, new Point(0.7, 0), new Point(0.84, 0)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Expo, new Point(0.16, 1), new Point(0.3, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Expo, new Point(0.87, 0), new Point(0.13, 1)),

            // ---- Circ ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Circ, new Point(0.55, 0), new Point(1, 0.45)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Circ, new Point(0, 0.55), new Point(0.45, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Circ, new Point(0.85, 0), new Point(0.15, 1)),

            // ---- Back ----
            new SimpleBezierEasingPreset("Ease In", EasingCategory.Back, new Point(0.36, 0), new Point(0.66, -0.56)),
            new SimpleBezierEasingPreset("Ease Out", EasingCategory.Back, new Point(0.34, 1.56), new Point(0.64, 1)),
            new SimpleBezierEasingPreset("Ease In Out", EasingCategory.Back, new Point(0.68, -0.6),
                new Point(0.32, 1.6)),

            // ---- Bounce ----
            new ComplexBezierEasingPreset(
                "Ease In",
                EasingCategory.Bounce,
                [
                    new BezierNodeDefinition(new Point(0.04, 0.016), new Vector(-0.007, -0.003),
                        new Vector(0.007, -0.002), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.08, 0.005), new Vector(-0.007, 0.002),
                        new Vector(0.017, 0.011), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.18, 0.068), new Vector(-0.017, -0.011),
                        new Vector(0.013, -0.009), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.26, 0.017), new Vector(-0.013, 0.009),
                        new Vector(0.033, 0.034), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.46, 0.220), new Vector(-0.033, -0.034),
                        new Vector(0.030, -0.033), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.64, 0.020), new Vector(-0.030, 0.033),
                        new Vector(0.020, 0.091), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.76, 0.564), new Vector(-0.020, -0.091),
                        new Vector(0.020, 0.055), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.88, 0.891), new Vector(-0.020, -0.055),
                        new Vector(0.020, 0.018), BezierNodeType.Corner)
                ]),

            new ComplexBezierEasingPreset(
                "Ease Out",
                EasingCategory.Bounce,
                [
                    new BezierNodeDefinition(new Point(0.12, 0.109), new Vector(-0.020, -0.018),
                        new Vector(0.020, 0.056), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.24, 0.436), new Vector(-0.020, -0.056),
                        new Vector(0.020, 0.092), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.36, 0.980), new Vector(-0.020, -0.092),
                        new Vector(0.030, -0.035), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.54, 0.750), new Vector(-0.030, 0.035),
                        new Vector(0.033, 0.034), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.74, 0.984), new Vector(-0.033, -0.034),
                        new Vector(0.013, -0.008), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.82, 0.9375), new Vector(-0.013, 0.008),
                        new Vector(0.017, 0.009), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.92, 0.993), new Vector(-0.017, -0.009),
                        new Vector(0.007, 0.001), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.96, 0.985), new Vector(-0.007, -0.001),
                        new Vector(0.007, 0.003), BezierNodeType.Smooth)
                ]),

            new ComplexBezierEasingPreset(
                "Ease In Out",
                EasingCategory.Bounce,
                [
                    new BezierNodeDefinition(new Point(0.02, 0.010), new Vector(-0.007, -0.002),
                        new Vector(0.007, 0.003), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.04, 0.003), new Vector(-0.007, -0.003),
                        new Vector(0.010, 0.004), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.10, 0.030), new Vector(-0.010, -0.004),
                        new Vector(0.020, 0.016), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.22, 0.124), new Vector(-0.020, -0.016),
                        new Vector(0.033, 0.046), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.42, 0.403), new Vector(-0.033, -0.046),
                        new Vector(0.013, 0.016), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.50, 0.500), new Vector(-0.013, -0.016),
                        new Vector(0.013, 0.016), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.58, 0.597), new Vector(-0.013, -0.016),
                        new Vector(0.033, 0.046), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.78, 0.876), new Vector(-0.033, -0.046),
                        new Vector(0.020, 0.016), BezierNodeType.Smooth),
                    new BezierNodeDefinition(new Point(0.90, 0.970), new Vector(-0.020, -0.016),
                        new Vector(0.010, 0.004), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.96, 0.997), new Vector(-0.010, -0.004),
                        new Vector(0.007, 0.001), BezierNodeType.Smooth)
                ]),

            // ---- Elastic ----
            new ComplexBezierEasingPreset(
                "Ease In",
                EasingCategory.Elastic,
                [
                    new BezierNodeDefinition(new Point(0.08, 0.002), new Vector(-0.020, 0.000),
                        new Vector(0.015, -0.001), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.27, -0.006), new Vector(-0.015, 0.001),
                        new Vector(0.020, 0.004), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.41, 0.016), new Vector(-0.020, -0.004),
                        new Vector(0.020, -0.010), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.57, -0.046), new Vector(-0.020, 0.010),
                        new Vector(0.020, 0.029), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.72, 0.131), new Vector(-0.020, -0.029),
                        new Vector(0.023, -0.084), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.86, -0.371), new Vector(-0.023, 0.084),
                        new Vector(0.023, 0.229), BezierNodeType.Corner)
                ]),

            new ComplexBezierEasingPreset(
                "Ease Out",
                EasingCategory.Elastic,
                [
                    new BezierNodeDefinition(new Point(0.14, 1.371), new Vector(-0.023, -0.229),
                        new Vector(0.023, -0.084), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.28, 0.869), new Vector(-0.023, 0.084),
                        new Vector(0.027, 0.029), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.44, 1.046), new Vector(-0.027, -0.029),
                        new Vector(0.025, -0.011), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.59, 0.980), new Vector(-0.025, 0.011),
                        new Vector(0.023, 0.005), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.73, 1.008), new Vector(-0.023, -0.005),
                        new Vector(0.025, -0.002), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.88, 0.998), new Vector(-0.025, 0.002),
                        new Vector(0.020, 0.001), BezierNodeType.Corner)
                ]),

            new ComplexBezierEasingPreset(
                "Ease In Out",
                EasingCategory.Elastic,
                [
                    new BezierNodeDefinition(new Point(0.06, 0.001), new Vector(-0.020, 0.000),
                        new Vector(0.020, -0.001), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.19, -0.005), new Vector(-0.020, 0.001),
                        new Vector(0.015, 0.005), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.29, 0.024), new Vector(-0.015, -0.005),
                        new Vector(0.015, -0.021), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.39, -0.100), new Vector(-0.015, 0.021),
                        new Vector(0.033, 0.200), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.61, 1.100), new Vector(-0.033, -0.200),
                        new Vector(0.015, -0.021), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.71, 0.976), new Vector(-0.015, 0.021),
                        new Vector(0.015, 0.005), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.81, 1.005), new Vector(-0.015, -0.005),
                        new Vector(0.015, -0.001), BezierNodeType.Corner),
                    new BezierNodeDefinition(new Point(0.91, 0.999), new Vector(-0.015, 0.001),
                        new Vector(0.015, 0.000), BezierNodeType.Corner)
                ])
        ];
    }
}