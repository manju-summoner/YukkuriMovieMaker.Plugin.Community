using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using YukkuriMovieMaker.Plugin;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    internal sealed class Vst3ScanCacheSettings : SettingsBase<Vst3ScanCacheSettings>
    {
        static readonly object saveLock = new();

        public override SettingsCategory Category => SettingsCategory.AudioEffect;
        public override string Name => "VST3 Scan Cache";
        public override bool HasSettingView => false;
        public override object? SettingView => null;

        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonProperty("environmentFingerprint")]
        public string? EnvironmentFingerprint { get; set; }

        [JsonProperty("entries")]
        public Dictionary<string, PersistentPluginScanCacheEntry<Vst3EffectPluginInfo>> Entries { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        public override void Initialize()
        {
            Entries ??= new(StringComparer.OrdinalIgnoreCase);
            if (!ReferenceEquals(Entries.Comparer, StringComparer.OrdinalIgnoreCase))
            {
                // Newtonsoftの既定では初期化子の比較子付き辞書へ流し込まれるため通常ここは通らない。
                // 辞書が置換生成される実装（ObjectCreationHandling.Replace等）へ変わった場合の保険（重複キーは後勝ち）
                var rebuilt = new Dictionary<string, PersistentPluginScanCacheEntry<Vst3EffectPluginInfo>>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in Entries)
                    rebuilt[pair.Key] = pair.Value;
                Entries = rebuilt;
            }
            foreach (var entry in Entries.Values.Where(x => x is not null))
                entry.Plugins ??= [];
        }

        public override void Save()
            => WithSaveLock(base.Save);

        internal static void WithSaveLock(Action save)
        {
            lock (saveLock)
                save();
        }
    }

    internal sealed class Vst3ScanCacheSettingsStorage : IPersistentPluginScanCacheStorage<Vst3EffectPluginInfo>
    {
        public PersistentPluginScanCacheState<Vst3EffectPluginInfo> Load()
        {
            var settings = Vst3ScanCacheSettings.Default;
            PersistentPluginScanCacheState<Vst3EffectPluginInfo>? state = null;
            Vst3ScanCacheSettings.WithSaveLock(() =>
            {
                state = new PersistentPluginScanCacheState<Vst3EffectPluginInfo>
                {
                    FormatVersion = settings.FormatVersion,
                    EnvironmentFingerprint = settings.EnvironmentFingerprint,
                    Entries = PersistentPluginScanCache.CloneEntries(settings.Entries),
                };
            });
            return state!;
        }

        public void Save(PersistentPluginScanCacheState<Vst3EffectPluginInfo> state)
        {
            var settings = Vst3ScanCacheSettings.Default;
            Vst3ScanCacheSettings.WithSaveLock(() =>
            {
                settings.FormatVersion = state.FormatVersion;
                settings.EnvironmentFingerprint = state.EnvironmentFingerprint;
                settings.Entries = PersistentPluginScanCache.CloneEntries(state.Entries);
                settings.Save();
            });
        }
    }
}
