using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx;
using YukkuriMovieMaker.Plugin.Transition;
using OfxHostTexts = YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx.Texts;

namespace YukkuriMovieMaker.Plugin.Community.Transition.OpenFx
{
    /// <summary>
    /// OpenFX場面切り替えのパラメータ。
    /// パラメータUIは選択したプラグインのdescribeInContext（トランジションコンテキスト）結果から動的に構築する
    /// （OpenFxVideoEffectと同じVOICEPEAK方式）。進行度パラメータ（Transition）はホストが駆動するためUIには出さない
    /// </summary>
    internal sealed class OpenFxTransitionParameter : TransitionParameterBase, IOpenFxPluginHost
    {
        /// <summary>
        /// プラグインバイナリ（.ofx）のパス
        /// </summary>
        [Display(Name = nameof(OfxHostTexts.OpenFxEffectPluginName), Description = nameof(OfxHostTexts.OpenFxEffectPluginDesc), Order = 0, ResourceType = typeof(OfxHostTexts))]
        [OpenFxPluginSelector(OpenFxPluginListKind.Transition)]
        public string PluginPath { get => pluginPath; set => Set(ref pluginPath, value); }
        string pluginPath = string.Empty;

        /// <summary>
        /// プラグインの識別子（OfxPlugin.pluginIdentifier）
        /// </summary>
        public string PluginId { get => pluginId; set => Set(ref pluginId, value); }
        string pluginId = string.Empty;

        public string PluginName { get => pluginName; set => Set(ref pluginName, value); }
        string pluginName = string.Empty;

        [Display(Name = nameof(Texts.EasingTypeName), Description = nameof(Texts.EasingTypeDesc), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingType EasingType { get => easingType; set => Set(ref easingType, value); }
        EasingType easingType = EasingType.Linear;

        [Display(Name = nameof(Texts.EasingModeName), Description = nameof(Texts.EasingModeDesc), Order = 0, ResourceType = typeof(Texts))]
        [EnumComboBox]
        public EasingMode EasingMode { get => easingMode; set => Set(ref easingMode, value); }
        EasingMode easingMode = EasingMode.InOut;

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

        public override ITransitionSource CreateTransition(IGraphicsDevicesAndContext devices, ID2D1Image before, ID2D1Image after)
            => new OpenFxTransitionSource(devices, before, after, this);

        protected override IEnumerable<IAnimatable> GetAnimatables() => Parameters;
    }
}
