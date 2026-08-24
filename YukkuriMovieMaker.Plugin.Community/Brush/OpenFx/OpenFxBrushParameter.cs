using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Plugin.Brush;
using YukkuriMovieMaker.Plugin.Community.Commons;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;

namespace YukkuriMovieMaker.Plugin.Community.Brush.OpenFx
{
    /// <summary>
    /// ブラシ「OpenFX」のパラメータ。
    /// 出力画像サイズ（OFXのプロジェクトサイズ＝RoDの基準）は幅・高さで指定し、
    /// 単位は線形グラデーションブラシと同じ流儀でピクセル／割合（塗り先の矩形に対する%）を切り替えられる。
    /// 既定は割合100%×100%＝塗り先にぴったり。
    /// プラグインのパラメータUIは選択したプラグインのdescribe結果から動的に構築する
    /// （ImmutableListの再代入でプロパティエディタが再構築されるVOICEPEAK方式。OpenFxShapeParameterと同じ流儀）
    /// </summary>
    internal class OpenFxBrushParameter : ScalableDrawingBrushParameterBase, IOpenFxPluginHost
    {
        // ネストされたプロパティのOrderは「親（Brush.Parameterブロック）のOrder＋自分のOrder」で解決されるため、
        // 負のOrderにするとブラシの「種類」コンボより上に表示されてしまう（図形「OpenFX」と同じ制約）。
        // 静的パラメータは1〜、動的パラメータ（RebuildParametersのstartOrder）は10〜の正のOrderとし、
        // 基底クラスの共通変換パラメータ（X=100〜反転=500）の手前に並べる

        /// <summary>
        /// プラグインバイナリ（.ofx）のパス
        /// </summary>
        [Display(Name = nameof(Texts.OpenFxBrushPluginName), Description = nameof(Texts.OpenFxBrushPluginDesc), Order = 1, ResourceType = typeof(Texts))]
        [OpenFxPluginSelector(OpenFxPluginListKind.Generator)]
        public string PluginPath { get => pluginPath; set => Set(ref pluginPath, value); }
        string pluginPath = string.Empty;

        /// <summary>
        /// プラグインの識別子（OfxPlugin.pluginIdentifier）
        /// </summary>
        public string PluginId { get => pluginId; set => Set(ref pluginId, value); }
        string pluginId = string.Empty;

        public string PluginName { get => pluginName; set => Set(ref pluginName, value); }
        string pluginName = string.Empty;

        /// <summary>
        /// 幅・高さの指定単位（線形グラデーションブラシと同じ流儀）。
        /// 割合は塗り先の矩形サイズに対する%で、既定の100%×100%は塗り先にぴったり
        /// </summary>
        [Display(Name = nameof(Texts.OpenFxBrushCoordinateModeName), Description = nameof(Texts.OpenFxBrushCoordinateModeDesc), Order = 2, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public CoordinateMode CoordinateMode { get => coordinateMode; set => Set(ref coordinateMode, value); }
        CoordinateMode coordinateMode = CoordinateMode.Relative;

        [Display(Name = nameof(Texts.OpenFxBrushWidthName), Description = nameof(Texts.OpenFxBrushWidthDesc), Order = 3, ResourceType = typeof(Texts))]
        [ConditionalUnitAnimationSlider("F1", nameof(CoordinateMode), 0d, 500d)]
        public Animation Width { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.OpenFxBrushHeightName), Description = nameof(Texts.OpenFxBrushHeightDesc), Order = 4, ResourceType = typeof(Texts))]
        [ConditionalUnitAnimationSlider("F1", nameof(CoordinateMode), 0d, 500d)]
        public Animation Height { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        // 繰り返し方法は他ブラシ（シーンブラシ等）と同じ位置（縦横比400と反転500の間）に並べる
        [Display(Name = nameof(Texts.OpenFxBrushExtendModeXName), Description = nameof(Texts.OpenFxBrushExtendModeXName), Order = 450, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ExtendMode ExtendModeX { get => extendModeX; set => Set(ref extendModeX, value); }
        ExtendMode extendModeX = ExtendMode.Wrap;

        [Display(Name = nameof(Texts.OpenFxBrushExtendModeYName), Description = nameof(Texts.OpenFxBrushExtendModeYName), Order = 450, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public ExtendMode ExtendModeY { get => extendModeY; set => Set(ref extendModeY, value); }
        ExtendMode extendModeY = ExtendMode.Wrap;

        /// <summary>
        /// プラグインのパラメータ（選択中プラグインのdescribe結果から構築）。
        /// リストの再代入によりプロパティエディタが再構築される
        /// </summary>
        [Display(Name = null, Description = null, AutoGenerateField = true)]
        public ImmutableList<OfxParameterBase> Parameters
        {
            get => parameters;
            set
            {
                // Undo/Redoの購読はUndoRedoable.SetがIEnumerable要素に対して自動で付け替える
                var oldParameters = parameters;
                if (Set(ref parameters, value))
                {
                    foreach (var removed in oldParameters)
                        removed.PropertyChanged -= Parameter_PropertyChanged;
                    foreach (var added in parameters)
                        added.PropertyChanged += Parameter_PropertyChanged;
                }
            }
        }
        ImmutableList<OfxParameterBase> parameters = [];

        void Parameter_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            // 名前を"Parameters"と一致させるとエディタ全体の再構築が走るため、ドット付きの別名で通知する
            OnPropertyChanged($"{nameof(Parameters)}.{e.PropertyName}");
        }

        /// <summary>
        /// プラグインを選択し、パラメータリストを再構築する（セレクターUIから呼ばれる）
        /// </summary>
        public void SelectPlugin(OpenFxPluginInfo info)
        {
            if (string.Equals(PluginPath, info.BinaryPath, StringComparison.OrdinalIgnoreCase)
                && string.Equals(PluginId, info.Identifier, StringComparison.OrdinalIgnoreCase))
                return;
            PluginPath = info.BinaryPath;
            PluginId = info.Identifier;
            PluginName = info.Name;
            RebuildParameters();
        }

        /// <summary>
        /// 選択中プラグインのdescribe結果からパラメータリストを構築し直す。
        /// 同名・同型のパラメータの値は引き継がれる
        /// </summary>
        internal void RebuildParameters()
        {
            if (string.IsNullOrEmpty(PluginPath) || string.IsNullOrEmpty(PluginId))
            {
                Parameters = [];
                return;
            }
            try
            {
                var plugin = OpenFxPluginScanner.LoadPlugin(PluginPath, PluginId)
                    ?? throw new InvalidOperationException($"プラグインが見つかりません。id={PluginId} path={PluginPath}");
                var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextGenerator);
                // 静的パラメータ（プラグイン欄・単位・サイズ系＝Order 1〜4）の後ろ、
                // 共通変換パラメータ（X=100〜）の手前に並ぶよう、動的パラメータは10起点にする
                // （+1刻みのため91個以上あるパラメータはX=100以降へ食い込んで共通変換と交互に並ぶ。
                //   図形「OpenFX」とUI配置を揃えることを優先した既知の割り切り）
                Parameters = OpenFxParameterFactory.Create(descriptor, Parameters, startOrder: 10);
            }
            catch (Exception e)
            {
                Log.Default.Write($"OFXプラグインのパラメータ構築に失敗しました。id={PluginId} path={PluginPath}", e);
                // 旧プラグインのパラメータを残すと、UIと選択中プラグインが食い違ったまま
                // 存在しない名前への書き込みが黙って空振りし続けるため、読み込み失敗を可視化する
                Parameters = [];
            }
        }

        /// <summary>
        /// 出力画像サイズ（OFXのプロジェクトサイズ）を求める。
        /// ピクセル指定では幅・高さの値をそのまま、割合指定では塗り先の矩形サイズに対する%として解釈する
        /// （100%×100%＝塗り先と同じサイズ）。
        /// 単位はUIスレッドから切り替わり得るため、呼び出し側が1回読み取った値を渡す
        /// （プロパティを再読みすると同一フレーム内で配置経路と食い違う）
        /// </summary>
        internal (double Width, double Height) GetOutputSize(CoordinateMode coordinateMode, int frame, int length, int fps, double boundsWidth, double boundsHeight)
        {
            var width = Width.GetValue(frame, length, fps);
            var height = Height.GetValue(frame, length, fps);
            return coordinateMode is CoordinateMode.Relative
                ? (boundsWidth * width / 100d, boundsHeight * height / 100d)
                : (width, height);
        }

        public override IBrushSource CreateBrush(IGraphicsDevicesAndContext devices)
        {
            return new OpenFxBrushSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
            => base.GetAnimatables().Concat([Width, Height]).Concat(Parameters);
    }
}
