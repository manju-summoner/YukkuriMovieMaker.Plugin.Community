using System.Collections.Immutable;
using System.Numerics;
using System.Reflection.Metadata;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen
{
    internal class PenShapeSource : IShapeSource
    {
        readonly DisposeCollector disposer = new();
        readonly InkResourceManager inkResourceManager = new();
        readonly InkStyleResourceManager inkStyleResourceManager = new();
        readonly SolidColorBrushManager solidColorBrushManager = new();

        readonly IGraphicsDevicesAndContext devices;
        readonly PenShapeParameter penShapeParameter;

        readonly ID2D1SolidColorBrush transparent;

        public ID2D1Image Output => commandList ?? throw new NullReferenceException($"{nameof(commandList)} is null.");
        ID2D1CommandList? commandList;

        bool isEditing;
        ImmutableList<SerializableStroke> strokes = [];
        double thickness;
        int pointFrom, pointLength;

        public PenShapeSource(IGraphicsDevicesAndContext devices, PenShapeParameter penShapeParameter)
        {
            this.devices = devices;
            this.penShapeParameter = penShapeParameter;
            disposer.Collect(inkResourceManager);
            disposer.Collect(inkStyleResourceManager);
            disposer.Collect(solidColorBrushManager);

            transparent = devices.DeviceContext.CreateSolidColorBrush(new Color4(0, 0, 0, 0));
            disposer.Collect(transparent);
        }


        public void Update(TimelineItemSourceDescription desc)
        {
            var dc = devices.DeviceContext;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;

            var thickness = penShapeParameter.Thickness.GetValue(frame, length, fps);
            var lengthRate = penShapeParameter.Length.GetValue(frame, length, fps);
            var offset = penShapeParameter.Offset.GetValue(frame, length, fps);
            var strokes = penShapeParameter.Strokes;
            var isEditing = penShapeParameter.IsEditing;

            var totalPoints = strokes.SelectMany(s => s.ToStroke().GetBezierStylusPoints()).Count();
            var doubleTotalPoints = totalPoints * 2;
            var pointFrom = (int)((totalPoints * (offset + 100) / 100 % doubleTotalPoints + doubleTotalPoints) % doubleTotalPoints) - totalPoints;
            var pointLength = (int)(totalPoints * lengthRate / 100);

            var seed = penShapeParameter.GetHashCode() + desc.ItemPosition.Frame;
            if (commandList is not null
                && this.thickness == thickness
                && this.strokes == strokes
                && this.isEditing == isEditing
                && this.pointFrom == pointFrom
                && this.pointLength == pointLength)
                return;
            this.thickness = thickness;
            this.strokes = strokes;
            this.isEditing = isEditing;
            this.pointFrom = pointFrom;
            this.pointLength = pointLength;

            inkResourceManager.BeginUse();
            inkStyleResourceManager.BeginUse();
            solidColorBrushManager.BeginUse();

            if (commandList is not null)
                disposer.RemoveAndDispose(ref commandList);
            commandList = dc.CreateCommandList();
            disposer.Collect(commandList);

            dc.Target = commandList;
            dc.BeginDraw();
            dc.Clear(null);

            //1pxの透明を描画する
            //これがないとキャンバスが空の場合にcommandListの画面サイズが定まらず、エラーになる
            dc.DrawRectangle(new Vortice.RawRectF(0,0,1,1),transparent);

            if (!penShapeParameter.IsEditing)
            {
                int currentPoint = 0;
                dc.Transform = Matrix3x2.CreateTranslation(-desc.ScreenSize.Width / 2f, -desc.ScreenSize.Height / 2f);
                foreach (var stroke in penShapeParameter.Strokes)
                {
                    var currentStrokeLength = stroke.ToStroke().GetBezierStylusPoints().Count;
                    var start = Math.Max(0, pointFrom - currentPoint);
                    var end = Math.Min(currentStrokeLength, pointFrom + pointLength - currentPoint);
                    currentPoint += currentStrokeLength;
                    if(start >= end)
                        continue;

                    var points =
                        stroke
                        .ToStroke()
                        .GetBezierStylusPoints()
                        .Select(p => new InkPoint()
                        {
                            X = (float)p.X,
                            Y = (float)p.Y,
                            Radius = (float)stroke.DrawingAttributes.Height * p.PressureFactor * (float)thickness / 100f,
                        })
                        .ToArray()
                        [start..end];

                    var ink = inkResourceManager.GetInk(dc, points);
                    var inkStyle = inkStyleResourceManager.GetInkStyle(dc, stroke.DrawingAttributes);

                    Color4 color;
                    if (stroke.DrawingAttributes.IsHighlighter)
                    {
                        dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                        var c = stroke.DrawingAttributes.Color;
                        color = new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f / 2f);
                    }
                    else
                    {
                        dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                        color = stroke.DrawingAttributes.Color.ToColor4();
                    }
                    var brush = solidColorBrushManager.GetBrush(dc, color);

                    dc.DrawInk(ink, brush, inkStyle);
                    dc.PrimitiveBlend = PrimitiveBlend.SourceOver;
                }
                dc.Transform = Matrix3x2.Identity;
            }
            dc.EndDraw();
            dc.Target = null;
            commandList.Close();

            inkResourceManager.EndUse();
            inkStyleResourceManager.EndUse();
            solidColorBrushManager.EndUse();
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