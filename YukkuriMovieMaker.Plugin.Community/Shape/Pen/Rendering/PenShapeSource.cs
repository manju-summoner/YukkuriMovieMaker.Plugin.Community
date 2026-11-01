using System.Collections.Immutable;
using System.Numerics;
using System.Reflection.Metadata;
using Vortice;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services;
using D2DEffects = Vortice.Direct2D1.Effects;
using ProjectBlend = YukkuriMovieMaker.Project.Blend;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Rendering
{
    internal class PenShapeSource : IShapeSource
    {
        readonly DisposeCollector disposer = new();
        readonly PenDrawResources resources = new();

        readonly List<PenLayerRenderer> layerRenderers = [];
        readonly List<PenLayerEffectChain> layerChains = [];
        readonly List<ID2D1CommandList> layerCommandLists = [];
        readonly List<ID2D1Image?> effectInputs = [];
        readonly List<IDisposable> compositionResources = [];

        List<PenLayerPlan> plans = [];
        List<PenLayerPlan> previousPlans = [];
        readonly PenLayerVisibility visibility = new();
        readonly List<PenComposeChain> chainPool = [];
        readonly Dictionary<Guid, int> chainIndices = [];
        int chainCount;

        readonly IGraphicsDevicesAndContext devices;
        readonly PenShapeParameter penShapeParameter;

        readonly ID2D1SolidColorBrush transparent;

        static readonly DrawDescription neutralDrawDescription = new(
            Vector3.Zero,
            Vector2.Zero,
            Vector2.One,
            Vector3.Zero,
            Matrix4x4.Identity,
            InterpolationMode.MultiSampleLinear,
            1,
            false,
            ImmutableList<VideoEffectController>.Empty);

        public ID2D1Image Output => outputImage ?? throw new NullReferenceException($"{nameof(outputImage)} is null.");
        ID2D1CommandList? commandList;
        ID2D1Image? outputImage;

        bool isEditing;
        double thickness;
        double scale;
        System.Drawing.Size screenSize;
        PenStrokeGeometry? wet;
        int wetLayerIndex;
        PenStrokeGeometry? recordedWet;
        int recordedWetCount;
        bool isWetChangeLocal;
        RawRectF wetChangeBounds;

        public double PreviewScale { get; set; } = 1;

        public PenShapeSource(IGraphicsDevicesAndContext devices, PenShapeParameter penShapeParameter)
        {
            this.devices = devices;
            this.penShapeParameter = penShapeParameter;
            disposer.Collect(resources);

            transparent = devices.DeviceContext.CreateSolidColorBrush(new Color4(0, 0, 0, 0));
            disposer.Collect(transparent);
        }


        public void SetWetStroke(int layerIndex, PenStrokeGeometry geometry)
        {
            wet = geometry;
            wetLayerIndex = layerIndex;
        }

        public void ClearWetStroke()
        {
            wet = null;
        }

        public bool TryGetWetChange(out RawRectF bounds)
        {
            bounds = wetChangeBounds;
            return isWetChangeLocal;
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            var dc = devices.DeviceContext;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var thickness = penShapeParameter.Thickness.GetValue(frame, length, fps);
            var isEditing = penShapeParameter.IsEditing;
            var scale = PreviewScale;
            var screenSize = desc.ScreenSize;

            var layers = penShapeParameter.Layers;
            var isStrokesChanged = UpdateRenderers(layers);
            var isEffectsChanged = SynchronizeEffects(layers);
            BuildPlans(layers, frame, length, fps);

            var isWetChanged = IsWetChanged();
            var isUnchanged = outputImage is not null
                && !isStrokesChanged
                && !isEffectsChanged
                && this.thickness == thickness
                && this.isEditing == isEditing
                && this.scale == scale
                && this.screenSize == screenSize
                && IsSamePlans();
            isWetChangeLocal = false;
            if (isUnchanged && !isWetChanged && !UpdateEffects(desc))
                return;
            var wetChangeStart = isUnchanged && isWetChanged && (recordedWet is null || ReferenceEquals(recordedWet, wet)) ? Math.Max(0, recordedWetCount - 1) : -1;
            this.thickness = thickness;
            this.isEditing = isEditing;
            this.scale = scale;
            this.screenSize = screenSize;
            recordedWet = wet;
            recordedWetCount = wet?.PointCount ?? 0;

            resources.BeginUse();

            ReleaseComposition();
            if (commandList is not null)
                disposer.RemoveAndDispose(ref commandList);
            commandList = dc.CreateCommandList();
            disposer.Collect(commandList);

            var isDirect = isEditing || IsDirectComposition();
            var transform = Matrix3x2.CreateTranslation(-screenSize.Width / 2f, -screenSize.Height / 2f)
                * Matrix3x2.CreateScale((float)scale);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);

            //1pxの透明を描画する
            //これがないとキャンバスが空の場合にcommandListの画面サイズが定まらず、エラーになる
            dc.DrawRectangle(new Vortice.RawRectF(0,0,1,1),transparent);

            if (isDirect && !isEditing)
            {
                dc.Transform = transform;
                foreach (var plan in plans)
                    layerRenderers[plan.Index].Draw(dc, plan.PointFrom, plan.PointLength, thickness, resources, GetWet(plan.Index));
                dc.Transform = Matrix3x2.Identity;
            }
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            outputImage = isDirect ? commandList : Compose(dc, commandList, transform, thickness, desc);

            if (wetChangeStart >= 0 && wet is not null && IsWetLocal())
            {
                wetChangeBounds = layerRenderers[wetLayerIndex].GetWetBounds(dc, wet, wetChangeStart, thickness, resources, transform);
                isWetChangeLocal = true;
            }

            resources.EndUse();

            (previousPlans, plans) = (plans, previousPlans);
        }

        bool IsWetChanged()
            => !ReferenceEquals(recordedWet, wet) || recordedWetCount != (wet?.PointCount ?? 0);

        bool IsWetLocal()
        {
            var index = FindPlan(wetLayerIndex);
            if (index < 0)
                return false;

            while (true)
            {
                var plan = plans[index];
                if (plan.HasEffects)
                    return false;
                if (plan.ParentId == Guid.Empty)
                    return true;

                index = FindFolderPlan(plan.ParentId);
                if (index < 0)
                    return false;
            }
        }

        int FindPlan(int layerIndex)
        {
            for (var i = 0; i < plans.Count; i++)
            {
                if (plans[i].Index == layerIndex)
                    return i;
            }
            return -1;
        }

        int FindFolderPlan(Guid id)
        {
            for (var i = 0; i < plans.Count; i++)
            {
                if (plans[i].IsFolder && plans[i].Id == id)
                    return i;
            }
            return -1;
        }

        PenStrokeGeometry? GetWet(int layerIndex)
            => layerIndex == wetLayerIndex ? wet : null;

        bool UpdateRenderers(ImmutableList<PenLayer> layers)
        {
            var count = layers.IsEmpty ? 1 : layers.Count;

            while (layerRenderers.Count > count)
            {
                var last = layerRenderers.Count - 1;
                layerRenderers[last].Dispose();
                layerRenderers.RemoveAt(last);
                layerChains[last].Dispose();
                layerChains.RemoveAt(last);
            }
            while (layerRenderers.Count < count)
            {
                layerRenderers.Add(new PenLayerRenderer(devices));
                layerChains.Add(new PenLayerEffectChain(devices));
            }

            if (layers.IsEmpty)
                return layerRenderers[0].SetStrokes(penShapeParameter.Strokes, true);

            var isChanged = false;
            var index = 0;
            foreach (var layer in layers)
            {
                isChanged |= layerRenderers[index].SetStrokes(layer.Strokes, false);
                index++;
            }
            return isChanged;
        }

        bool SynchronizeEffects(ImmutableList<PenLayer> layers)
        {
            if (layers.IsEmpty)
                return layerChains[0].Synchronize([]);

            var isChanged = false;
            var index = 0;
            foreach (var layer in layers)
            {
                isChanged |= layerChains[index].Synchronize(layer.VideoEffects);
                index++;
            }
            return isChanged;
        }

        bool UpdateEffects(TimelineItemSourceDescription desc)
        {
            if (effectInputs.Count == 0)
                return false;
            if (effectInputs.Count != plans.Count)
                return true;

            var isChanged = false;
            for (var i = 0; i < plans.Count; i++)
            {
                var input = effectInputs[i];
                if (input is null)
                    continue;

                layerChains[plans[i].Index].Apply(input, CreateEffectDescription(desc), out var isOutputChanged);
                isChanged |= isOutputChanged;
            }
            return isChanged;
        }

        static EffectDescription CreateEffectDescription(TimelineItemSourceDescription desc)
            => new(desc, neutralDrawDescription, 0, 1, 0, 1);

        void BuildPlans(ImmutableList<PenLayer> layers, int frame, int length, int fps)
        {
            plans.Clear();

            if (layers.IsEmpty)
            {
                GetRange(penShapeParameter.Length, penShapeParameter.Offset, layerRenderers[0].TotalPointCount, frame, length, fps, out var pointFrom, out var pointLength);
                plans.Add(new PenLayerPlan(0, pointFrom, pointLength, 100, ProjectBlend.Normal, false, Guid.Empty, Guid.Empty, false, false));
                return;
            }

            visibility.Update(layers);

            var globalTotalPoints = 0;
            var index = 0;
            foreach (var layer in layers)
            {
                if (visibility[index] && !layer.IsRangeOverridden)
                    globalTotalPoints += layerRenderers[index].TotalPointCount;
                index++;
            }
            GetRange(penShapeParameter.Length, penShapeParameter.Offset, globalTotalPoints, frame, length, fps, out var globalPointFrom, out var globalPointLength);

            var basePoint = 0;
            index = 0;
            foreach (var layer in layers)
            {
                if (!visibility[index])
                {
                    index++;
                    continue;
                }

                int pointFrom;
                int pointLength;
                if (layer.IsRangeOverridden)
                {
                    GetRange(layer.Length, layer.Offset, layerRenderers[index].TotalPointCount, frame, length, fps, out pointFrom, out pointLength);
                }
                else
                {
                    pointFrom = globalPointFrom - basePoint;
                    pointLength = globalPointLength;
                    basePoint += layerRenderers[index].TotalPointCount;
                }
                plans.Add(new PenLayerPlan(index, pointFrom, pointLength, layer.Opacity.GetValue(frame, length, fps), layer.BlendMode, layer.IsClipping, layer.Id, layer.ParentId, layer.IsFolder, PenLayerEffectChain.HasEnabledEffect(layer.VideoEffects)));
                index++;
            }
        }

        public void CollectOrder(TimelineItemSourceDescription desc, List<PenOrderEntry> entries)
        {
            entries.Clear();

            var layers = penShapeParameter.Layers;
            if (layers.IsEmpty || layerRenderers.Count != layers.Count)
                return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            visibility.Update(layers);

            var globalTotalPoints = 0;
            var index = 0;
            foreach (var layer in layers)
            {
                if (visibility[index] && !layer.IsRangeOverridden)
                    globalTotalPoints += layerRenderers[index].TotalPointCount;
                index++;
            }
            GetRange(penShapeParameter.Length, penShapeParameter.Offset, globalTotalPoints, frame, length, fps, out var globalPointFrom, out var globalPointLength);

            var number = 0;
            var basePoint = 0;
            index = 0;
            foreach (var layer in layers)
            {
                if (!visibility[index])
                {
                    index++;
                    continue;
                }

                var renderer = layerRenderers[index];
                var count = layer.IsFolder ? 0 : Math.Min(layer.Strokes.Count, renderer.ElementCount);
                if (layer.IsRangeOverridden)
                {
                    GetRange(layer.Length, layer.Offset, renderer.TotalPointCount, frame, length, fps, out var pointFrom, out var pointLength);
                    for (var i = 0; i < count; i++)
                        entries.Add(new PenOrderEntry(index, i, i + 1, true, renderer.IsDrawn(i, pointFrom, pointLength)));
                }
                else
                {
                    for (var i = 0; i < count; i++)
                    {
                        number++;
                        entries.Add(new PenOrderEntry(index, i, number, false, renderer.IsDrawn(i, globalPointFrom - basePoint, globalPointLength)));
                    }
                    basePoint += renderer.TotalPointCount;
                }
                index++;
            }
        }

        bool IsDirectComposition()
        {
            foreach (var plan in plans)
            {
                if (plan.Opacity < 100 || plan.BlendMode != ProjectBlend.Normal || plan.IsClipping || plan.HasEffects)
                    return false;
            }
            return true;
        }

        ID2D1Image Compose(ID2D1DeviceContext6 dc, ID2D1CommandList baseImage, Matrix3x2 transform, double thickness, TimelineItemSourceDescription desc)
        {
            foreach (var plan in plans)
            {
                var layerCommandList = dc.CreateCommandList();
                layerCommandLists.Add(layerCommandList);

                dc.Target = layerCommandList;
                dc.BeginDraw();
                dc.Clear(null);
                dc.DrawRectangle(new Vortice.RawRectF(0, 0, 1, 1), transparent);
                if (!plan.IsFolder)
                {
                    dc.Transform = transform;
                    layerRenderers[plan.Index].Draw(dc, plan.PointFrom, plan.PointLength, thickness, resources, GetWet(plan.Index));
                    dc.Transform = Matrix3x2.Identity;
                }
                dc.EndDraw();
                dc.Target = null;
                layerCommandList.Close();
            }

            chainIndices.Clear();
            chainCount = 0;
            var root = GetChain(Guid.Empty);
            root.Image = baseImage;

            var index = 0;
            foreach (var plan in plans)
            {
                var layerImage = layerCommandLists[index];
                index++;

                ID2D1Image source;
                if (plan.IsFolder)
                {
                    var group = FindChain(plan.Id);
                    if (group is not null)
                        FlushGroup(dc, group);
                    source = GetChainOutput(group) ?? layerImage;
                }
                else
                {
                    source = layerImage;
                }

                if (plan.HasEffects)
                {
                    effectInputs.Add(source);
                    source = layerChains[plan.Index].Apply(source, CreateEffectDescription(desc), out _);
                }
                else
                {
                    effectInputs.Add(null);
                }

                var chain = GetChain(plan.ParentId);
                if (plan.IsClipping && chain.GroupImage is not null && chain.GroupMask is not null)
                {
                    chain.GroupImage = ClipOnto(dc, chain.GroupImage, source, chain.GroupMask, plan);
                    continue;
                }

                FlushGroup(dc, chain);
                if (plan.IsClipping)
                {
                    AddToChain(dc, chain, plan, source);
                    continue;
                }

                chain.GroupImage = source;
                chain.GroupMask = source;
                chain.GroupPlan = plan;
            }

            FlushGroup(dc, root);
            return GetChainOutput(root) ?? baseImage;
        }

        void FlushGroup(ID2D1DeviceContext6 dc, PenComposeChain chain)
        {
            var image = chain.GroupImage;
            if (image is null)
                return;

            var plan = chain.GroupPlan;
            chain.GroupImage = null;
            chain.GroupMask = null;
            AddToChain(dc, chain, plan, image);
        }

        ID2D1Image ClipOnto(ID2D1DeviceContext6 dc, ID2D1Image accumulated, ID2D1Image source, ID2D1Image mask, in PenLayerPlan plan)
        {
            var alphaMask = new D2DEffects.AlphaMask(dc);
            compositionResources.Add(alphaMask);
            alphaMask.SetInput(0, source, true);
            alphaMask.SetInput(1, mask, true);

            ID2D1Effect clipped = alphaMask;
            if (plan.Opacity < 100)
            {
                var opacity = new D2DEffects.Opacity(dc) { Value = (float)(plan.Opacity / 100) };
                compositionResources.Add(opacity);
                opacity.SetInputEffect(0, alphaMask);
                clipped = opacity;
            }

            var node = CreateBlendNode(dc, plan.BlendMode);
            node.SetInput(0, accumulated, true);
            node.SetInputEffect(1, clipped);

            var output = node.Output;
            compositionResources.Add(output);
            return output;
        }

        ID2D1Effect CreateBlendNode(ID2D1DeviceContext6 dc, ProjectBlend blendMode)
        {
            if (blendMode.IsCompositionEffect())
            {
                var composite = new D2DEffects.Composite(dc) { InputCount = 2, Mode = blendMode.ToD2DCompositionMode() };
                compositionResources.Add(composite);
                return composite;
            }

            var blend = new D2DEffects.Blend(dc) { Mode = blendMode.ToD2DBlendMode() };
            compositionResources.Add(blend);
            return blend;
        }

        void AddToChain(ID2D1DeviceContext6 dc, PenComposeChain chain, in PenLayerPlan plan, ID2D1Image source)
        {
            ID2D1Effect? sourceEffect = null;
            if (plan.Opacity < 100)
            {
                var opacity = new D2DEffects.Opacity(dc) { Value = (float)(plan.Opacity / 100) };
                compositionResources.Add(opacity);
                opacity.SetInput(0, source, true);
                sourceEffect = opacity;
            }

            if (chain.Effect is null && chain.Image is null)
            {
                chain.Effect = sourceEffect;
                if (sourceEffect is null)
                    chain.Image = source;
                return;
            }

            var node = CreateBlendNode(dc, plan.BlendMode);
            if (chain.Effect is null)
                node.SetInput(0, chain.Image, true);
            else
                node.SetInputEffect(0, chain.Effect);

            if (sourceEffect is null)
                node.SetInput(1, source, true);
            else
                node.SetInputEffect(1, sourceEffect);

            chain.Effect = node;
            chain.Image = null;
        }

        PenComposeChain GetChain(Guid id)
        {
            if (chainIndices.TryGetValue(id, out var existing))
                return chainPool[existing];

            if (chainCount == chainPool.Count)
                chainPool.Add(new PenComposeChain());

            var chain = chainPool[chainCount];
            chain.Reset();
            chainIndices.Add(id, chainCount);
            chainCount++;
            return chain;
        }

        PenComposeChain? FindChain(Guid id)
            => chainIndices.TryGetValue(id, out var index) ? chainPool[index] : null;

        ID2D1Image? GetChainOutput(PenComposeChain? chain)
        {
            if (chain is null)
                return null;
            if (chain.Effect is null)
                return chain.Image;

            var output = chain.Effect.Output;
            compositionResources.Add(output);
            return output;
        }

        void ReleaseComposition()
        {
            outputImage = null;
            for (var i = compositionResources.Count - 1; i >= 0; i--)
                compositionResources[i].Dispose();
            compositionResources.Clear();
            for (var i = layerCommandLists.Count - 1; i >= 0; i--)
                layerCommandLists[i].Dispose();
            layerCommandLists.Clear();
            effectInputs.Clear();
            for (var i = 0; i < chainPool.Count; i++)
                chainPool[i].Reset();
            chainIndices.Clear();
            chainCount = 0;
        }

        bool IsSamePlans()
        {
            if (plans.Count != previousPlans.Count)
                return false;
            for (var i = 0; i < plans.Count; i++)
            {
                if (!plans[i].Equals(previousPlans[i]))
                    return false;
            }
            return true;
        }

        static void GetRange(Animation lengthAnimation, Animation offsetAnimation, int totalPoints, int frame, int length, int fps, out int pointFrom, out int pointLength)
        {
            if (totalPoints <= 0)
            {
                pointFrom = 0;
                pointLength = 0;
                return;
            }

            var lengthRate = lengthAnimation.GetValue(frame, length, fps);
            var offset = offsetAnimation.GetValue(frame, length, fps);
            var doubleTotalPoints = totalPoints * 2;
            pointFrom = (int)((totalPoints * (offset + 100) / 100 % doubleTotalPoints + doubleTotalPoints) % doubleTotalPoints) - totalPoints;
            pointLength = (int)(totalPoints * lengthRate / 100);
        }

        #region IDisposable
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // マネージド状態を破棄します (マネージド オブジェクト)
                    ReleaseComposition();
                    foreach (var layerRenderer in layerRenderers)
                        layerRenderer.Dispose();
                    layerRenderers.Clear();
                    foreach (var layerChain in layerChains)
                        layerChain.Dispose();
                    layerChains.Clear();
                    disposer.Dispose();
                }

                // アンマネージド リソース (アンマネージド オブジェクト) を解放し、ファイナライザーをオーバーライドします
                // 大きなフィールドを null に設定します
                disposedValue = true;
            }
        }

        // 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
        // ~PenShapeSource()
        // {
        //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
