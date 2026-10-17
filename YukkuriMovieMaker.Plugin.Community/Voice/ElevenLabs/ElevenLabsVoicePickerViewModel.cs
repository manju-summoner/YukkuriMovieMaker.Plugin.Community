using System.ComponentModel;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.ElevenLabs
{
    internal class ElevenLabsVoicePickerItem(ElevenLabsVoiceInfo info, bool isRegistered) : Bindable
    {
        bool isChecked = isRegistered;

        public ElevenLabsVoiceInfo Info { get; } = info;

        /// <summary>
        /// 話者の表に既にある声。チェックを外しても表からは消さないので、チェック済みのまま変更できないようにする
        /// </summary>
        public bool IsRegistered { get; } = isRegistered;
        public bool CanChange => !IsRegistered;
        public bool IsChecked { get => isChecked; set => Set(ref isChecked, value); }
    }

    internal record ElevenLabsVoicePickerOption(ElevenLabsVoiceKind? Value, string Name);

    internal class ElevenLabsVoicePickerViewModel : Bindable
    {
        List<ElevenLabsVoicePickerItem> items = [];
        ICollectionView? itemsView;
        ElevenLabsVoicePickerOption? selectedKind;
        string searchText = string.Empty, errorText = string.Empty;
        bool isLoading, isLoaded;

        public ElevenLabsVoicePickerViewModel()
        {
            selectedKind = Kinds[0];
        }

        public ICollectionView? ItemsView { get => itemsView; private set => Set(ref itemsView, value); }

        //SelectedValue に null を渡すと「すべて」が選ばれないので、選択肢そのものを SelectedItem で受け取る
        public IReadOnlyList<ElevenLabsVoicePickerOption> Kinds { get; } =
        [
            new(null, Texts.All),
            new(ElevenLabsVoiceKind.MyVoice, Texts.MyVoice),
            new(ElevenLabsVoiceKind.Library, Texts.LibraryVoice),
            new(ElevenLabsVoiceKind.Default, Texts.DefaultVoice),
        ];

        public ElevenLabsVoicePickerOption? SelectedKind { get => selectedKind; set { if (Set(ref selectedKind, value)) ItemsView?.Refresh(); } }
        public string SearchText { get => searchText; set { if (Set(ref searchText, value)) ItemsView?.Refresh(); } }
        public string ErrorText { get => errorText; private set => Set(ref errorText, value); }
        public bool IsLoading { get => isLoading; private set => Set(ref isLoading, value); }
        public bool IsLoaded { get => isLoaded; private set => Set(ref isLoaded, value); }

        public async Task LoadAsync()
        {
            var apiKey = ElevenLabsSettings.Default.APIKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                ErrorText = Texts.APIKeyRequired;
                return;
            }

            IsLoading = true;
            ErrorText = string.Empty;
            List<ElevenLabsVoiceInfo> catalog;
            try
            {
                catalog = ElevenLabsVoiceList.CreateCatalog(await ElevenLabsVoiceList.FetchAllAsync(apiKey));
            }
            catch (Exception ex)
            {
                Log.Default.Write("ElevenLabs failed to load the voice list", ex);
                ErrorText = Texts.LoadVoicesFailed + Environment.NewLine + ex.Message;
                return;
            }
            finally
            {
                IsLoading = false;
            }

            var registered = ElevenLabsVoiceList.GetRegisteredIds(ElevenLabsSettings.Default.Voices);
            items = [.. catalog.Select(v => new ElevenLabsVoicePickerItem(v, registered.Contains(v.VoiceId)))];
            if (items.Count == 0)
                ErrorText = Texts.NoVoicesFound;

            var view = CollectionViewSource.GetDefaultView(items);
            view.Filter = x => x is ElevenLabsVoicePickerItem item && ElevenLabsVoiceList.IsMatch(item.Info, SelectedKind?.Value, SearchText);
            ItemsView = view;
            IsLoaded = true;
        }

        public void Apply()
        {
            ElevenLabsVoiceList.AddVoices(
                ElevenLabsSettings.Default.Voices,
                items.Where(x => x.IsChecked && !x.IsRegistered).Select(x => x.Info));
        }
    }
}
