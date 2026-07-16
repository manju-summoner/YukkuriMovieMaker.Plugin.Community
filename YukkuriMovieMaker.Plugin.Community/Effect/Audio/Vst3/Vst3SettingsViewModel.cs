using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    class Vst3SettingsViewModel : Bindable
    {
        public ObservableCollection<string> AdditionalDirectories { get; } = [.. Vst3Settings.Default.AdditionalPluginDirectories];

        public IReadOnlyList<string> DefaultDirectories { get; } = [.. Vst3PluginScanner.GetDefaultDirectories()];

        public string? SelectedDirectory { get => selectedDirectory; set => Set(ref selectedDirectory, value); }
        string? selectedDirectory;

        public ObservableCollection<Vst3EffectPluginInfo> Plugins { get; } = [];

        public bool IsScanning { get => isScanning; set => Set(ref isScanning, value, nameof(IsScanning), nameof(ScanStatusText)); }
        bool isScanning;

        public string ScanStatusText => IsScanning
            ? Texts.Vst3SettingsScanningMessage
            : string.Format(Texts.Vst3SettingsPluginCountMessage, Plugins.Count);

        public ICommand AddDirectory { get; }
        public ICommand RemoveDirectory { get; }
        public ICommand Rescan { get; }

        public Vst3SettingsViewModel()
        {
            AddDirectory = new ActionCommand(
                _ => !IsScanning,
                _ =>
                {
                    using var dialog = new System.Windows.Forms.FolderBrowserDialog();
                    if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                        return;
                    var path = dialog.SelectedPath;
                    if (string.IsNullOrWhiteSpace(path) || AdditionalDirectories.Contains(path, StringComparer.OrdinalIgnoreCase))
                        return;
                    AdditionalDirectories.Add(path);
                    SaveDirectories();
                    _ = RescanAsync();
                });
            RemoveDirectory = new ActionCommand(
                _ => !IsScanning && SelectedDirectory is not null,
                _ =>
                {
                    if (SelectedDirectory is not string directory)
                        return;
                    AdditionalDirectories.Remove(directory);
                    SaveDirectories();
                    _ = RescanAsync();
                });
            Rescan = new ActionCommand(
                _ => !IsScanning,
                _ => _ = RescanAsync());

            var cached = Vst3PluginScanner.CachedPlugins;
            if (cached is not null)
            {
                foreach (var plugin in cached)
                    Plugins.Add(plugin);
                OnPropertyChanged(nameof(ScanStatusText));
            }
            else
            {
                _ = RescanAsync();
            }
        }

        void SaveDirectories()
        {
            Vst3Settings.Default.AdditionalPluginDirectories = [.. AdditionalDirectories];
            Vst3Settings.Default.Save();
        }

        async Task RescanAsync()
        {
            if (IsScanning)
                return;
            IsScanning = true;
            try
            {
                var plugins = await Task.Run(() => Vst3PluginScanner.GetEffectPlugins(refresh: true));
                // 継続がUIスレッド外で再開されてもコレクション更新が失敗しないようにする
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Plugins.Clear();
                    foreach (var plugin in plugins)
                        Plugins.Add(plugin);
                });
            }
            catch (Exception e)
            {
                Log.Default.Write("VST3プラグインのスキャンに失敗しました。", e);
            }
            finally
            {
                IsScanning = false;
            }
        }
    }
}
