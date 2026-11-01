using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace MNN.Unity
{
    // Managed DB approximation: 8-connected outer components, convex hull/minimum rectangle and rectangular
    // offset. Unlike OpenCV RETR_LIST/Clipper, holes are not separate candidates and offsets have square corners.
    internal static class MNNPaddleDbPostprocessor
    {
        internal sealed class Workspace
        {
            internal byte[] Mask;
            internal int[] Queue;
            internal void Clear() { Mask = null; Queue = null; }
        }

        internal static MNNTextBox[] Decode(float[] values, int[] shape, int imageWidth, int imageHeight,
            MNNPaddleOcrConfig config, CancellationToken token = default, Workspace workspace = null)
        {
            if (shape.Length != 4 || shape[0] != 1 || shape[1] != 1 || shape[2] < 1 || shape[3] < 1 ||
                values.Length != (long)shape[2] * shape[3])
                throw new InvalidDataException("Expected DB probability map [1,1,height,width].");
            int width = shape[3], height = shape[2];
            int length = values.Length;
            var mask = workspace?.Mask;
            if (mask == null || mask.Length < length)
                mask = new byte[length];
            if (workspace != null)
                workspace.Mask = mask;
            for (int i = 0; i < values.Length; ++i)
            {
                if ((i & 4095) == 0)
                    token.ThrowIfCancellationRequested();
                float p = values[i];
                if (float.IsNaN(p) || float.IsInfinity(p) || p < 0 || p > 1)
                    throw new InvalidDataException("DB output must contain finite probabilities in [0,1].");
                mask[i] = p > config.pixelThreshold ? (byte)1 : (byte)0;
            }
            var queue = workspace?.Queue;
            var boundary = new List<Vector2>();
            var boxes = new List<MNNTextBox>();
            int candidates = 0;
            for (int seed = 0; seed < length; ++seed)
            {
                if ((seed & 4095) == 0)
                    token.ThrowIfCancellationRequested();
                if (mask[seed] != 1)
                    continue;
                if (++candidates > config.maximumCandidates)
                    break;
                boundary.Clear();
                if (queue == null || queue.Length < length)
                {
                    queue = new int[length];
                    if (workspace != null)
                        workspace.Queue = queue;
                }
                int head = 0, tail = 1;
                queue[0] = seed;
                mask[seed] = 2;
                while (head < tail)
                {
                    if ((head & 4095) == 0)
                        token.ThrowIfCancellationRequested();
                    int current = queue[head++], x = current % width, y = current / width;
                    bool edge = false;
                    for (int dy = -1; dy <= 1; ++dy)
                    for (int dx = -1; dx <= 1; ++dx)
                    {
                        if (dx == 0 && dy == 0)
                            continue;
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || nx >= width || ny < 0 || ny >= height || mask[ny * width + nx] == 0)
                        {
                            edge = true;
                            continue;
                        }
                        int next = ny * width + nx;
                        if (mask[next] == 1)
                        {
                            mask[next] = 2;
                            queue[tail++] = next;
                        }
                    }
                    if (edge)
                        boundary.Add(new Vector2(x, y));
                }
                if (boundary.Count < 4)
                    continue;
                var rectangle = MinimumRectangle(boundary);
                float w = Vector2.Distance(rectangle[0], rectangle[1]), h = Vector2.Distance(rectangle[0], rectangle[3]);
                if (Math.Min(w, h) < 3)
                    continue;
                float score = Score(values, width, height, rectangle, token);
                if (score < config.boxThreshold)
                    continue;
                float distance = w * h * config.unclipRatio / (2 * (w + h));
                var u = (rectangle[1] - rectangle[0]) / w;
                var v = (rectangle[3] - rectangle[0]) / h;
                rectangle[0] -= (u + v) * distance;
                rectangle[1] += (u - v) * distance;
                rectangle[2] += (u + v) * distance;
                rectangle[3] += (v - u) * distance;
                if (Math.Min(w + 2 * distance, h + 2 * distance) < 5)
                    continue;
                var ordered = Order(rectangle);
                for (int i = 0; i < 4; ++i)
                    ordered[i] = new Vector2(
                        (float)Math.Max(0, Math.Min(imageWidth - 1, Math.Round(ordered[i].x / width * imageWidth))),
                        (float)Math.Max(0, Math.Min(imageHeight - 1, Math.Round(ordered[i].y / height * imageHeight))));
                if (Vector2.Distance(ordered[0], ordered[1]) < 3 || Vector2.Distance(ordered[0], ordered[3]) < 3)
                    continue;
                boxes.Add(new MNNTextBox(ordered, score));
            }
            SortReadingOrder(boxes);
            return boxes.ToArray();
        }

        // Monotone-chain hull; examining its edge orientations gives the exact minimum bounding rectangle.
        internal static Vector2[] MinimumRectangle(List<Vector2> points)
        {
            var sorted = points.OrderBy(p => p.x).ThenBy(p => p.y).ToArray();
            var hull = new List<Vector2>();
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lower = hull.Count;
            for (int i = sorted.Length - 2; i >= 0; --i)
            {
                var p = sorted[i];
                while (hull.Count > lower && Cross(hull[hull.Count - 1] - hull[hull.Count - 2], p - hull[hull.Count - 1]) <= 0)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            if (hull.Count > 1)
                hull.RemoveAt(hull.Count - 1);
            if (hull.Count < 3)
                return new[] {sorted[0], sorted[0], sorted[0], sorted[0]};
            double best = double.PositiveInfinity;
            Vector2[] result = null;
            for (int i = 0; i < hull.Count; ++i)
            {
                var u = (hull[(i + 1) % hull.Count] - hull[i]).normalized;
                var v = new Vector2(-u.y, u.x);
                float minU = float.PositiveInfinity, maxU = float.NegativeInfinity, minV = float.PositiveInfinity, maxV = float.NegativeInfinity;
                foreach (var p in hull)
                {
                    float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                    minU = Math.Min(minU, pu); maxU = Math.Max(maxU, pu);
                    minV = Math.Min(minV, pv); maxV = Math.Max(maxV, pv);
                }
                double area = (double)(maxU - minU) * (maxV - minV);
                if (area < best)
                {
                    best = area;
                    result = new[] {u * minU + v * minV, u * maxU + v * minV, u * maxU + v * maxV, u * minU + v * maxV};
                }
            }
            return result;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        private static Vector2[] Order(Vector2[] points)
        {
            var p = points.OrderBy(point => point.x).ThenBy(point => point.y).ToArray();
            var leftTop = p[0].y <= p[1].y ? p[0] : p[1];
            var leftBottom = p[0].y <= p[1].y ? p[1] : p[0];
            var rightTop = p[2].y <= p[3].y ? p[2] : p[3];
            var rightBottom = p[2].y <= p[3].y ? p[3] : p[2];
            return new[] {leftTop, rightTop, rightBottom, leftBottom};
        }

        private static float Score(float[] values, int width, int height, Vector2[] rectangle, CancellationToken token)
        {
            int left = Math.Max(0, (int)Math.Floor(rectangle.Min(p => p.x))), right = Math.Min(width - 1, (int)Math.Ceiling(rectangle.Max(p => p.x)));
            int top = Math.Max(0, (int)Math.Floor(rectangle.Min(p => p.y))), bottom = Math.Min(height - 1, (int)Math.Ceiling(rectangle.Max(p => p.y)));
            var u = rectangle[1] - rectangle[0];
            var v = rectangle[3] - rectangle[0];
            float w = u.sqrMagnitude, h = v.sqrMagnitude;
            double sum = 0;
            int count = 0;
            for (int y = top; y <= bottom; ++y)
            {
                token.ThrowIfCancellationRequested();
                for (int x = left; x <= right; ++x)
                {
                    var p = new Vector2(x, y) - rectangle[0];
                    float pu = Vector2.Dot(p, u), pv = Vector2.Dot(p, v);
                    if (pu >= -.01f && pu <= w + .01f && pv >= -.01f && pv <= h + .01f)
                    {
                        sum += values[y * width + x];
                        ++count;
                    }
                }
            }
            return count == 0 ? 0 : (float)(sum / count);
        }

        internal static void SortReadingOrder(List<MNNTextBox> boxes)
        {
            // Match Paddle's top-to-bottom ordering with same-row left-to-right correction.
            boxes.Sort((a, b) => a.Points[0].y != b.Points[0].y ? a.Points[0].y.CompareTo(b.Points[0].y) : a.Points[0].x.CompareTo(b.Points[0].x));
            for (int i = 1; i < boxes.Count; ++i)
            {
                int j = i;
                while (j > 0 && Math.Abs(boxes[j].Points[0].y - boxes[j - 1].Points[0].y) < 10 && boxes[j].Points[0].x < boxes[j - 1].Points[0].x)
                {
                    var previous = boxes[j - 1]; boxes[j - 1] = boxes[j]; boxes[j] = previous;
                    --j;
                }
            }
        }

        internal static MNNRgbImage Crop(MNNRgbImage image, MNNTextBox box, CancellationToken token = default)
        {
            var p = box.Points;
            int width = Math.Max(1, (int)Math.Max(Vector2.Distance(p[0], p[1]), Vector2.Distance(p[2], p[3])));
            int height = Math.Max(1, (int)Math.Max(Vector2.Distance(p[0], p[3]), Vector2.Distance(p[1], p[2])));
            double dx1 = p[1].x - p[2].x, dx2 = p[3].x - p[2].x, dx3 = p[0].x - p[1].x + p[2].x - p[3].x;
            double dy1 = p[1].y - p[2].y, dy2 = p[3].y - p[2].y, dy3 = p[0].y - p[1].y + p[2].y - p[3].y;
            double denominator = dx1 * dy2 - dx2 * dy1;
            double g = 0, h = 0;
            if (Math.Abs(dx3) + Math.Abs(dy3) > 1e-8)
            {
                if (Math.Abs(denominator) < 1e-8)
                    throw new InvalidDataException("Degenerate OCR crop quadrilateral.");
                g = (dx3 * dy2 - dx2 * dy3) / denominator;
                h = (dx1 * dy3 - dx3 * dy1) / denominator;
            }
            double a = p[1].x - p[0].x + g * p[1].x, b = p[3].x - p[0].x + h * p[3].x;
            double d = p[1].y - p[0].y + g * p[1].y, e = p[3].y - p[0].y + h * p[3].y;
            var pixels = new byte[checked(width * height * 3)];
            var source = image.Pixels;
            bool rotate = height >= width * 1.5f;
            for (int y = 0; y < height; ++y)
            {
                token.ThrowIfCancellationRequested();
                double v = (double)y / height;
                for (int x = 0; x < width; ++x)
                {
                    double u = (double)x / width, w = g * u + h * v + 1;
                    double sx = Math.Max(0, Math.Min(image.Width - 1, (a * u + b * v + p[0].x) / w));
                    double sy = Math.Max(0, Math.Min(image.Height - 1, (d * u + e * v + p[0].y) / w));
                    int x0 = (int)sx, y0 = (int)sy, x1 = Math.Min(x0 + 1, image.Width - 1), y1 = Math.Min(y0 + 1, image.Height - 1);
                    double fx = sx - x0, fy = sy - y0;
                    int target = rotate ? ((width - 1 - x) * height + y) * 3 : (y * width + x) * 3;
                    for (int c = 0; c < 3; ++c)
                    {
                        double first = source[(y0 * image.Width + x0) * 3 + c] * (1 - fx) + source[(y0 * image.Width + x1) * 3 + c] * fx;
                        double second = source[(y1 * image.Width + x0) * 3 + c] * (1 - fx) + source[(y1 * image.Width + x1) * 3 + c] * fx;
                        pixels[target + c] = (byte)Math.Round(first * (1 - fy) + second * fy);
                    }
                }
            }
            return new MNNRgbImage(pixels, rotate ? height : width, rotate ? width : height);
        }
    }
}
