using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;
using LocalizationTexts = YukkuriMovieMaker.Resources.Localization.Texts;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXトランジションを単一アイテムの登場・退場に適用する映像エフェクト「場面切り替え（OpenFX）」。
    /// 本体の「場面切り替え」（ルール画像。InOutTransitionEffect）に準拠したUI・時間制御で、
    /// 遷移元・遷移先の一方を透過画像としてトランジションコンテキストのプラグインを駆動する。
    /// パラメータUIは選択したプラグインのdescribe結果から動的に構築する（OpenFxVideoEffectと同じ方式）
    /// </summary>
    [VideoEffect(nameof(Texts.OpenFxInOutTransitionEffectName), [VideoEffectCategories.Transition], ["OpenFX", "OFX", "シーンチェンジ", "トランジション", "ワイプ", "scene change", "transition", "wipe"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class OpenFxInOutTransitionEffect : VideoEffectBase, IOpenFxPluginHost
    {
        public override string Label => string.IsNullOrEmpty(PluginName) ? Texts.OpenFxInOutTransitionEffectName : $"{Texts.OpenFxInOutTransitionEffectName} {PluginName}";

        /// <summary>
        /// プラグインバイナリ（.ofx）のパス
        /// </summary>
        [Display(GroupName = nameof(Texts.OpenFxInOutTransitionEffectName), Name = nameof(Texts.OpenFxEffectPluginName), Description = nameof(Texts.OpenFxEffectPluginDesc), Order = 0, ResourceType = typeof(Texts))]
        [OpenFxPluginSelector(OpenFxPluginListKind.Transition)]
        public string PluginPath { get => pluginPath; set => Set(ref pluginPath, value); }
        string pluginPath = string.Empty;

        /// <summary>
        /// プラグインの識別子（OfxPlugin.pluginIdentifier）
        /// </summary>
        public string PluginId { get => pluginId; set => Set(ref pluginId, value); }
        string pluginId = string.Empty;

        public string PluginName { get => pluginName; set => Set(ref pluginName, value, nameof(PluginName), nameof(Label)); }
        string pluginName = string.Empty;

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

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutTransitionEffectIsInEffectName), Description = nameof(LocalizationTexts.InOutTransitionEffectIsInEffectDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [ToggleSlider]
        public bool IsInEffect { set => Set(ref isInEffect, value); get => isInEffect; }
        bool isInEffect = true;

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutTransitionEffectIsReversedInEffectName), Description = nameof(LocalizationTexts.InOutTransitionEffectIsReversedInEffectDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [ToggleSlider]
        public bool IsReversedInEffect { set => Set(ref isReversedInEffect, value); get => isReversedInEffect; }
        bool isReversedInEffect = false;

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutTransitionEffectIsOutEffectName), Description = nameof(LocalizationTexts.InOutTransitionEffectIsOutEffectDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [ToggleSlider]
        public bool IsOutEffect { set => Set(ref isOutEffect, value); get => isOutEffect; }
        bool isOutEffect = true;

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutTransitionEffectIsReversedOutEffectName), Description = nameof(LocalizationTexts.InOutTransitionEffectIsReversedOutEffectDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [ToggleSlider]
        public bool IsReversedOutEffect { set => Set(ref isReversedOutEffect, value); get => isReversedOutEffect; }
        bool isReversedOutEffect = false;

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutTransitionEffectEffectTimeSecondsName), Description = nameof(LocalizationTexts.InOutTransitionEffectEffectTimeSecondsDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [TextBoxSlider("F2", nameof(LocalizationTexts.SecUnit), 0, 0.5, ResourceType = typeof(LocalizationTexts))]
        [Range(0d, YMM4Constants.VeryLargeValue)]
        [DefaultValue(1d)]
        public double EffectTimeSeconds { set => Set(ref effectTimeSeconds, value); get => effectTimeSeconds; }
        double effectTimeSeconds = 1d;

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutEffectBaseEasingTypeName), Description = nameof(LocalizationTexts.InOutEffectBaseEasingTypeDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [EnumComboBox]
        public EasingType EasingType { set => Set(ref easingType, value); get => easingType; }
        EasingType easingType = EasingType.Linear;

        [Display(GroupName = nameof(LocalizationTexts.InOutGroupName), Name = nameof(LocalizationTexts.InOutEffectBaseEasingModeName), Description = nameof(LocalizationTexts.InOutEffectBaseEasingModeDesc), Order = 100, ResourceType = typeof(LocalizationTexts))]
        [EnumComboBox]
        public EasingMode EasingMode { set => Set(ref easingMode, value); get => easingMode; }
        EasingMode easingMode = EasingMode.InOut;

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
                var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextTransition);
                // 進行度（Transition）はホストが毎フレーム設定するためUIから除外する
                Parameters = OpenFxParameterFactory.Create(descriptor, Parameters, [OfxConstants.ImageEffectTransitionParamName]);
            }
            catch (Exception e)
            {
                Log.Default.Write($"OFXプラグインのパラメータ構築に失敗しました。id={PluginId} path={PluginPath}", e);
                // 旧プラグインのパラメータを残すと、UIと選択中プラグインが食い違ったまま
                // 存在しない名前への書き込みが黙って空振りし続けるため、読み込み失敗を可視化する
                Parameters = [];
            }
        }

        public override IVideoEffectProcessor CreateVideoEffect(IGraphicsDevicesAndContext devices)
        {
            return new OpenFxInOutTransitionEffectProcessor(devices, this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => Parameters;
    }
}
