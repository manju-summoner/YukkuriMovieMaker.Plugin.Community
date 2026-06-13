using System;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Voice.IrodoriTTS.EmojiPalette;

internal record EmojiItem(string Emoji, string Description, string ImageFileName)
{
    public BitmapImage Image { get; } = new(new Uri($"pack://application:,,,/YukkuriMovieMaker.Plugin.Community;component/Voice/IrodoriTTS/EmojiPalette/Emoji/{ImageFileName}", UriKind.Absolute));
}
