using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Media;

namespace YukkuriMovieMaker.Plugin.Community.Tool.Explorer
{
    public sealed class ExplorerAddressBarViewModel : INotifyPropertyChanged
    {
        string currentValue = string.Empty;

        FontFamily fontFamily = SystemFonts.MessageFontFamily;
        double fontSize = SystemFonts.MessageFontSize;
        FontStyle fontStyle = SystemFonts.MessageFontStyle;
        FontWeight fontWeight = SystemFonts.MessageFontWeight;
        FontStretch fontStretch = FontStretches.Normal;

        CancellationTokenSource? hasChildrenCancellationTokenSource;

        CancellationTokenSource? suggestionCancellationTokenSource;
        CancellationTokenSource? menuCancellationTokenSource;

        IReadOnlyList<AddressBarSuggestion> externalSuggestions = [];

        readonly List<AddressBarBreadcrumbSegment> allPathSegments = [];
        readonly List<AddressBarBreadcrumbSegment> omittedSegments = [];

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<AddressBarBreadcrumbToken> BreadcrumbTokens { get; } = [];
        public ObservableCollection<AddressBarSuggestion> Suggestions { get; } = [];
        public ObservableCollection<AddressBarMenuEntry> MenuEntries { get; } = [];

        public int MaxFileSystemSuggestions { get; set; } = 50;
        public int MaxExternalSuggestions { get; set; } = 50;

        public bool IsEditing
        {
            get;
            private set
            {
                if (field == value) return;
                field = value;
                OnPropertyChanged();
                UpdateSuggestionsOpenState();
            }
        }

        public string EditText
        {
            get;
            set
            {
                if (string.Equals(field, value, StringComparison.Ordinal)) return;
                field = value ?? string.Empty;
                OnPropertyChanged();

                if (IsEditing)
                    _ = UpdateSuggestionsAsync(field);
            }
        } = string.Empty;

        public bool IsSuggestionsOpen
        {
            get => field;
            private set
            {
                if (field == value) return;
                field = value;
                OnPropertyChanged();
            }
        }

        public AddressBarSuggestion? SelectedSuggestion
        {
            get;
            set
            {
                if (ReferenceEquals(field, value)) return;
                field = value;
                OnPropertyChanged();
            }
        }

        public double AvailableWidth
        {
            get;
            set
            {
                if (Math.Abs(field - value) < 0.5) return;
                field = value;
            }
        }

        public double PixelsPerDip
        {
            get;
            set
            {
                if (Math.Abs(field - value) < 0.001) return;
                field = value;
            }
        } = 1.0;
        public bool HasCurrentSubfolders
        {
            get;
            private set
            {
                if (field == value) return;
                field = value;
                OnPropertyChanged();
                UpdateBreadcrumbTokens();
            }
        }

        public bool CanOpenEllipsisMenu => omittedSegments.Count > 0;

        public void SetFontInfo(FontFamily family, double size, FontStyle style, FontWeight weight, FontStretch stretch)
        {
            fontFamily = family ?? SystemFonts.MessageFontFamily;
            fontSize = size;
            fontStyle = style;
            fontWeight = weight;
            fontStretch = stretch;
        }

        public void SetExternalSuggestions(IEnumerable<AddressBarSuggestion>? suggestions)
        {
            externalSuggestions = suggestions?.ToArray() ?? [];
            RefreshSuggestions();
        }

        public void SetCurrentValue(string? value)
        {
            currentValue = value ?? string.Empty;

            if (!IsEditing)
                EditText = currentValue;

            RebuildSegmentsFromValue(currentValue);
            UpdateBreadcrumbTokens();

            _ = UpdateHasCurrentSubfoldersAsync();
        }

        async Task UpdateHasCurrentSubfoldersAsync()
        {
            hasChildrenCancellationTokenSource?.Cancel();
            hasChildrenCancellationTokenSource?.Dispose();
            hasChildrenCancellationTokenSource = new CancellationTokenSource();
            var token = hasChildrenCancellationTokenSource.Token;

            var currentFolder = NormalizeOutputPath(currentValue?.Trim() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(currentFolder))
            {
                HasCurrentSubfolders = false;
                return;
            }

            bool hasChildren = false;
            try
            {
                hasChildren = await Task.Run(() =>
                {
                    if (token.IsCancellationRequested)
                        return false;

                    try
                    {
                        // 1件だけ見て終わる
                        return Directory.EnumerateDirectories(currentFolder).Take(1).Any();
                    }
                    catch
                    {
                        return false;
                    }
                }, token);
            }
            catch
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            HasCurrentSubfolders = hasChildren;
        }

        public void BeginEdit()
        {
            IsEditing = true;
            EditText = currentValue;
            SelectedSuggestion = null;
            RefreshSuggestions();
        }

        public void CancelEdit()
        {
            IsEditing = false;
            EditText = currentValue;
            SelectedSuggestion = null;
            Suggestions.Clear();
            UpdateSuggestionsOpenState();
        }

        public void EndEditAfterCommit()
        {
            IsEditing = false;
            SelectedSuggestion = null;
            Suggestions.Clear();
            UpdateSuggestionsOpenState();
        }

        public void RefreshSuggestions()
        {
            if (!IsEditing)
                return;

            _ = UpdateSuggestionsAsync(EditText);
        }

        public void MoveSuggestionSelection(int delta)
        {
            if (Suggestions.Count == 0)
                return;

            var currentIndex = SelectedSuggestion != null ? Suggestions.IndexOf(SelectedSuggestion) : -1;
            var nextIndex = currentIndex + delta;

            if (nextIndex < 0) nextIndex = 0;
            if (nextIndex >= Suggestions.Count) nextIndex = Suggestions.Count - 1;

            SelectedSuggestion = Suggestions[nextIndex];
        }

        public string ResolveSuggestionToValue(AddressBarSuggestion suggestion)
        {
            if (!string.IsNullOrWhiteSpace(suggestion.FullPath))
                return NormalizeOutputPath(suggestion.FullPath!);

            return ResolveInputTextToValue(suggestion.InsertText);
        }

        public string ResolveInputTextToValue(string? inputText)
        {
            var text = (inputText ?? string.Empty).Trim();
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
                text = text[1..^1];

            text = text.Replace('/', '\\');
            text = Environment.ExpandEnvironmentVariables(text);

            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var baseDirectory = GetBaseDirectoryFromCurrentValue();

            string combined;
            if (Path.IsPathRooted(text))
                combined = NormalizeRootedPath(text);
            else
                combined = Path.Combine(baseDirectory, text);

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(combined);
            }
            catch
            {
                return string.Empty;
            }

            return NormalizeOutputPath(fullPath);
        }

        public static bool ExistsDirectory(string path)
        {
            try { return Directory.Exists(path); }
            catch { return false; }
        }

        public static string NormalizeOutputPath(string path)
        {
            var normalized = NormalizeRootedPath(path);

            var root = TryGetRoot(normalized);
            if (!string.IsNullOrWhiteSpace(root) &&
                string.Equals(EnsureTrailingBackslash(root), EnsureTrailingBackslash(normalized), StringComparison.OrdinalIgnoreCase))
            {
                // ルートは末尾 '\' を維持
                return EnsureTrailingBackslash(root);
            }

            // ルート以外は末尾 '\' を落とす
            normalized = normalized.TrimEnd('\\');

            // "C:" は "C:\" に寄せる
            if (IsDriveLetterOnly(normalized))
                return EnsureTrailingBackslash(normalized + "\\");

            return normalized;
        }

        public void UpdateBreadcrumbTokens()
        {
            BreadcrumbTokens.Clear();

            if (allPathSegments.Count == 0)
            {
                omittedSegments.Clear();
                OnPropertyChanged(nameof(CanOpenEllipsisMenu));
                return;
            }

            var visibleSegments = CalculateVisibleSegments(allPathSegments, AvailableWidth);

            if (omittedSegments.Count > 0)
            {
                BreadcrumbTokens.Add(AddressBarBreadcrumbToken.CreateSegment(AddressBarBreadcrumbSegment.CreateEllipsis()));
                // "..." の右側の区切りはクリック無効
                if (visibleSegments.Count > 0)
                    BreadcrumbTokens.Add(AddressBarBreadcrumbToken.CreateSeparator(leftPath: null));
            }

            for (var i = 0; i < visibleSegments.Count; i++)
            {
                if (i > 0)
                    BreadcrumbTokens.Add(AddressBarBreadcrumbToken.CreateSeparator(visibleSegments[i - 1].FullPath));

                BreadcrumbTokens.Add(AddressBarBreadcrumbToken.CreateSegment(visibleSegments[i]));
            }
            
            // 末尾の ">"（現在フォルダに子フォルダがある場合だけ）
            if (HasCurrentSubfolders)
            {
                // 「現在フォルダ」のパスは、可視セグメントの末尾がそれになる（末尾優先で残す仕様のため）
                var currentPathForTail = visibleSegments.Count > 0
                    ? visibleSegments[^1].FullPath
                    : allPathSegments[^1].FullPath;

                if (!string.IsNullOrWhiteSpace(currentPathForTail))
                    BreadcrumbTokens.Add(AddressBarBreadcrumbToken.CreateSeparator(currentPathForTail));
            }

            OnPropertyChanged(nameof(CanOpenEllipsisMenu));
        }

        public void LoadEllipsisMenu()
        {
            MenuEntries.Clear();
            foreach (var segment in omittedSegments)
            {
                if (!string.IsNullOrWhiteSpace(segment.FullPath))
                    MenuEntries.Add(new AddressBarMenuEntry(segment.DisplayText, segment.FullPath!));
            }
        }

        public async Task LoadDriveMenuAsync()
        {
            menuCancellationTokenSource?.Cancel();
            menuCancellationTokenSource?.Dispose();
            menuCancellationTokenSource = new CancellationTokenSource();
            var token = menuCancellationTokenSource.Token;

            IReadOnlyList<AddressBarMenuEntry> entries;
            try
            {
                entries = await Task.Run(() => BuildDriveMenuEntries(token), token);
            }
            catch
            {
                entries = [];
            }

            if (token.IsCancellationRequested)
                return;

            MenuEntries.Clear();
            foreach (var entry in entries)
                MenuEntries.Add(entry);
        }

        public async Task LoadSubfolderMenuAsync(string leftPath)
        {
            if (string.IsNullOrWhiteSpace(leftPath))
                return;

            menuCancellationTokenSource?.Cancel();
            menuCancellationTokenSource?.Dispose();
            menuCancellationTokenSource = new CancellationTokenSource();
            var token = menuCancellationTokenSource.Token;

            IReadOnlyList<AddressBarMenuEntry> entries;
            try
            {
                entries = await Task.Run(() => BuildSubfolderEntries(leftPath, token), token);
            }
            catch
            {
                entries = [];
            }

            if (token.IsCancellationRequested)
                return;

            MenuEntries.Clear();
            foreach (var entry in entries)
                MenuEntries.Add(entry);
        }

        async Task UpdateSuggestionsAsync(string currentText)
        {
            suggestionCancellationTokenSource?.Cancel();
            suggestionCancellationTokenSource?.Dispose();
            suggestionCancellationTokenSource = new CancellationTokenSource();
            var token = suggestionCancellationTokenSource.Token;

            try { await Task.Delay(120, token); }
            catch { return; }

            var fileSystemTask = Task.Run(() => BuildFileSystemSuggestions(currentText, token), token);
            var externalTask = Task.Run(() => BuildExternalSuggestions(currentText, token), token);

            AddressBarSuggestion[] fileSystemSuggestions;
            AddressBarSuggestion[] filteredExternalSuggestions;

            try
            {
                await Task.WhenAll(fileSystemTask, externalTask);
                fileSystemSuggestions = fileSystemTask.Result;
                filteredExternalSuggestions = externalTask.Result;
            }
            catch
            {
                return;
            }

            if (token.IsCancellationRequested)
                return;

            var unique = new Dictionary<string, AddressBarSuggestion>(StringComparer.OrdinalIgnoreCase);

            foreach (var suggestion in fileSystemSuggestions)
            {
                var key = suggestion.FullPath ?? suggestion.InsertText;
                unique.TryAdd(key, suggestion);
            }

            foreach (var suggestion in filteredExternalSuggestions)
            {
                var key = suggestion.FullPath ?? suggestion.InsertText;
                unique.TryAdd(key, suggestion);
            }

            Suggestions.Clear();
            foreach (var suggestion in unique.Values)
                Suggestions.Add(suggestion);

            UpdateSelectedSuggestionAfterRefresh(currentText);
            UpdateSuggestionsOpenState();
        }
        void UpdateSelectedSuggestionAfterRefresh(string currentText)
        {
            // ルール：
            // - 入力が空なら自動選択しない（Enterでキャンセルさせる）
            // - 末尾が '\'（=フォルダ名未入力状態）なら自動選択しない（Enterでそのフォルダへ移動させる）
            // - それ以外は「既存選択が残ってれば維持」、無ければ先頭を選択

            if (!ShouldAutoSelectSuggestion(currentText))
            {
                SelectedSuggestion = null;
                return;
            }

            if (SelectedSuggestion != null && Suggestions.Contains(SelectedSuggestion))
                return;

            SelectedSuggestion = Suggestions.FirstOrDefault();
        }

        static bool ShouldAutoSelectSuggestion(string rawInput)
        {
            var text = (rawInput ?? string.Empty).Trim();

            if (text.Length == 0)
                return false;

            // "..." 対応（両端だけ外す）
            if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
                text = text[1..^1];

            text = text.Replace('/', '\\');
            text = Environment.ExpandEnvironmentVariables(text);

            // 末尾 '\' は「フォルダ名未入力」扱い => 自動選択しない
            if (text.EndsWith('\\'))
                return false;

            return true;
        }

        AddressBarSuggestion[] BuildFileSystemSuggestions(string currentText, CancellationToken token)
        {
            if (!IsEditing || MaxFileSystemSuggestions <= 0)
                return [];

            var query = TryBuildDirectoryQuery(currentText);
            if (query == null)
                return [];

            var (directoryPart, namePart) = query.Value;

            var results = new List<AddressBarSuggestion>();
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(directoryPart))
                {
                    if (token.IsCancellationRequested)
                        break;

                    var directoryName = GetDirectoryName(directory);
                    if (!directoryName.StartsWith(namePart, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var fullPath = NormalizeOutputPath(directory);

                    results.Add(new AddressBarSuggestion(
                        displayText: directoryName,
                        insertText: fullPath,
                        fullPath: fullPath,
                        source: AddressBarSuggestionSource.FileSystem));

                    if (results.Count >= MaxFileSystemSuggestions)
                        break;
                }
            }
            catch
            {
                return [];
            }

            return [.. results.OrderBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase)];
        }

        AddressBarSuggestion[] BuildExternalSuggestions(string currentText, CancellationToken token)
        {
            if (!IsEditing || MaxExternalSuggestions <= 0)
                return [];

            var text = (currentText ?? string.Empty).Trim();
            if (text.Length == 0)
                return [.. externalSuggestions.Take(MaxExternalSuggestions)];

            var results = new List<AddressBarSuggestion>();
            foreach (var suggestion in externalSuggestions)
            {
                if (token.IsCancellationRequested)
                    break;

                if (suggestion.DisplayText.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    suggestion.InsertText.Contains(text, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(suggestion);
                    if (results.Count >= MaxExternalSuggestions)
                        break;
                }
            }
            return [.. results];
        }

        (string directoryPart, string namePart)? TryBuildDirectoryQuery(string rawInput)
        {
            var input = (rawInput ?? string.Empty).Trim();
            if (input.Length >= 2 && input[0] == '"' && input[^1] == '"')
                input = input[1..^1];

            input = input.Replace('/', '\\');
            input = Environment.ExpandEnvironmentVariables(input);

            var baseDirectory = GetBaseDirectoryFromCurrentValue();

            if (input.EndsWith('\\'))
            {
                var candidate = Path.IsPathRooted(input) ? NormalizeRootedPath(input) : Path.Combine(baseDirectory, input);
                candidate = NormalizeRootedPath(candidate);
                return (EnsureTrailingBackslash(candidate), string.Empty);
            }

            var lastSeparatorIndex = input.LastIndexOf('\\');
            string directoryPartText;
            string namePart;

            if (lastSeparatorIndex >= 0)
            {
                directoryPartText = input[..(lastSeparatorIndex + 1)];
                namePart = input[(lastSeparatorIndex + 1)..];
            }
            else
            {
                directoryPartText = string.Empty;
                namePart = input;
            }

            var resolvedDirectoryPart = string.IsNullOrWhiteSpace(directoryPartText)
                ? EnsureTrailingBackslash(baseDirectory)
                : EnsureTrailingBackslash(Path.IsPathRooted(directoryPartText)
                    ? NormalizeRootedPath(directoryPartText)
                    : NormalizeRootedPath(Path.Combine(baseDirectory, directoryPartText)));

            if (IsDriveLetterOnly(resolvedDirectoryPart.TrimEnd('\\')))
                resolvedDirectoryPart = EnsureTrailingBackslash(resolvedDirectoryPart.TrimEnd('\\') + "\\");

            return (resolvedDirectoryPart, namePart ?? string.Empty);
        }

        void UpdateSuggestionsOpenState()
        {
            IsSuggestionsOpen = IsEditing && Suggestions.Count > 0;
        }

        void RebuildSegmentsFromValue(string value)
        {
            allPathSegments.Clear();
            omittedSegments.Clear();

            var normalized = value?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(normalized))
                return;

            normalized = NormalizeOutputPath(normalized);

            var root = TryGetRoot(normalized);
            if (string.IsNullOrWhiteSpace(root))
                return;

            root = EnsureTrailingBackslash(root);

            if (IsUncShareRoot(root))
            {
                var display = root.TrimEnd('\\');
                allPathSegments.Add(new AddressBarBreadcrumbSegment(AddressBarBreadcrumbSegmentKind.UncRoot, display, root));
            }
            else
            {
                var driveLetter = root.Length >= 2 ? root[..2] : root.TrimEnd('\\');
                var label = TryGetDriveLabel(driveLetter);
                var display = string.IsNullOrWhiteSpace(label) ? driveLetter : $"{label} ({driveLetter})";
                allPathSegments.Add(new AddressBarBreadcrumbSegment(AddressBarBreadcrumbSegmentKind.Drive, display, root));
            }

            if (normalized.Length <= root.Length)
                return;

            var relative = normalized[root.Length..].TrimStart('\\');
            if (relative.Length == 0)
                return;

            var parts = relative.Split(['\\'], StringSplitOptions.RemoveEmptyEntries);

            var current = root;
            for (var i = 0; i < parts.Length; i++)
            {
                current = Path.Combine(current, parts[i]);
                var fullPath = NormalizeOutputPath(current);
                allPathSegments.Add(new AddressBarBreadcrumbSegment(AddressBarBreadcrumbSegmentKind.Directory, parts[i], fullPath));
            }
        }

        List<AddressBarBreadcrumbSegment> CalculateVisibleSegments(List<AddressBarBreadcrumbSegment> segments, double widthBudget)
        {
            omittedSegments.Clear();

            var pcBlockWidth = MeasurePcBlockWidth();
            var safetyPadding = 16.0;

            // 末尾 ">"（子フォルダがあるとき）ぶんの幅を先に予約
            var tailSeparatorWidth = HasCurrentSubfolders ? MeasureSeparatorWidth() : 0.0;

            // ここで tailSeparatorWidth を引くのがポイント
            var remainingWidth = Math.Max(0, widthBudget - pcBlockWidth - safetyPadding - tailSeparatorWidth);

            if (remainingWidth <= 0)
            {
                if (segments.Count > 1)
                    omittedSegments.AddRange(segments.Take(segments.Count - 1));
                return [.. segments.Skip(Math.Max(0, segments.Count - 1))];
            }

            // 省略なし判定も tail は予約済みなので segments の幅だけでOK
            var widthWithoutEllipsis = MeasureSegmentsWidth(segments);
            if (widthWithoutEllipsis <= remainingWidth)
                return [.. segments];

            // 省略あり
            var ellipsisWidth = MeasureSegmentWidth(AddressBarBreadcrumbSegment.CreateEllipsis());
            var separatorWidth = MeasureSeparatorWidth();

            var visible = new List<AddressBarBreadcrumbSegment>();
            double used = 0;

            for (var i = segments.Count - 1; i >= 0; i--)
            {
                var segment = segments[i];
                var segmentWidth = MeasureSegmentWidth(segment);

                var additional = visible.Count == 0 ? segmentWidth : segmentWidth + separatorWidth;

                // "..." + （"..."右の区切り1個） + いままでの使用幅 + 追加分
                var projected = ellipsisWidth + separatorWidth + used + additional;

                if (visible.Count == 0)
                {
                    visible.Insert(0, segment);
                    used += additional;
                    continue;
                }

                if (projected > remainingWidth)
                    break;

                visible.Insert(0, segment);
                used += additional;
            }

            var omittedCount = segments.Count - visible.Count;
            if (omittedCount > 0)
                omittedSegments.AddRange(segments.Take(omittedCount));

            return visible;
        }

        double MeasureSegmentsWidth(List<AddressBarBreadcrumbSegment> segments)
        {
            double width = 0;
            for (var i = 0; i < segments.Count; i++)
            {
                if (i > 0)
                    width += MeasureSeparatorWidth();
                width += MeasureSegmentWidth(segments[i]);
            }
            return width;
        }

        static double MeasurePcBlockWidth()
        {
            // XAMLと一致させる
            const double iconWidth = 18;
            const double driveButtonWidth = 18;
            return iconWidth + driveButtonWidth;
        }

        static double MeasureSeparatorWidth()
        {
            // XAMLと一致させる
            const double separatorButtonWidth = 18;
            return separatorButtonWidth;
        }

        double MeasureSegmentWidth(AddressBarBreadcrumbSegment segment)
        {
            if (segment.Kind == AddressBarBreadcrumbSegmentKind.Ellipsis)
            {
                var ellipsisWidth = MeasureTextWidth("...");
                const double ellipsisPadding = 12; // XAML Padding="6,0"
                return ellipsisWidth + ellipsisPadding;
            }

            var textWidth = MeasureTextWidth(segment.DisplayText);
            const double paddingLeftRight = 12; // XAML Padding="6,0"
            return textWidth + paddingLeftRight;
        }

        double MeasureTextWidth(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            var typeface = new Typeface(fontFamily, fontStyle, fontWeight, fontStretch);

            var formattedText = new FormattedText(
                text,
                CultureInfo.CurrentUICulture,
                System.Windows.FlowDirection.LeftToRight,
                typeface,
                fontSize,
                Brushes.Black,
                PixelsPerDip);

            return formattedText.WidthIncludingTrailingWhitespace;
        }

        string GetBaseDirectoryFromCurrentValue()
        {
            var current = (currentValue ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(current))
                return Environment.CurrentDirectory;

            current = NormalizeOutputPath(current);

            var root = TryGetRoot(current);
            if (!string.IsNullOrWhiteSpace(root) &&
                string.Equals(EnsureTrailingBackslash(root), EnsureTrailingBackslash(current), StringComparison.OrdinalIgnoreCase))
            {
                return EnsureTrailingBackslash(root);
            }

            return current;
        }

        static AddressBarMenuEntry[] BuildDriveMenuEntries(CancellationToken token)
        {
            DriveInfo[] drives;
            try { drives = DriveInfo.GetDrives(); }
            catch { return []; }

            var list = new List<AddressBarMenuEntry>();

            foreach (var drive in drives)
            {
                if (token.IsCancellationRequested)
                    break;

                var driveRoot = drive.Name; // "C:\"
                var driveLetter = driveRoot.Length >= 2 ? driveRoot[..2] : driveRoot.TrimEnd('\\');

                string label = string.Empty;
                try { label = drive.IsReady ? (drive.VolumeLabel ?? string.Empty) : string.Empty; }
                catch { label = string.Empty; }

                var display = string.IsNullOrWhiteSpace(label) ? driveLetter : $"{label} ({driveLetter})";
                list.Add(new AddressBarMenuEntry(display, driveRoot));
            }
            var entries = list
                .OrderBy(x => GetDriveLetterSortKey(x.FullPath))
                .ThenBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase);
            return [.. entries];
        }
        static int GetDriveLetterSortKey(string fullPath)
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return int.MaxValue;

            // "C:\" の先頭文字でソート
            var c = fullPath[0];
            if (char.IsLetter(c))
                return char.ToUpperInvariant(c);

            return int.MaxValue;
        }

        static AddressBarMenuEntry[] BuildSubfolderEntries(string leftPath, CancellationToken token)
        {
            var list = new List<AddressBarMenuEntry>();
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(leftPath))
                {
                    if (token.IsCancellationRequested)
                        break;

                    var name = GetDirectoryName(directory);
                    list.Add(new AddressBarMenuEntry(name, directory));
                }
            }
            catch
            {
                return [];
            }

            var entries = list.OrderBy(x => x.DisplayText, StringComparer.OrdinalIgnoreCase);
            return [.. entries];
        }

        static string GetDirectoryName(string fullPath)
        {
            try
            {
                var name = Path.GetFileName(fullPath.TrimEnd('\\'));
                return string.IsNullOrWhiteSpace(name) ? fullPath : name;
            }
            catch
            {
                return fullPath;
            }
        }

        static string NormalizeRootedPath(string text)
        {
            if (IsDriveLetterOnly(text))
                return EnsureTrailingBackslash(text + "\\");

            return text;
        }

        static bool IsDriveLetterOnly(string text)
        {
            if (text == null)
                return false;

            return text.Length == 2 && char.IsLetter(text[0]) && text[1] == ':';
        }

        static string EnsureTrailingBackslash(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            return path.EndsWith('\\') ? path : path + "\\";
        }

        static string? TryGetRoot(string path)
        {
            try { return Path.GetPathRoot(path); }
            catch { return null; }
        }

        static bool IsUncShareRoot(string rootWithTrailingBackslash)
        {
            // Path.GetPathRoot("\\server\\share\\dir") は "\\server\\share\\"
            // 共有ルートを "UNCルート扱い" とする
            return rootWithTrailingBackslash.StartsWith("\\\\", StringComparison.Ordinal);
        }

        static string TryGetDriveLabel(string driveLetter)
        {
            try
            {
                var driveInfo = new DriveInfo(driveLetter);
                if (!driveInfo.IsReady)
                    return string.Empty;

                return driveInfo.VolumeLabel ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
