﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Project;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.ReflectionAndExtrusion.Heightmap.HeightmapFile
{
    internal class HeightmapFileParameter : HeightmapParameterBase, IFileItem, IResourceItem
    {
        [Display(Name = nameof(Texts.FileName), Description = nameof(Texts.FileDesc), ResourceType = typeof(Texts))]
        [FileSelector(Settings.FileGroupType.Texture, ShowThumbnail = true)]
        public string? File { get => file; set => Set(ref file, value); }
        private string? file;

        [Display(Name = nameof(Texts.XName), Description = nameof(Texts.XName), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.YName), Description = nameof(Texts.YName), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "px", -500, 500)]
        public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Zoom), Description = nameof(Texts.Zoom), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "%", 0, 400)]
        public Animation Zoom { get; } = new Animation(100, 1, YMM4Constants.VeryLargeValue);

        [Display(Name = nameof(Texts.Rotation), Description = nameof(Texts.Rotation), ResourceType = typeof(Texts))]
        [AnimationSlider("F1", "°", -360, 360)]
        public Animation Rotation { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);

        public HeightmapFileParameter()
        {
        }

        public HeightmapFileParameter(SharedDataStore store) : base(store)
        {
        }

        public override IVideoEffectProcessor CreateHeightmapSource(IGraphicsDevicesAndContext devices)
        {
            return new HeightmapFileSource(devices, this);
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [X, Y, Zoom, Rotation];

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            if (store.Load<SharedData>() is SharedData data)
                data.CopyTo(this);
        }

        public override IEnumerable<string> CreateExoVideoFilters(bool isEnabled, int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            var fps = exoOutputDescription.VideoInfo.FPS;
            yield return $"_name=アニメーション効果\r\n" +
                $"_disable={(isEnabled ? 0 : 1)}\r\n" +
                $"track0={X.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track1={Y.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track2={Zoom.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"track3={Rotation.ToExoString(keyFrameIndex, "F2", fps)}\r\n" +
                $"name=ハイトマップファイル@YMM4-未実装\r\n" +
                $"param=" +
                    $"file={File};" +
                    $"\r\n";
        }

        public IEnumerable<TimelineResource> GetResources()
        {
            if (TimelineResource.TryParseFromPath(File, TimelineResourceType.Image, out var resource))
                yield return resource;
        }

        public IEnumerable<string> GetFiles()
        {
            if(!string.IsNullOrEmpty(File))
                yield return File;
        }

        public void ReplaceFile(string from, string to)
        {
            if (File == from)
                File = to;
        }

        class SharedData
        {
            public string? File { get; set; }
            public Animation X { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
            public Animation Y { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue);
            public Animation Zoom { get; } = new Animation(100, 1, YMM4Constants.VeryLargeValue);
            public Animation Rotation { get; } = new Animation(0, YMM4Constants.VerySmallValue, YMM4Constants.VeryLargeValue, 360);

            public SharedData()
            {
            }
            public SharedData(HeightmapFileParameter parameter)
            {
                File = parameter.File;
                X.CopyFrom(parameter.X);
                Y.CopyFrom(parameter.Y);
                Zoom.CopyFrom(parameter.Zoom);
                Rotation.CopyFrom(parameter.Rotation);
            }
            public void CopyTo(HeightmapFileParameter parameter)
            {
                parameter.File = File;
                parameter.X.CopyFrom(X);
                parameter.Y.CopyFrom(Y);
                parameter.Zoom.CopyFrom(Zoom);
                parameter.Rotation.CopyFrom(Rotation);
            }
        }
    }
}
