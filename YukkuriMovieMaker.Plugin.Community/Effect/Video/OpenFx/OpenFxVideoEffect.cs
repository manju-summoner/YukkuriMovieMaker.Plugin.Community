using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OpenFX（OFX）プラグインを映像エフェクトとしてホストするエフェクト。
    /// パラメータUIは選択したプラグインのdescribe結果から動的に構築する
    /// （ImmutableListの再代入でプロパティエディタが再構築されるVOICEPEAK方式）。
    /// </summary>
    [VideoEffect(nameof(Texts.OpenFxEffectName), [VideoEffectCategories.Filtering], ["OpenFX", "OFX", "プラグイン", "plugin"], IsAviUtlSupported = false, ResourceType = typeof(Texts))]
    internal class OpenFxVideoEffect : VideoEffectBase
    {
        public override string Label => string.IsNullOrEmpty(PluginName) ? Texts.OpenFxEffectName : $"{Texts.OpenFxEffectName} {PluginName}";

        /// <summary>
        /// プラグインバイナリ（.ofx）のパス
        /// </summary>
        [Display(GroupName = nameof(Texts.OpenFxEffectName), Name = nameof(Texts.OpenFxEffectPluginName), Description = nameof(Texts.OpenFxEffectPluginDesc), Order = -100, ResourceType = typeof(Texts))]
        [OpenFxPluginSelector]
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

        /// <summary>
        /// プラグインを選択し、パラメータリストを再構築する（セレクターUIから呼ばれる）
        /// </summary>
        internal void SelectPlugin(OpenFxPluginInfo info)
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
        /// 同名・同型のパラメータの値は引き継がれる。プラグインが読み込めない場合は現状を維持する
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
                var descriptor = plugin.DescribeInContext(OfxConstants.ImageEffectContextFilter);
                Parameters = OpenFxParameterFactory.Create(descriptor, Parameters);
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
            return new OpenFxVideoEffectProcessor(devices, this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => Parameters;
    }
}
