using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Audio.Vst3
{
    class Vst3SettingsViewModel : Bindable
    {
        public ObservableCollection<string> AdditionalDirectories { get; } = [.. Vst3Settings.Default.AdditionalPluginDirectories];

        // YMM4管理外のフォルダー（Program Files等）は存在しない場合は項目ごと非表示にする。
        // YMM4管理下のフォルダー（user\resources配下）は存在しなくても表示し、ボタンで作成できるようにする
        public IReadOnlyList<Vst3DefaultDirectoryViewModel> DefaultDirectories { get; } =
            [.. Vst3PluginScanner.GetDefaultDirectoryInfos()
                .Where(x => x.IsUserManaged || System.IO.Directory.Exists(x.Path))
                .Select(x => new Vst3DefaultDirectoryViewModel(x.Path, canCreate: x.IsUserManaged))];

        public string? SelectedDirectory { get => selectedDirectory; set => Set(ref selectedDirectory, value); }
        string? selectedDirectory;

        public ObservableCollection<Vst3EffectPluginInfo> Plugins { get; } = [];

        public bool IsScanning
        {
            get => isScanning;
            set
            {
                // スキャン完了は入力イベント起点でないため、明示的に通知しないとボタンが無効のまま残る。
                // CommandManagerはスレッド固有のため、UIスレッド外で再開された場合に備えてDispatcher経由で呼ぶ
                if (Set(ref isScanning, value, nameof(IsScanning), nameof(ScanStatusText)))
                    System.Windows.Application.Current?.Dispatcher.InvokeAsync(CommandManager.InvalidateRequerySuggested);
            }
        }
        bool isScanning;
        // 前回の保存結果の読み込みと再スキャンが交錯したとき、古い方の結果で一覧を上書きしないための世代番号
        long pluginListVersion;

        public string ScanStatusText => IsScanning
            ? Texts.Vst3SettingsScanningMessage
            : string.Format(Texts.Vst3SettingsPluginCountMessage, Plugins.Count);

        public ICommand AddDirectory { get; }
        public ICommand RemoveDirectory { get; }
        public ICommand Rescan { get; }
        public ICommand OpenDirectory { get; }

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
            OpenDirectory = new ActionCommand(
                item => item is Vst3DefaultDirectoryViewModel directory
                    && (directory.CanCreate || System.IO.Directory.Exists(directory.Path)),
                item =>
                {
                    if (item is not Vst3DefaultDirectoryViewModel directory)
                        return;
                    try
                    {
                        if (directory.CanCreate)
                            System.IO.Directory.CreateDirectory(directory.Path);
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = directory.Path,
                            UseShellExecute = true,
                        });
                    }
                    catch (Exception e)
                    {
                        Log.Default.Write("VST3フォルダーを開けませんでした。", e);
                    }
                });

            var cached = Vst3PluginScanner.CachedPlugins;
            if (cached is not null)
            {
                foreach (var plugin in cached)
                    Plugins.Add(plugin);
                OnPropertyChanged(nameof(ScanStatusText));
            }
            else
            {
                // 設定画面を開いただけではスキャンしない。前回の保存結果を表示し、更新は再スキャンボタンで行う。
                // スキャン中は同じロックの解放を待つため、UIスレッドを塞がないようバックグラウンドで読む
                _ = LoadKnownPluginsAsync();
            }
        }

        /// <summary>
        /// 標準のVST3フォルダー1件分の表示項目。
        /// CanCreateがtrueの場合、フォルダーが存在しなくても「開く」ボタンで作成できる
        /// </summary>
        public class Vst3DefaultDirectoryViewModel(string path, bool canCreate)
        {
            public string Path { get; } = path;
            public bool CanCreate { get; } = canCreate;
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
            var version = Interlocked.Increment(ref pluginListVersion);
            IsScanning = true;
            try
            {
                var plugins = await Task.Run(() =>
                {
                    Vst3PluginScanner.GetEffectPlugins(refresh: true);
                    // 再走査が失敗した場合の不完了な一覧で表示を空にせず、完了した結果か保存結果を表示する
                    return Vst3PluginScanner.GetKnownPlugins();
                });
                // 継続がUIスレッド外で再開されてもコレクション更新が失敗しないようにする
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (version == Interlocked.Read(ref pluginListVersion))
                        ReplacePlugins(plugins);
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

        async Task LoadKnownPluginsAsync()
        {
            var version = Interlocked.Increment(ref pluginListVersion);
            try
            {
                var plugins = await Task.Run(Vst3PluginScanner.GetKnownPlugins);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (version == Interlocked.Read(ref pluginListVersion))
                        ReplacePlugins(plugins);
                });
            }
            catch (Exception e)
            {
                Log.Default.Write("VST3プラグイン一覧の読み込みに失敗しました。", e);
            }
        }

        void ReplacePlugins(IReadOnlyList<Vst3EffectPluginInfo> plugins)
        {
            Plugins.Clear();
            foreach (var plugin in plugins)
                Plugins.Add(plugin);
            OnPropertyChanged(nameof(ScanStatusText));
        }
    }
}
