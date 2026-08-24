using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.ItemEditor;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OpenFx
{
    /// <summary>
    /// OFXパラメータの表示メタデータ（ラベル・説明・グループ・表示順）を
    /// パラメータインスタンスから動的に解決する表示属性。
    /// 多次元パラメータでは dimensionIndex で「ラベル (X)」のような行別ラベルにする。
    /// </summary>
    public class OfxParamDisplayAttribute(int dimensionIndex = -1) : CustomDisplayAttributeBase
    {
        static readonly string[] dimensionLabels = ["X", "Y", "Z"];

        public override string? GetGroupName(object instance)
        {
            var group = (instance as OfxParameterBase)?.Group;
            return string.IsNullOrEmpty(group) ? Texts.OpenFxEffectName : group;
        }

        public override string? GetName(object instance)
        {
            if (instance is not OfxParameterBase parameter)
                return null;
            var label = string.IsNullOrEmpty(parameter.Label) ? parameter.Name : parameter.Label;
            if (dimensionIndex is >= 0 and < 3)
                return $"{label} ({dimensionLabels[dimensionIndex]})";
            return label;
        }

        public override string? GetDescription(object instance)
        {
            var description = (instance as OfxParameterBase)?.Description;
            return string.IsNullOrEmpty(description) ? GetName(instance) : description;
        }

        public override int? GetOrder(object instance)
        {
            var order = (instance as OfxParameterBase)?.Order ?? 0;
            return order * 4 + Math.Max(0, dimensionIndex);
        }

        public override bool? GetAutoGenerateField(object instance) => false;
        public override bool? GetAutoGenerateFilter(object instance) => true;
    }

    /// <summary>
    /// スライダー範囲・書式をOFXパラメータの定義（DisplayMin/Max・Digits）から動的に設定するAnimationSlider。
    /// AnimationSliderAttribute はコンストラクタ引数で範囲が固定されるため、
    /// バインド時にインスタンスの値で上書きする。
    /// </summary>
    public class OfxAnimationSliderAttribute() : AnimationSliderAttribute("F2", "", 0, 1)
    {
        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            base.SetBindings(control, itemProperties);
            if (control is not AnimationSlider slider || itemProperties.Length is 0)
                return;
            var (format, displayMin, displayMax) = itemProperties[0].PropertyOwner switch
            {
                OfxNumberParameter p => (p.StringFormat, p.DisplayMin, p.DisplayMax),
                OfxNumber2DParameter p => (p.StringFormat, p.DisplayMin, p.DisplayMax),
                OfxNumber3DParameter p => (p.StringFormat, p.DisplayMin, p.DisplayMax),
                _ => (null, 0.0, 0.0),
            };
            if (format is null)
                return;
            slider.StringFormat = format;
            slider.Delta = format.StartsWith('F') && int.TryParse(format[1..], out var digits) ? Math.Pow(0.1, digits) : 1;
            slider.DefaultMin = displayMin;
            slider.DefaultMax = displayMax;
        }
    }

    /// <summary>
    /// OfxChoiceParameter 用のコンボボックス属性。
    /// 選択肢が実行時に決まるため、EnumComboBoxではなく専用エディタで
    /// パラメータインスタンスの Options を表示する。
    /// </summary>
    public class OfxChoiceComboBoxAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new OfxChoiceComboBox();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not OfxChoiceComboBox editor)
                return;
            editor.Attach(itemProperties);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not OfxChoiceComboBox editor)
                return;
            editor.Detach();
        }
    }

    /// <summary>
    /// OFXの選択肢パラメータを編集するコンボボックス。
    /// Undo/Redoや外部からの値変更に追従するため、パラメータのPropertyChangedを購読して表示を更新する
    /// </summary>
    internal class OfxChoiceComboBox : ComboBox, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        ItemProperty[]? itemProperties;
        OfxChoiceParameter? subscribedParameter;
        bool isUpdatingDisplay;

        public OfxChoiceComboBox()
        {
            SelectionChanged += OnSelectionChanged;
        }

        public void Attach(ItemProperty[] itemProperties)
        {
            this.itemProperties = itemProperties;
            var parameter = GetTargetParameters().FirstOrDefault();
            if (parameter is null)
                return;
            if (!ReferenceEquals(subscribedParameter, parameter))
            {
                if (subscribedParameter is not null)
                    subscribedParameter.PropertyChanged -= OnParameterPropertyChanged;
                subscribedParameter = parameter;
                parameter.PropertyChanged += OnParameterPropertyChanged;
            }
            UpdateDisplay(parameter);
        }

        public void Detach()
        {
            itemProperties = null;
            if (subscribedParameter is not null)
            {
                subscribedParameter.PropertyChanged -= OnParameterPropertyChanged;
                subscribedParameter = null;
            }
            isUpdatingDisplay = true;
            try
            {
                ItemsSource = null;
            }
            finally
            {
                isUpdatingDisplay = false;
            }
        }

        void OnParameterPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is OfxChoiceParameter parameter)
                UpdateDisplay(parameter);
        }

        void UpdateDisplay(OfxChoiceParameter parameter)
        {
            isUpdatingDisplay = true;
            try
            {
                if (!ReferenceEquals(ItemsSource, parameter.Options))
                    ItemsSource = parameter.Options;
                SelectedIndex = Math.Clamp(parameter.Value, -1, parameter.Options.Count - 1);
            }
            finally
            {
                isUpdatingDisplay = false;
            }
        }

        void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingDisplay || itemProperties is null || SelectedIndex < 0)
                return;
            BeginEdit?.Invoke(this, EventArgs.Empty);
            foreach (var parameter in GetTargetParameters())
                parameter.Value = SelectedIndex;
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        System.Collections.Generic.IEnumerable<OfxChoiceParameter> GetTargetParameters()
            => itemProperties?.Select(x => x.PropertyOwner).OfType<OfxChoiceParameter>() ?? [];
    }
}
