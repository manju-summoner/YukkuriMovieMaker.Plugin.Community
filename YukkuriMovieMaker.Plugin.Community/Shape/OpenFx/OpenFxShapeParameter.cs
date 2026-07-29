using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.ItemEditor;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Shape.OpenFx
{
    /// <summary>
    /// 図形「OpenFX」のパラメータ。
    /// 出力サイズ（OFXのプロジェクトサイズ＝RoDの基準）はユーザー指定で、
    /// 「サイズとアスペクト比」または「幅と高さ」の2方式で指定できる（前者は幅・高さへ変換して使う）。
    /// プラグインのパラメータUIは選択したプラグインのdescribe結果から動的に構築する
    /// （ImmutableListの再代入でプロパティエディタが再構築されるVOICEPEAK方式。OpenFxVideoEffectと同じ流儀）
    /// </summary>
    internal class OpenFxShapeParameter : ShapeParameterBase, IOpenFxPluginHost, IResizableShapeParameter
    {
        // ネストされたプロパティのOrderは「親（ShapeParameterブロック）のOrder＋自分のOrder」で解決されるため、
        // 映像エフェクトのような負のOrderにすると図形アイテムの「種類」コンボより上に表示されてしまう。
        // 静的パラメータは1〜、動的パラメータ（RebuildParametersのstartOrder）は10〜の正のOrderで種類の下に並べる

        /// <summary>
        /// プラグインバイナリ（.ofx）のパス
        /// </summary>
        [Display(Name = nameof(Texts.OpenFxShapePluginName), Description = nameof(Texts.OpenFxShapePluginDesc), Order = 1, ResourceType = typeof(Texts))]
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

        [Display(Name = nameof(Texts.OpenFxShapeSizeModeName), Description = nameof(Texts.OpenFxShapeSizeModeDesc), Order = 2, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public OpenFxShapeSizeMode SizeMode { get => sizeMode; set => Set(ref sizeMode, value); }
        OpenFxShapeSizeMode sizeMode = OpenFxShapeSizeMode.SizeAspect;

        [Display(Name = nameof(Texts.OpenFxShapeSizeName), Description = nameof(Texts.OpenFxShapeSizeDesc), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0, 500)]
        [OpenFxShapeSizeModeDisplaySwitch(OpenFxShapeSizeMode.SizeAspect)]
        public Animation Size { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.OpenFxShapeAspectRateName), Description = nameof(Texts.OpenFxShapeAspectRateDesc), Order = 4, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "", -100d, 100d)]
        [OpenFxShapeSizeModeDisplaySwitch(OpenFxShapeSizeMode.SizeAspect)]
        public Animation AspectRate { get; } = new Animation(0, -100, 100);

        [Display(Name = nameof(Texts.OpenFxShapeWidthName), Description = nameof(Texts.OpenFxShapeWidthDesc), Order = 5, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 500d)]
        [OpenFxShapeSizeModeDisplaySwitch(OpenFxShapeSizeMode.WidthHeight)]
        public Animation Width { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.OpenFxShapeHeightName), Description = nameof(Texts.OpenFxShapeHeightDesc), Order = 6, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", 0d, 500d)]
        [OpenFxShapeSizeModeDisplaySwitch(OpenFxShapeSizeMode.WidthHeight)]
        public Animation Height { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);

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

        public OpenFxShapeParameter(SharedDataStore? sharedData) : base(sharedData)
        {
        }

        [Obsolete("JsonSerializer用")]
        public OpenFxShapeParameter() : this(null)
        {
        }

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
                // 静的パラメータ（プラグイン欄・サイズ系＝Order 1〜6）の後ろに並ぶよう、動的パラメータは10起点にする
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
        /// サイズとアスペクト比の変換は本体の図形（SizeAndAspectShapeParameterBase）と同じ式
        /// </summary>
        internal (double Width, double Height) GetOutputSize(int frame, int length, int fps)
        {
            if (SizeMode is OpenFxShapeSizeMode.WidthHeight)
            {
                return (Width.GetValue(frame, length, fps), Height.GetValue(frame, length, fps));
            }
            else
            {
                var size = Size.GetValue(frame, length, fps);
                var aspect = AspectRate.GetValue(frame, length, fps);
                var width = size * (1.0 - Math.Max(0, aspect / 100d));
                var height = size * (1.0 + Math.Min(0, aspect / 100d));
                return (width, height);
            }
        }

        // 本体の図形（SizeAndAspectShapeParameterBase.Resize）と同じ実装。挙動を変える場合は両方を揃えること
        public void Resize(double xScale, double yScale)
        {
            if (SizeMode is OpenFxShapeSizeMode.SizeAspect)
            {
                var (width, height) = GetOutputSize(0, 1, 30);
                var size = Size.GetValue(0, 1, 30);

                var newWidth = Math.Max(1, width * xScale);
                var newHeight = Math.Max(1, height * yScale);
                var newSize = Math.Max(1, Math.Max(newWidth, newHeight));

                // サイズの倍率はフレーム0の縦横比から求めた単一値のため、縦横比がアニメーションしていると
                // フレーム0以外のサイズはずれる（SizeとAspectRateのキーフレーム構造は独立でフレーム毎の対応が取れない既知の制限）。
                // フレーム0のサイズが0のときは倍率（newSize / size）が0除算で定まらず、
                // フレーム0のサイズが1px未満のときはnewSize側だけが1px床で持ち上がり倍率が1 / sizeへ爆発する
                // （例: size=0.001の縮小で1000倍）ため、これらのケースではフレーム0の縦横比から操作本来の倍率
                // （サイズ→最大寸法の変換式にxScale/yScaleを掛けたもの）を再構成して適用し、
                // 1px床を下回ったままだと復帰できないフレーム0の値のみ1px（newSize。既定のGetOutputSizeではこの経路で常に1）を与える
                // （フレーム0以外の意図的な0キーフレームは0×倍率=0のまま維持される。
                //   なお、この経路ではフレーム0のみ1pxへ復帰するため、フレーム0と他キーフレームの比は保存されない）
                var aspect = AspectRate.GetValue(0, 1, 30);
                var sizeScale = Math.Max(xScale * (1 - Math.Max(0, aspect / 100)), yScale * (1 + Math.Min(0, aspect / 100)));
                // sizeが1px以上のときは常に通常経路（newSize / size）を使う（v4.54までと同一挙動）。
                // 1px床を下回る縮小では倍率がnewSize / size = 1 / sizeとなって全キーフレームが比を保って一様に縮み、
                // 1px床到達後は倍率1の不動点になる（操作本来の倍率を適用するとフレーム0だけが1pxに留まり、
                // 操作の繰り返しで他のキーフレームだけが無制限に縮み続ける）。
                // sizeが1px未満のときは1 / sizeが拡大（倍率爆発）へ転じるため通常経路は使えず、
                // 再構成した操作本来の倍率を適用する（フレーム0が1pxへ復帰するため、以降の操作は1px以上の経路になる）
                if (size <= 0 || (size < 1 && size * sizeScale < 1))
                {
                    // 倍率0以下の退化した操作（Resize(0, 0)等）では、他のキーフレームが復帰不能な0へ潰れないよう倍率1（維持）とする
                    if (sizeScale <= 0)
                        sizeScale = 1;
                    if (Size.AnimationType is AnimationType.移動量指定)
                    {
                        var value = Size.Values[0];
                        var scaled = value.Value * sizeScale;
                        value.Value = scaled < 1 ? newSize : scaled;
                    }
                    else
                    {
                        var values = Size.Values;
                        for (var i = 0; i < values.Count; i++)
                        {
                            var scaled = values[i].Value * sizeScale;
                            values[i].Value = IsFrame0Value(Size, i) && scaled < 1 ? newSize : scaled;
                        }
                    }
                }
                else
                {
                    Size.MultiplyToEachValues(newSize / size);
                }

                // 縦横比は0を跨いだり符号が反転したりするため単一倍率の乗算では変換できず、
                // キーフレーム毎に幅/高さ比へ換算してから変換する。
                // フレーム0の値のみ、newWidth/newHeightの1px床と同等になるよう寸法1px相当（±maxAbsAspect）でクランプする
                // （±100のまま維持すると図形全体が不可視になり以後のドラッグで復帰できなくなるため。
                //   フレーム0以外のキーフレームはサイズが異なりうるためクランプせず、±100＝意図的な潰し表現も保持する）
                var ratioScale = xScale / yScale;
                var maxAbsAspect = 100 * (1 - 1 / newSize);
                if (AspectRate.AnimationType is AnimationType.移動量指定)
                {
                    var value = AspectRate.Values[0];
                    value.Value = ResizeAspect(value.Value, ratioScale, maxAbsAspect);
                }
                else
                {
                    var values = AspectRate.Values;
                    for (var i = 0; i < values.Count; i++)
                        values[i].Value = ResizeAspect(values[i].Value, ratioScale, IsFrame0Value(AspectRate, i) ? maxAbsAspect : 100);
                }
            }
            else
            {
                Width.MultiplyToEachValues(xScale);
                Height.MultiplyToEachValues(yScale);
            }
        }

        /// <summary>
        /// フレーム0の評価（GetValue(0, ...)）に指定インデックスの値が寄与するかを返す。
        /// 反復移動かつSpan==0のとき、フレーム0は2点目（to）の値で評価される。
        /// ランダム移動は先頭2値のランダム補間で評価されるため、両方が寄与する
        /// </summary>
        static bool IsFrame0Value(Animation animation, int index)
        {
            if (animation.AnimationType is AnimationType.ランダム移動)
                return index <= 1;
            if (animation.AnimationType is AnimationType.反復移動 && animation.Span is 0 && animation.Values.Count > 1)
                return index == 1;
            return index == 0;
        }

        /// <summary>
        /// 縦横比(-100～100)を幅/高さ比に換算し、比率をratioScale倍した結果の縦横比を返す。
        /// 結果は±maxAbsAspect（寸法1px相当）までにクランプする
        /// </summary>
        static double ResizeAspect(double aspect, double ratioScale, double maxAbsAspect)
        {
            aspect = Math.Clamp(aspect, -100, 100);
            var ratio = aspect >= 0 ? 1 - aspect / 100 : 1 / (1 + aspect / 100);
            var newRatio = ratio * ratioScale;
            // aspect=±100は比率0または+Infinityを経由するため、倍率0（またはxScale=yScale=0によるratioScale=NaN）
            // との組み合わせで0×Infinity=NaNになる。旧実装では両寸法とも1px床に落ちるケースなので正方形として扱う
            if (double.IsNaN(newRatio))
                newRatio = 1;
            var newAspect = newRatio <= 1 ? (1 - newRatio) * 100 : (1 / newRatio - 1) * 100;
            return Math.Clamp(newAspect, -maxAbsAspect, maxAbsAspect);
        }

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc) => [];
        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters) => [];

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new OpenFxShapeSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Size, AspectRate, Width, Height, .. Parameters];

        protected override void LoadSharedData(SharedDataStore sharedData)
        {
            var data = sharedData.Load<OpenFxShapeParameterSharedData>();
            if (data is null)
                return;
            // 退避データは複数回Loadされ得るため、動的パラメータの要素をストアと共有しないよう複製して取り込む
            data = Json.Json.GetClone(data) ?? data;
            SizeMode = data.SizeMode;
            Size.CopyFrom(data.Size);
            AspectRate.CopyFrom(data.AspectRate);
            Width.CopyFrom(data.Width);
            Height.CopyFrom(data.Height);
            PluginPath = data.PluginPath;
            PluginId = data.PluginId;
            PluginName = data.PluginName;
            // 図形種類の切り替えから戻ったときに設定値（キーフレーム含む）を失わないよう、退避済みのリストを取り込む
            Parameters = data.Parameters;
        }

        protected override void SaveSharedData(SharedDataStore storage)
        {
            // 動的パラメータのリスト参照をそのまま退避すると、要素に付けた PropertyChanged 購読が
            // 旧OpenFxShapeParameterのハンドラーごと退避リストへ残り続け、種類の行き来のたびに
            // 同じ要素へ購読が蓄積する。Jsonの深い複製で購読を持たない独立したコピーを退避する
            var data = new OpenFxShapeParameterSharedData(this);
            storage.Save(Json.Json.GetClone(data) ?? data);
        }
    }

    /// <summary>
    /// 図形種類の切り替え時に設定を引き継ぐための退避データ（Jsonの深い複製で保存する）
    /// </summary>
    internal class OpenFxShapeParameterSharedData
    {
        public OpenFxShapeSizeMode SizeMode { get; set; }
        public Animation Size { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation AspectRate { get; } = new Animation(0, -100, 100);
        public Animation Width { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public Animation Height { get; } = new Animation(100, 0, YMM4Constants.VeryLargeValue);
        public string PluginPath { get; set; } = string.Empty;
        public string PluginId { get; set; } = string.Empty;
        public string PluginName { get; set; } = string.Empty;
        public ImmutableList<OfxParameterBase> Parameters { get; set; } = [];

        public OpenFxShapeParameterSharedData()
        {
        }

        public OpenFxShapeParameterSharedData(OpenFxShapeParameter parameter)
        {
            SizeMode = parameter.SizeMode;
            Size.CopyFrom(parameter.Size);
            AspectRate.CopyFrom(parameter.AspectRate);
            Width.CopyFrom(parameter.Width);
            Height.CopyFrom(parameter.Height);
            PluginPath = parameter.PluginPath;
            PluginId = parameter.PluginId;
            PluginName = parameter.PluginName;
            Parameters = parameter.Parameters;
        }
    }

    internal enum OpenFxShapeSizeMode
    {
        [Display(Name = nameof(Texts.OpenFxShapeSizeModeSizeAspectName), Description = nameof(Texts.OpenFxShapeSizeModeSizeAspectName), ResourceType = typeof(Texts))]
        SizeAspect,
        [Display(Name = nameof(Texts.OpenFxShapeSizeModeWidthHeightName), Description = nameof(Texts.OpenFxShapeSizeModeWidthHeightName), ResourceType = typeof(Texts))]
        WidthHeight,
    }

    /// <summary>
    /// サイズ指定方法に応じてサイズ系パラメータの表示・非表示を切り替える
    /// （本体のSizeModeDisplaySwitchAttributeと同じ流儀）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    internal sealed class OpenFxShapeSizeModeDisplaySwitchAttribute(OpenFxShapeSizeMode sizeMode) : Attribute, ICustomVisibilityAttribute2
    {
        static readonly EqualsToVisibilityConverter converter = new();
        public OpenFxShapeSizeMode SizeMode { get; } = sizeMode;

        public Binding GetBinding(object item, object propertyOwner)
        {
            return new Binding(nameof(OpenFxShapeParameter.SizeMode)) { Converter = converter, Source = propertyOwner, ConverterParameter = SizeMode };
        }

        class EqualsToVisibilityConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return value is not OpenFxShapeSizeMode a || parameter is not OpenFxShapeSizeMode b
                    ? DependencyProperty.UnsetValue
                    : a == b ? Visibility.Visible : Visibility.Collapsed;
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
    }
}
