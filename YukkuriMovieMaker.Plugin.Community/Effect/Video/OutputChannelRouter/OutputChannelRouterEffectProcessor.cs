using Vortice.Direct2D1;
using D2DEffects = Vortice.Direct2D1.Effects;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Player.Video.Effects;

namespace YukkuriMovieMaker.Plugin.Community.Effect.Video.OutputChannelRouter
{
    internal sealed class OutputChannelRouterEffectProcessor : VideoEffectProcessorBase
    {
        private readonly OutputChannelRouterEffect _item;

        private OutputChannelRouterCustomEffect? _routerEffect;
        private D2DEffects.AffineTransform2D? _sink;
        private ID2D1Bitmap? _transparentBitmap;

        private bool _isFirst = true;
        private ChannelSource _outputR, _outputG, _outputB, _outputA;
        private ID2D1Image? _lastBranchInput;

        public OutputChannelRouterEffectProcessor(
            IGraphicsDevicesAndContext devices,
            OutputChannelRouterEffect item)
            : base(devices)
        {
            _item = item;
        }

        protected override ID2D1Image? CreateEffect(IGraphicsDevicesAndContext devices)
        {
            _transparentBitmap = CreateTransparentBitmap(devices);
            if (_transparentBitmap is null)
                return null;
            disposer.Collect(_transparentBitmap);

            var router = new OutputChannelRouterCustomEffect(devices);
            if (!router.IsEnabled)
            {
                router.Dispose();
                return null;
            }
            _routerEffect = router;
            disposer.Collect(_routerEffect);

            _routerEffect.SetBranchInput(_transparentBitmap);

            _sink = new D2DEffects.AffineTransform2D(devices.DeviceContext);
            disposer.Collect(_sink);

            using var routerOutput = _routerEffect.Output;
            _sink.SetInput(0, routerOutput, true);

            var output = _sink.Output;
            disposer.Collect(output);
            return output;
        }

        protected override void setInput(ID2D1Image? input)
        {
            _routerEffect?.SetCurrentInput(input);
        }

        protected override void ClearEffectChain()
        {
            _routerEffect?.SetCurrentInput(null);
            _routerEffect?.SetBranchInput(null);
            _sink?.SetInput(0, null, true);
            _lastBranchInput = null;
        }

        public override DrawDescription Update(EffectDescription effectDescription)
        {
            if (IsPassThroughEffect || _routerEffect is null || _sink is null)
                return effectDescription.DrawDescription;

            var desc = effectDescription.DrawDescription;

            var targetIndex = _item.TargetIndex;
            var outputR = _item.OutputR;
            var outputG = _item.OutputG;
            var outputB = _item.OutputB;
            var outputA = _item.OutputA;

            ID2D1Image? branchInput;
            var needsBranch = NeedsBranchInput(outputR, outputG, outputB, outputA);
            if (needsBranch)
            {
                var cur = desc.GetCustomValue<int>("OutputBranch.CurrentIndex");
                if (targetIndex == cur && input is not null)
                    branchInput = input;
                else if (desc.TryGetCustomValue<ID2D1Image>(out var branchImage, $"OutputBranch.Branch{targetIndex}"))
                    branchInput = branchImage;
                else
                    branchInput = _transparentBitmap;
            }
            else
            {
                branchInput = _transparentBitmap;
            }

            if (_isFirst || !ReferenceEquals(_lastBranchInput, branchInput))
                _routerEffect.SetBranchInput(branchInput);

            if (_isFirst || _outputR != outputR)
                _routerEffect.SourceR = (int)outputR;
            if (_isFirst || _outputG != outputG)
                _routerEffect.SourceG = (int)outputG;
            if (_isFirst || _outputB != outputB)
                _routerEffect.SourceB = (int)outputB;
            if (_isFirst || _outputA != outputA)
                _routerEffect.SourceA = (int)outputA;

            _isFirst = false;
            _outputR = outputR;
            _outputG = outputG;
            _outputB = outputB;
            _outputA = outputA;
            _lastBranchInput = branchInput;

            return desc;
        }

        private static bool NeedsBranchInput(
            ChannelSource r, ChannelSource g, ChannelSource b, ChannelSource a)
        {
            return IsBranchSource(r) || IsBranchSource(g) || IsBranchSource(b) || IsBranchSource(a);
        }

        private static bool IsBranchSource(ChannelSource src) =>
            src is ChannelSource.BranchR
                or ChannelSource.BranchG
                or ChannelSource.BranchB
                or ChannelSource.BranchA
                or ChannelSource.BranchLuminance;

        private static ID2D1Bitmap? CreateTransparentBitmap(IGraphicsDevicesAndContext devices)
        {
            try
            {
                var pixels = new byte[4] { 0, 0, 0, 0 };
                var handle = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
                try
                {
                    var props = new BitmapProperties1(
                        new Vortice.DCommon.PixelFormat(
                            Vortice.DXGI.Format.B8G8R8A8_UNorm,
                            Vortice.DCommon.AlphaMode.Premultiplied),
                        96f, 96f, BitmapOptions.None);
                    return ((ID2D1DeviceContext1)devices.DeviceContext).CreateBitmap(
                        new Vortice.Mathematics.SizeI(1, 1),
                        handle.AddrOfPinnedObject(), 4, props);
                }
                finally
                {
                    handle.Free();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
