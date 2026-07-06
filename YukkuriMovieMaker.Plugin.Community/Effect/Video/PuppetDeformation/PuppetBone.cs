using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Newtonsoft.Json;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.UndoRedo;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.PuppetDeformation
{
    /// <summary>
    /// パペット変形のボーン。親ジョイント→自分のジョイントを結ぶセグメントを表し、割り当てられたピンを回転・揺れで駆動する。
    /// 回転は親ジョイント（セグメントの根元）を中心に行い、親の回転が子のジョイントとピンにも伝わる（フォワードキネマティクス）。
    /// 親を持たないルートは自身のジョイントを中心に回転する。角度を子側が持つため、分岐した各枝は独立して回転できる。
    /// </summary>
    public class PuppetBone : Animatable
    {
        [JsonIgnore]
        [IgnoreUndoRedo]
        public bool IsSelected { get => isSelected; set => Set(ref isSelected, value); }
        bool isSelected = false;

        /// <summary>ボーンの識別子。ピンの割り当てと親子関係の参照に使う</summary>
        public Guid Id { get => id; set => Set(ref id, value); }
        Guid id = Guid.NewGuid();

        /// <summary>親ボーンのId。Guid.Emptyでルート</summary>
        public Guid ParentId { get => parentId; set => Set(ref parentId, value); }
        Guid parentId = Guid.Empty;

        [Display(GroupName = nameof(Texts.PuppetBoneGroupName), Name = nameof(Texts.PuppetBoneNameName), Description = nameof(Texts.PuppetBoneNameDesc), Order = 0, ResourceType = typeof(Texts))]
        [TextEditor]
        public string Name { get => name; set => Set(ref name, value); }
        string name = string.Empty;

        [Display(GroupName = nameof(Texts.PuppetBoneGroupName), Name = nameof(Texts.PuppetBoneEnabledName), Description = nameof(Texts.PuppetBoneEnabledDesc), Order = 1, ResourceType = typeof(Texts))]
        [ToggleSlider]
        public bool IsEnabled { get => isEnabled; set => Set(ref isEnabled, value); }
        bool isEnabled = true;

        [Display(GroupName = nameof(Texts.PuppetBoneGroupName), Name = nameof(Texts.PuppetBoneAngleName), Description = nameof(Texts.PuppetBoneAngleDesc), Order = 2, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -180.0, 180.0)]
        public Animation Angle { get; } = new Animation(0, -36000, 36000);

        [Display(GroupName = nameof(Texts.PuppetBoneSwayGroupName), Name = nameof(Texts.PuppetBoneSwayAngleName), Description = nameof(Texts.PuppetBoneSwayAngleDesc), Order = 3, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", 0.0, 90.0)]
        public Animation SwayAngle { get; } = new Animation(0, 0, 3600);

        [Display(GroupName = nameof(Texts.PuppetBoneSwayGroupName), Name = nameof(Texts.PuppetBoneSwayPeriodName), Description = nameof(Texts.PuppetBoneSwayPeriodDesc), Order = 4, ResourceType = typeof(Texts))]
        [TextBoxSlider("F2", nameof(Texts.SecUnit), 0.1, 10.0, ResourceType = typeof(Texts))]
        [Range(0.0, 100.0)]
        [DefaultValue(1.0)]
        public double SwayPeriod { get => swayPeriod; set => Set(ref swayPeriod, value); }
        double swayPeriod = 1.0;

        [Display(GroupName = nameof(Texts.PuppetBoneSwayGroupName), Name = nameof(Texts.PuppetBoneSwayPhaseName), Description = nameof(Texts.PuppetBoneSwayPhaseDesc), Order = 5, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "°", -360.0, 360.0)]
        [Range(-3600.0, 3600.0)]
        [DefaultValue(0.0)]
        public double SwayPhase { get => swayPhase; set => Set(ref swayPhase, value); }
        double swayPhase = 0;

        [Display(GroupName = nameof(Texts.PuppetBoneSwayGroupName), Name = nameof(Texts.PuppetBoneSwayPropagationName), Description = nameof(Texts.PuppetBoneSwayPropagationDesc), Order = 6, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "%", 0.0, 200.0)]
        [Range(0.0, 10000.0)]
        [DefaultValue(100.0)]
        public double SwayPropagation { get => swayPropagation; set => Set(ref swayPropagation, value); }
        double swayPropagation = 100;

        [Display(GroupName = nameof(Texts.PuppetBoneSwayGroupName), Name = nameof(Texts.PuppetBoneSwayFlexibilityName), Description = nameof(Texts.PuppetBoneSwayFlexibilityDesc), Order = 7, ResourceType = typeof(Texts))]
        [TextBoxSlider("F0", "°", -180.0, 180.0)]
        [Range(-3600.0, 3600.0)]
        [DefaultValue(60.0)]
        public double SwayFlexibility { get => swayFlexibility; set => Set(ref swayFlexibility, value); }
        double swayFlexibility = 60;

        [Display(GroupName = nameof(Texts.PuppetBoneGroupName), Name = nameof(Texts.PuppetBoneJointXName), Description = nameof(Texts.PuppetBoneJointXDesc), Order = 8, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation JointX { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(GroupName = nameof(Texts.PuppetBoneGroupName), Name = nameof(Texts.PuppetBoneJointYName), Description = nameof(Texts.PuppetBoneJointYDesc), Order = 9, ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500.0, 500.0)]
        public Animation JointY { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public static PuppetBone Create(double jointX, double jointY, Guid parentId)
        {
            var bone = new PuppetBone { ParentId = parentId };
            bone.JointX.Values[0].Value = jointX;
            bone.JointY.Values[0].Value = jointY;
            return bone;
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Angle, SwayAngle, JointX, JointY];
    }
}
