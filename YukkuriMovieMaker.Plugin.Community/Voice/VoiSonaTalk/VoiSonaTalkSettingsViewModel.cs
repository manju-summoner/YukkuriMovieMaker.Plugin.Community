using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using YukkuriMovieMaker.Commons;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk
{
    internal class VoiSonaTalkSettingsViewModel : Bindable
    {
        bool isLoading = false;
        public bool IsLoading { get => isLoading; set => Set(ref isLoading, value, nameof(IsLoading), nameof(CharacterCountText)); }
        public ICommand RefreshCommand { get; }

        public string CharacterCountText => IsLoading ? Texts.Loading : string.Format(Texts.CharacterCountText, VoiSonaTalkSettings.Default.Voices.Length);

        public VoiSonaTalkSettingsViewModel()
        {
            RefreshCommand = new ActionCommand(
                _=> true,
                async _ => 
                {
                    IsLoading = true;
                    try
                    {
                        await VoiSonaTalkAPIHelper.UpdateVoicesAsync();
                    }
                    finally
                    {
                        IsLoading = false;
                    }
                });
        }
    }
}
