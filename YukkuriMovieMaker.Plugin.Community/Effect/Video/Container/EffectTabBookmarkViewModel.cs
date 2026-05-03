namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTabBookmarkViewModel : EffectTabBaseViewModel
{
    public EffectTabBookmarkViewModel(EffectTab model) : base(model)
    {

    }

    public string Name
    {
        get => Model.Name;
        set
        {
            if (Model.Name == value) return;
            Model.Name = value;
            OnPropertyChanged(nameof(Name));
        }
    }
}
