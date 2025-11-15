using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Voice.VoiSonaTalk.Editor
{
    internal class VoiSonaTalkStyleWeightDisplayAttribute : CustomDisplayAttributeBase
    {
        public override bool? GetAutoGenerateField(object instance) => false;

        public override bool? GetAutoGenerateFilter(object instance) => true;

        public override string? GetGroupName(object instance) => null;


        public override string? GetName(object instance) => (string?)instance.GetType().GetProperty(nameof(VoiSonaTalkStyleWeight.Name))?.GetValue(instance);
        public override string? GetDescription(object instance) => GetName(instance);

        public override int? GetOrder(object instance) => 0;
    }
}
