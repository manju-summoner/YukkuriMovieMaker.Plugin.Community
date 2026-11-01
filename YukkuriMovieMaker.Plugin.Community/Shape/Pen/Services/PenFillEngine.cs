using System.Windows;
using System.Windows.Media.Imaging;

namespace YukkuriMovieMaker.Plugin.Community.Shape.Pen.Services
{
    class PenFillEngine
    {
        const int DirectionRight = 0;
        const int DirectionDown = 1;
        const int DirectionLeft = 2;
        const int DirectionUp = 3;
        const int DirectionCount = 4;
        const int InitialCapacity = 1024;
        const double MinFigureArea = 1;

        static readonly double SimplifyTolerance = Math.Sqrt(2);

        byte[] region = [];
        byte[] outgoing = [];
        byte[] centerline = [];
        int[] stack = [];
        int[] frontier = [];
        int[] nextFrontier = [];
        int[] ranges = [];
        int[] lengths = [];
        bool[] kept = [];
        Point[] loop = [];
        Point[] vertices = [];

        int width;
        int height;
        int pitch;
        int stackCount;
        int frontierCount;
        int nextCount;
        int rangeCount;
        int loopCount;
        int vertexCount;
        int figureCount;

        public bool TryFill(WriteableBitmap image, IEnumerable<SerializableStroke> boundaries, Point seed, int difference, out SerializableStylusPoint[] points, out int[] figures)
        {
            points = [];
            figures = [];

            var imageWidth = image.PixelWidth;
            var imageHeight = image.PixelHeight;
            var seedX = (int)Math.Floor(seed.X);
            var seedY = (int)Math.Floor(seed.Y);
            if (imageWidth <= 0 || imageHeight <= 0 || seedX < 0 || seedY < 0 || seedX >= imageWidth || seedY >= imageHeight)
                return false;

            EnsureBuffers(imageWidth, imageHeight);
            var reach = MarkCenterlines(boundaries);

            int left, top, right, bottom;
            image.Lock();
            try
            {
                if (!Flood(image, seedX, seedY, difference, out left, out top, out right, out bottom))
                    return false;

                Grow(image, seedX, seedY, difference, reach, ref left, ref top, ref right, ref bottom);
            }
            finally
            {
                image.Unlock();
            }

            Trace(left, top, right, bottom);
            if (figureCount == 0)
                return false;

            points = new SerializableStylusPoint[vertexCount];
            for (var i = 0; i < vertexCount; i++)
                points[i] = new SerializableStylusPoint(vertices[i].X, vertices[i].Y, SerializableStylusPoint.NeutralPressure);
            figures = new int[figureCount];
            Array.Copy(lengths, figures, figureCount);
            return true;
        }

        void EnsureBuffers(int imageWidth, int imageHeight)
        {
            pitch = imageWidth + 1;
            if (width != imageWidth || height != imageHeight)
            {
                width = imageWidth;
                height = imageHeight;
                region = new byte[imageWidth * imageHeight];
                outgoing = new byte[pitch * (imageHeight + 1)];
                centerline = new byte[(imageWidth * imageHeight + 7) / 8];
                return;
            }

            Array.Clear(region);
            Array.Clear(outgoing);
            Array.Clear(centerline);
        }

        int MarkCenterlines(IEnumerable<SerializableStroke> boundaries)
        {
            var radius = 0.0;
            foreach (var stroke in boundaries)
            {
                if (stroke.FillFigures is not null)
                    continue;

                var size = stroke.DrawingAttributes.Height;
                if (size > radius)
                    radius = size;

                var points = stroke.StylusPoints;
                if (points.Length == 1)
                    MarkPixel((int)Math.Floor(points[0].X), (int)Math.Floor(points[0].Y));
                for (var i = 1; i < points.Length; i++)
                    MarkSegment(points[i - 1], points[i]);
            }

            var reach = (int)Math.Ceiling(radius) + 1;
            var limit = Math.Max(width, height);
            return reach < limit ? reach : limit;
        }

        void MarkSegment(SerializableStylusPoint from, SerializableStylusPoint to)
        {
            var x = (int)Math.Floor(from.X);
            var y = (int)Math.Floor(from.Y);
            var lastX = (int)Math.Floor(to.X);
            var lastY = (int)Math.Floor(to.Y);
            var deltaX = Math.Abs(lastX - x);
            var deltaY = Math.Abs(lastY - y);
            var stepX = x < lastX ? 1 : -1;
            var stepY = y < lastY ? 1 : -1;
            var error = deltaX - deltaY;

            MarkPixel(x, y);
            while (x != lastX || y != lastY)
            {
                var doubled = error * 2;
                var isMoved = false;
                if (doubled > -deltaY)
                {
                    error -= deltaY;
                    x += stepX;
                    MarkPixel(x, y);
                    isMoved = true;
                }
                if (doubled < deltaX)
                {
                    error += deltaX;
                    y += stepY;
                    MarkPixel(x, y);
                    isMoved = true;
                }
                if (!isMoved)
                    return;
            }
        }

        void MarkPixel(int x, int y)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            var index = y * width + x;
            centerline[index >> 3] |= (byte)(1 << (index & 7));
        }

        bool IsCenterline(int index) => (centerline[index >> 3] & (1 << (index & 7))) != 0;

        unsafe bool Flood(WriteableBitmap image, int seedX, int seedY, int difference, out int left, out int top, out int right, out int bottom)
        {
            var pixels = (byte*)image.BackBuffer;
            var stride = (nint)image.BackBufferStride;
            var seed = *(uint*)(pixels + seedY * stride + seedX * 4);

            left = right = seedX;
            top = bottom = seedY;
            stackCount = 0;
            Push(seedX, seedY);

            var map = region;
            var isFilled = false;
            while (stackCount > 0)
            {
                var index = stack[--stackCount];
                var y = index / width;
                var x = index - y * width;
                var row = y * width;
                var pixelRow = pixels + y * stride;
                if (map[row + x] != 0 || !IsMatch(pixelRow, x, seed, difference))
                    continue;

                var spanLeft = x;
                while (spanLeft > 0 && map[row + spanLeft - 1] == 0 && IsMatch(pixelRow, spanLeft - 1, seed, difference))
                    spanLeft--;
                var spanRight = x;
                while (spanRight < width - 1 && map[row + spanRight + 1] == 0 && IsMatch(pixelRow, spanRight + 1, seed, difference))
                    spanRight++;

                Array.Fill(map, (byte)1, row + spanLeft, spanRight - spanLeft + 1);
                isFilled = true;

                if (spanLeft < left)
                    left = spanLeft;
                if (spanRight > right)
                    right = spanRight;
                if (y < top)
                    top = y;
                if (y > bottom)
                    bottom = y;

                if (y > 0)
                    ScanRow(pixelRow - stride, y - 1, spanLeft, spanRight, seed, difference);
                if (y < height - 1)
                    ScanRow(pixelRow + stride, y + 1, spanLeft, spanRight, seed, difference);
            }
            return isFilled;
        }

        unsafe void ScanRow(byte* pixelRow, int y, int spanLeft, int spanRight, uint seed, int difference)
        {
            var row = y * width;
            var map = region;
            var isInside = false;
            for (var x = spanLeft; x <= spanRight; x++)
            {
                if (map[row + x] != 0 || !IsMatch(pixelRow, x, seed, difference))
                {
                    isInside = false;
                    continue;
                }

                if (isInside)
                    continue;
                Push(x, y);
                isInside = true;
            }
        }

        static unsafe bool IsMatch(byte* pixelRow, int x, uint seed, int difference)
        {
            var value = *(uint*)(pixelRow + x * 4);
            if (value == seed)
                return true;

            for (var shift = 0; shift < 32; shift += 8)
            {
                var delta = (int)((value >> shift) & 0xFF) - (int)((seed >> shift) & 0xFF);
                if (delta > difference || -delta > difference)
                    return false;
            }
            return true;
        }

        void Push(int x, int y)
        {
            if (stackCount == stack.Length)
                Array.Resize(ref stack, stack.Length == 0 ? InitialCapacity : stack.Length * 2);
            stack[stackCount++] = y * width + x;
        }

        unsafe void Grow(WriteableBitmap image, int seedX, int seedY, int difference, int reach, ref int left, ref int top, ref int right, ref int bottom)
        {
            if (reach <= 0)
                return;

            var pixels = (byte*)image.BackBuffer;
            var stride = (nint)image.BackBufferStride;
            var seed = *(uint*)(pixels + seedY * stride + seedX * 4);
            var map = region;
            left = Math.Max(0, left - 1);
            top = Math.Max(0, top - 1);
            right = Math.Min(width - 1, right + 1);
            bottom = Math.Min(height - 1, bottom + 1);

            nextCount = 0;
            for (var y = top; y <= bottom; y++)
            {
                var row = y * width;
                var pixelRow = pixels + y * stride;
                for (var x = left; x <= right; x++)
                {
                    var index = row + x;
                    if (map[index] != 0 || IsMatch(pixelRow, x, seed, difference) || !HasFilled(map, x, y))
                        continue;

                    map[index] = 2;
                    PushNext(index);
                }
            }
            Swap();

            for (var step = 1; step < reach && frontierCount > 0; step++)
            {
                nextCount = 0;
                for (var i = 0; i < frontierCount; i++)
                {
                    var index = frontier[i];
                    if (IsCenterline(index))
                        continue;

                    var y = index / width;
                    var x = index - y * width;
                    var fromY = Math.Max(0, y - 1);
                    var toY = Math.Min(height - 1, y + 1);
                    var fromX = Math.Max(0, x - 1);
                    var toX = Math.Min(width - 1, x + 1);
                    for (var neighborY = fromY; neighborY <= toY; neighborY++)
                    {
                        var row = neighborY * width;
                        var neighborRow = pixels + neighborY * stride;
                        for (var neighborX = fromX; neighborX <= toX; neighborX++)
                        {
                            var neighbor = row + neighborX;
                            if (map[neighbor] != 0 || IsMatch(neighborRow, neighborX, seed, difference))
                                continue;

                            map[neighbor] = 2;
                            PushNext(neighbor);
                            if (neighborX < left)
                                left = neighborX;
                            if (neighborX > right)
                                right = neighborX;
                            if (neighborY < top)
                                top = neighborY;
                            if (neighborY > bottom)
                                bottom = neighborY;
                        }
                    }
                }
                Swap();
            }
        }

        void Swap()
        {
            (frontier, nextFrontier) = (nextFrontier, frontier);
            frontierCount = nextCount;
        }

        bool HasFilled(byte[] map, int x, int y)
        {
            var fromY = Math.Max(0, y - 1);
            var toY = Math.Min(height - 1, y + 1);
            var fromX = Math.Max(0, x - 1);
            var toX = Math.Min(width - 1, x + 1);
            for (var neighborY = fromY; neighborY <= toY; neighborY++)
            {
                var row = neighborY * width;
                for (var neighborX = fromX; neighborX <= toX; neighborX++)
                {
                    if (map[row + neighborX] == 1)
                        return true;
                }
            }
            return false;
        }

        void PushFrontier(int index)
        {
            if (frontierCount == frontier.Length)
                Array.Resize(ref frontier, frontier.Length == 0 ? InitialCapacity : frontier.Length * 2);
            frontier[frontierCount++] = index;
        }

        void PushNext(int index)
        {
            if (nextCount == nextFrontier.Length)
                Array.Resize(ref nextFrontier, nextFrontier.Length == 0 ? InitialCapacity : nextFrontier.Length * 2);
            nextFrontier[nextCount++] = index;
        }

        void Trace(int left, int top, int right, int bottom)
        {
            vertexCount = 0;
            figureCount = 0;

            var map = region;
            var edges = outgoing;
            for (var y = top; y <= bottom; y++)
            {
                var row = y * width;
                var above = row - width;
                var below = row + width;
                var hasAbove = y > 0;
                var hasBelow = y < height - 1;
                var vertexRow = y * pitch;
                var nextVertexRow = vertexRow + pitch;
                for (var x = left; x <= right; x++)
                {
                    if (map[row + x] == 0)
                        continue;

                    if (!hasAbove || map[above + x] == 0)
                        edges[vertexRow + x + 1] |= 1 << DirectionLeft;
                    if (!hasBelow || map[below + x] == 0)
                        edges[nextVertexRow + x] |= 1 << DirectionRight;
                    if (x == 0 || map[row + x - 1] == 0)
                        edges[vertexRow + x] |= 1 << DirectionDown;
                    if (x == width - 1 || map[row + x + 1] == 0)
                        edges[nextVertexRow + x + 1] |= 1 << DirectionUp;
                }
            }

            for (var y = top; y <= bottom + 1; y++)
            {
                for (var x = left; x <= right + 1; x++)
                {
                    while (outgoing[y * pitch + x] != 0)
                        Walk(x, y);
                }
            }
        }

        void Walk(int startX, int startY)
        {
            var x = startX;
            var y = startY;
            var direction = GetFirstDirection(outgoing[y * pitch + x]);
            loopCount = 0;
            while (direction >= 0)
            {
                outgoing[y * pitch + x] &= (byte)~(1 << direction);
                AppendLoop(x, y);
                x += GetDeltaX(direction);
                y += GetDeltaY(direction);
                if (x == startX && y == startY)
                    break;
                direction = GetNextDirection(outgoing[y * pitch + x], direction);
            }
            Emit();
        }

        static int GetFirstDirection(byte bits)
        {
            for (var direction = 0; direction < DirectionCount; direction++)
            {
                if ((bits & (1 << direction)) != 0)
                    return direction;
            }
            return -1;
        }

        static int GetNextDirection(byte bits, int incoming)
        {
            var left = (incoming + DirectionCount - 1) % DirectionCount;
            if ((bits & (1 << left)) != 0)
                return left;
            if ((bits & (1 << incoming)) != 0)
                return incoming;
            var right = (incoming + 1) % DirectionCount;
            return (bits & (1 << right)) != 0 ? right : -1;
        }

        static int GetDeltaX(int direction) => direction switch
        {
            DirectionRight => 1,
            DirectionLeft => -1,
            _ => 0,
        };

        static int GetDeltaY(int direction) => direction switch
        {
            DirectionDown => 1,
            DirectionUp => -1,
            _ => 0,
        };

        void AppendLoop(int x, int y)
        {
            if (loopCount == loop.Length)
                Array.Resize(ref loop, loop.Length == 0 ? InitialCapacity : loop.Length * 2);
            loop[loopCount++] = new Point(x, y);
        }

        void Emit()
        {
            if (loopCount < PenFillFigures.MinLength || Math.Abs(GetArea()) < MinFigureArea)
                return;

            var count = Simplify();
            if (count < PenFillFigures.MinLength)
                return;

            for (var i = 0; i < loopCount; i++)
            {
                if (kept[i])
                    AppendVertex(loop[i]);
            }
            AppendLength(count);
        }

        double GetArea()
        {
            var area = 0.0;
            var previous = loop[loopCount - 1];
            for (var i = 0; i < loopCount; i++)
            {
                var current = loop[i];
                area += previous.X * current.Y - current.X * previous.Y;
                previous = current;
            }
            return area / 2;
        }

        int Simplify()
        {
            if (kept.Length < loopCount)
                kept = new bool[loopCount];
            Array.Clear(kept, 0, loopCount);

            var farthest = GetFarthest();
            kept[0] = true;
            kept[farthest] = true;
            SimplifyRange(0, farthest);
            SimplifyRange(farthest, loopCount);

            var count = 0;
            for (var i = 0; i < loopCount; i++)
            {
                if (kept[i])
                    count++;
            }
            return count;
        }

        int GetFarthest()
        {
            var origin = loop[0];
            var farthest = 1;
            var distance = -1.0;
            for (var i = 1; i < loopCount; i++)
            {
                var dx = loop[i].X - origin.X;
                var dy = loop[i].Y - origin.Y;
                var current = dx * dx + dy * dy;
                if (current <= distance)
                    continue;
                distance = current;
                farthest = i;
            }
            return farthest;
        }

        void SimplifyRange(int first, int last)
        {
            rangeCount = 0;
            PushRange(first, last);
            while (rangeCount > 0)
            {
                var end = ranges[--rangeCount];
                var start = ranges[--rangeCount];
                if (end - start < 2)
                    continue;

                var from = GetLoopPoint(start);
                var to = GetLoopPoint(end);
                var farthest = -1;
                var distance = SimplifyTolerance;
                for (var i = start + 1; i < end; i++)
                {
                    var current = GetDistance(GetLoopPoint(i), from, to);
                    if (current <= distance)
                        continue;
                    distance = current;
                    farthest = i;
                }
                if (farthest < 0)
                    continue;

                kept[farthest] = true;
                PushRange(start, farthest);
                PushRange(farthest, end);
            }
        }

        Point GetLoopPoint(int index) => loop[index == loopCount ? 0 : index];

        void PushRange(int start, int end)
        {
            if (rangeCount + 2 > ranges.Length)
                Array.Resize(ref ranges, ranges.Length == 0 ? InitialCapacity : ranges.Length * 2);
            ranges[rangeCount++] = start;
            ranges[rangeCount++] = end;
        }

        static double GetDistance(Point point, Point from, Point to)
        {
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var lengthSquared = dx * dx + dy * dy;
            var px = point.X - from.X;
            var py = point.Y - from.Y;
            if (lengthSquared <= 0)
                return Math.Sqrt(px * px + py * py);

            var rate = Math.Clamp((px * dx + py * dy) / lengthSquared, 0, 1);
            var offsetX = px - dx * rate;
            var offsetY = py - dy * rate;
            return Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
        }

        void AppendVertex(Point point)
        {
            if (vertexCount == vertices.Length)
                Array.Resize(ref vertices, vertices.Length == 0 ? InitialCapacity : vertices.Length * 2);
            vertices[vertexCount++] = point;
        }

        void AppendLength(int length)
        {
            if (figureCount == lengths.Length)
                Array.Resize(ref lengths, lengths.Length == 0 ? InitialCapacity : lengths.Length * 2);
            lengths[figureCount++] = length;
        }
    }
}
