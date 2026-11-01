using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace MNN.Unity
{
    public sealed partial class MNNImageTask
    {
        private MNNImageTaskResult EmbedFace(MNNRgbImage image, CancellationToken token)
        {
            if (image.Width != 112 || image.Height != 112)
                throw new ArgumentException("MobileFaceNet expects a five-point aligned 112x112 RGB face. Use AlignFace on SCRFD keypoints.");
            var output = Run(image, "input.1", "516", 112, 112, 127.5f, 127.5f, token);
            RequireShape(output, 1, 512);
            float[] values = output.Values;
            double norm = Math.Sqrt(values.Sum(v => (double)v * v));
            if (norm < 1e-12) throw new InvalidDataException("Face embedding has zero norm.");
            for (int i = 0; i < values.Length; ++i) values[i] = (float)(values[i] / norm);
            return new MNNImageTaskResult {Vector = values};
        }

        private MNNImageTaskResult Landmarks(MNNRgbImage image, CancellationToken token)
        {
            var output = Run(image, "data", "fc1", 192, 192, 127.5f, 128, token);
            RequireShape(output, 1, 212);
            var points = new MNNImagePoint[106];
            float[] values = output.Values;
            for (int i = 0; i < points.Length; ++i)
                points[i] = new MNNImagePoint((values[i * 2] + 1) * .5f * image.Width, (values[i * 2 + 1] + 1) * .5f * image.Height);
            return new MNNImageTaskResult {Points = points};
        }

        private MNNImageTaskResult DetectFaces(MNNRgbImage image, CancellationToken token)
        {
            const int size = 640;
            float ratio = Math.Min((float)size / image.Width, (float)size / image.Height);
            int width = Math.Max(1, (int)(image.Width * ratio)), height = Math.Max(1, (int)(image.Height * ratio));
            float scale = (float)height / image.Height;
            var input = new float[3 * size * size];
            Array.Fill(input, -127.5f / 128);
            var resized = MNNImageSampling.Tensor(image, width, height, 127.5f, 128, token: token);
            for (int c = 0; c < 3; ++c)
                for (int y = 0; y < height; ++y)
                    Array.Copy(resized, c * width * height + y * width, input, c * size * size + y * size, width);
            token.ThrowIfCancellationRequested();
            var outputs = _graph.RunOutputs(new[] {"443", "468", "493", "446", "471", "496", "449", "474", "499"},
                new MNNGenerationGraph.Input("input.1", input, 1, 3, size, size));
            return DecodeScrfd(outputs, image.Width, image.Height, scale, token);
        }

        internal static MNNImageTaskResult DecodeScrfd(MNNGenerationGraph.Output[] outputs, int imageWidth, int imageHeight, float scale, CancellationToken token = default)
        {
            if (outputs == null || outputs.Length != 9 || !(scale > 0)) throw new InvalidDataException("SCRFD requires nine output heads.");
            var candidates = new List<Tuple<MNNDetection, MNNImagePoint[]>>();
            for (int level = 0; level < 3; ++level)
            {
                int stride = 8 << level, columns = 640 / stride, count = columns * columns * 2;
                float[] scores = outputs[level].Values, boxes = outputs[level + 3].Values, points = outputs[level + 6].Values;
                if (scores.Length != count || boxes.Length != count * 4 || points.Length != count * 10)
                    throw new InvalidDataException("SCRFD head shape does not match its two-anchor stride pyramid.");
                for (int i = 0; i < count; ++i)
                {
                    if ((i & 255) == 0) token.ThrowIfCancellationRequested();
                    if (scores[i] < 0 || scores[i] > 1) throw new InvalidDataException("SCRFD scores must be probabilities.");
                    if (scores[i] < .5f) continue;
                    float x = (i / 2 % columns) * stride, y = (i / 2 / columns) * stride;
                    for (int c = 0; c < 4; ++c) if (boxes[i * 4 + c] < 0) throw new InvalidDataException("SCRFD distances must be nonnegative.");
                    float left = Math.Max(0, (x - boxes[i * 4] * stride) / scale), top = Math.Max(0, (y - boxes[i * 4 + 1] * stride) / scale);
                    float right = Math.Min(imageWidth, (x + boxes[i * 4 + 2] * stride) / scale), bottom = Math.Min(imageHeight, (y + boxes[i * 4 + 3] * stride) / scale);
                    if (right <= left || bottom <= top) continue;
                    var landmarks = new MNNImagePoint[5];
                    for (int j = 0; j < 5; ++j)
                        landmarks[j] = new MNNImagePoint((x + points[i * 10 + j * 2] * stride) / scale, (y + points[i * 10 + j * 2 + 1] * stride) / scale);
                    candidates.Add(Tuple.Create(new MNNDetection(0, "face", scores[i], left, top, right - left, bottom - top), landmarks));
                }
            }
            var kept = new List<Tuple<MNNDetection, MNNImagePoint[]>>();
            foreach (var candidate in candidates.OrderByDescending(pair => pair.Item1.Score))
            {
                if (kept.Any(pair => FaceIoU(pair.Item1, candidate.Item1) > .4f)) continue;
                kept.Add(candidate);
                if (kept.Count == 300) break;
            }
            return new MNNImageTaskResult {Detections = kept.Select(pair => pair.Item1).ToArray(), FaceKeypoints = kept.Select(pair => pair.Item2).ToArray()};
        }

        private static float FaceIoU(MNNDetection a, MNNDetection b)
        {
            float intersection = Math.Max(0, Math.Min(a.X + a.Width, b.X + b.Width) - Math.Max(a.X, b.X)) * Math.Max(0, Math.Min(a.Y + a.Height, b.Y + b.Height) - Math.Max(a.Y, b.Y));
            return intersection / (a.Width * a.Height + b.Width * b.Height - intersection);
        }

        /// <summary>Axis-aligned 192px crop with 1.5x face-box margin for Landmark106.
        /// The returned landmark coordinates are relative to this crop.</summary>
        public static MNNRgbImage CropFace(MNNRgbImage image, MNNDetection face)
        {
            if (image == null || face == null) throw new ArgumentNullException(nameof(image));
            float side = Math.Max(face.Width, face.Height) * 1.5f;
            if (!(side > 0)) throw new ArgumentException("Face box must have positive dimensions.");
            float centerX = face.X + face.Width / 2, centerY = face.Y + face.Height / 2;
            var rgb = new byte[192 * 192 * 3];
            for (int y = 0; y < 192; ++y)
                for (int x = 0; x < 192; ++x)
                    for (int c = 0; c < 3; ++c)
                        rgb[(y * 192 + x) * 3 + c] = MNNImageSampling.Byte(MNNImageSampling.Pixel(image.Pixels, image.Width, image.Height,
                            centerX + (x - 96) * side / 192, centerY + (y - 96) * side / 192, c, true));
            return new MNNRgbImage(rgb, 192, 192);
        }

        /// <summary>Five-point least-squares similarity alignment to the ArcFace 112px template.</summary>
        public static MNNRgbImage AlignFace(MNNRgbImage image, MNNImagePoint[] keypoints)
        {
            if (image == null || keypoints == null || keypoints.Length != 5) throw new ArgumentException("Expected source image and five ordered SCRFD face keypoints.");
            double[] tx = {38.2946, 73.5318, 56.0252, 41.5493, 70.7299}, ty = {51.6963, 51.5014, 71.7366, 92.3655, 92.2041};
            double sx = 0, sy = 0, dx = tx.Average(), dy = ty.Average();
            foreach (var point in keypoints)
            {
                if (point == null || float.IsNaN(point.X) || float.IsInfinity(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.Y)) throw new ArgumentException("Face keypoints must be finite.");
                sx += point.X / 5.0; sy += point.Y / 5.0;
            }
            double norm = 0, aa = 0, bb = 0;
            for (int i = 0; i < 5; ++i)
            {
                double x = keypoints[i].X - sx, y = keypoints[i].Y - sy;
                norm += x * x + y * y;
                aa += x * (tx[i] - dx) + y * (ty[i] - dy);
                bb += x * (ty[i] - dy) - y * (tx[i] - dx);
            }
            if (norm < 1e-6) throw new ArgumentException("Degenerate face keypoints.");
            double a = aa / norm, b = bb / norm, determinant = a * a + b * b;
            if (determinant < 1e-12) throw new ArgumentException("Degenerate face alignment.");
            double translateX = dx - a * sx + b * sy, translateY = dy - b * sx - a * sy;
            var rgb = new byte[112 * 112 * 3];
            for (int y = 0; y < 112; ++y)
                for (int x = 0; x < 112; ++x)
                {
                    float sourceX = (float)((a * (x - translateX) + b * (y - translateY)) / determinant);
                    float sourceY = (float)((-b * (x - translateX) + a * (y - translateY)) / determinant);
                    for (int c = 0; c < 3; ++c) rgb[(y * 112 + x) * 3 + c] = MNNImageSampling.Byte(MNNImageSampling.Pixel(image.Pixels, image.Width, image.Height, sourceX, sourceY, c, true));
                }
            return new MNNRgbImage(rgb, 112, 112);
        }
    }
}
