namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.Container;

public sealed class EffectTabStashViewModel : EffectTabBaseViewModel
{
    public EffectTabStashViewModel(EffectTab model) : base(model)
    {
    }

    /// <summary>
    /// メニュー表示用の文字列。「タブ名: 先頭エフェクト名 (+他N件)」形式。
    /// 元のタブ名は Model.Name から直接参照する。
    /// </summary>
    public string Name
    {
        get
        {
            var effects = Model.Effects;
            if (effects.Count == 0)
                return Model.Name;

            var firstEffectName = effects[0].Label;
            return effects.Count > 1
                ? string.Format(Texts.Menu_StashNameFormat, Model.Name, firstEffectName, effects.Count - 1)
                : string.Format(Texts.Menu_StashNameFormatSingle, Model.Name, firstEffectName);
        }
    }
}
