using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.GeminiTTS
{
    internal class GeminiTTSVoicePickerItem(GeminiTTSVoiceInfo info, bool isChecked) : Bindable
    {
        bool isChecked = isChecked;

        public GeminiTTSVoiceInfo Info { get; } = info;
        public bool IsChecked { get => isChecked; set => Set(ref isChecked, value); }
    }

    internal record GeminiTTSVoicePickerOption(string Value, string Name);

    internal class GeminiTTSVoicePickerViewModel : Bindable
    {
        List<GeminiTTSVoiceInfo> catalog = [];
        List<GeminiTTSVoicePickerItem> items = [];
        ICollectionView? itemsView;
        IReadOnlyList<GeminiTTSVoicePickerOption> languages = [new(string.Empty, Texts.All)];
        string selectedLanguage = string.Empty, selectedGender = string.Empty, searchText = string.Empty, errorText = string.Empty;
        bool isLoading, isLoaded;

        public ICollectionView? ItemsView { get => itemsView; private set => Set(ref itemsView, value); }
        public IReadOnlyList<GeminiTTSVoicePickerOption> Languages { get => languages; private set => Set(ref languages, value); }
        public IReadOnlyList<GeminiTTSVoicePickerOption> Genders { get; } =
        [
            new(string.Empty, Texts.All),
            new("female", Texts.Female),
            new("male", Texts.Male),
            new("neutral", Texts.Neutral),
        ];

        public string SelectedLanguage { get => selectedLanguage; set { if (Set(ref selectedLanguage, value)) ItemsView?.Refresh(); } }
        public string SelectedGender { get => selectedGender; set { if (Set(ref selectedGender, value)) ItemsView?.Refresh(); } }
        public string SearchText { get => searchText; set { if (Set(ref searchText, value)) ItemsView?.Refresh(); } }
        public string ErrorText { get => errorText; private set => Set(ref errorText, value); }
        public bool IsLoading { get => isLoading; private set => Set(ref isLoading, value); }
        public bool IsLoaded { get => isLoaded; private set => Set(ref isLoaded, value); }

        public async Task LoadAsync()
        {
            var apiKey = GeminiTTSSettings.Default.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ErrorText = Texts.ApiKeyRequired;
                return;
            }

            IsLoading = true;
            ErrorText = string.Empty;
            try
            {
                catalog = GeminiTTSVoiceList.CreateCatalog(await GeminiTTSVoiceList.FetchAllAsync(apiKey));
            }
            catch (Exception ex)
            {
                Log.Default.Write("GeminiTTS failed to load the voice list", ex);
                ErrorText = Texts.LoadVoicesFailed + Environment.NewLine + ex.Message;
                return;
            }
            finally
            {
                IsLoading = false;
            }

            var selectedIds = GeminiTTSSettings.Default.Voices.Select(v => v.Id).ToHashSet();
            items = [.. catalog.Select(v => new GeminiTTSVoicePickerItem(v, selectedIds.Contains(v.Id)))];

            var availableLanguages = catalog.Where(v => !v.IsCustom).Select(v => GeminiTTSVoiceList.GetPrimaryLanguage(v.LanguageCode)).Where(x => x.Length > 0).Distinct();
            Languages =
            [
                new(string.Empty, Texts.All),
                .. availableLanguages.Select(x => new GeminiTTSVoicePickerOption(x, GetLanguageName(x))).OrderBy(x => x.Name, StringComparer.CurrentCulture),
            ];
            selectedLanguage = GetDefaultLanguage(Languages.Select(x => x.Value), CultureInfo.CurrentUICulture);
            OnPropertyChanged(nameof(SelectedLanguage));

            var view = CollectionViewSource.GetDefaultView(items);
            view.Filter = x => x is GeminiTTSVoicePickerItem item && GeminiTTSVoiceList.IsMatch(item.Info, SelectedLanguage, SelectedGender, SearchText);
            ItemsView = view;
            IsLoaded = true;
        }

        /// <summary>
        /// YMM4 の表示言語と主言語サブタグが同じ言語。その言語の声が無い（中国語・インドネシア語など）ときはすべての言語を出す
        /// </summary>
        internal static string GetDefaultLanguage(IEnumerable<string> availableLanguages, CultureInfo uiCulture)
        {
            var language = GeminiTTSVoiceList.GetPrimaryLanguage(uiCulture.Name);
            return availableLanguages.Contains(language) ? language : string.Empty;
        }

        static string GetLanguageName(string language)
        {
            try
            {
                return $"{CultureInfo.GetCultureInfo(language).DisplayName} ({language})";
            }
            catch (CultureNotFoundException)
            {
                return language;
            }
        }

        public void Apply()
        {
            GeminiTTSVoiceList.ApplySelection(
                GeminiTTSSettings.Default.Voices,
                catalog,
                items.Where(x => x.IsChecked).Select(x => x.Info.Id));
        }
    }
}
