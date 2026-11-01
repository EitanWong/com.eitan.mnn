using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal static class MNNStudioImage
    {
        // Decode on the editor thread before handing immutable RGB pixels to a worker.
        internal static MNNRgbImage ReadRgb(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {hideFlags = HideFlags.HideAndDontSave};
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    throw new InvalidDataException("Cannot decode the image. Choose a PNG or JPEG file.");
                var source = texture.GetPixels32();
                var pixels = new byte[checked(source.Length * 3)];
                for (int y = 0; y < texture.height; ++y)
                    for (int x = 0; x < texture.width; ++x)
                    {
                        var color = source[(texture.height - y - 1) * texture.width + x];
                        int offset = (y * texture.width + x) * 3;
                        pixels[offset] = color.r;
                        pixels[offset + 1] = color.g;
                        pixels[offset + 2] = color.b;
                    }
                return new MNNRgbImage(pixels, texture.width, texture.height);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        internal static MNNGeneratedImage DrawDetections(MNNRgbImage image, MNNDetection[] detections)
        {
            byte[] pixels = image.ToArray();
            int thickness = Math.Max(1, Math.Min(image.Width, image.Height) / 300);
            foreach (var box in detections)
            {
                int left = Math.Max(0, Math.Min(image.Width - 1, (int)Math.Floor(box.X)));
                int right = Math.Max(0, Math.Min(image.Width - 1, (int)Math.Ceiling(box.X + box.Width)));
                int top = Math.Max(0, Math.Min(image.Height - 1, (int)Math.Floor(box.Y)));
                int bottom = Math.Max(0, Math.Min(image.Height - 1, (int)Math.Ceiling(box.Y + box.Height)));
                void Pixel(int x, int y)
                {
                    int offset = (y * image.Width + x) * 3;
                    pixels[offset] = (byte)(box.ClassIndex % 2 == 0 ? 255 : 32);
                    pixels[offset + 1] = 192;
                    pixels[offset + 2] = (byte)(box.ClassIndex % 2 == 0 ? 32 : 255);
                }
                for (int edge = 0; edge < thickness; ++edge)
                {
                    for (int x = left; x <= right; ++x)
                    {
                        Pixel(x, Math.Min(bottom, top + edge));
                        Pixel(x, Math.Max(top, bottom - edge));
                    }
                    for (int y = top; y <= bottom; ++y)
                    {
                        Pixel(Math.Min(right, left + edge), y);
                        Pixel(Math.Max(left, right - edge), y);
                    }
                }
            }
            return new MNNGeneratedImage(pixels, image.Width, image.Height, "Object detections", 0);
        }

        internal static MNNGeneratedImage DrawTextRegions(MNNRgbImage image, MNNOcrResult result)
        {
            byte[] pixels = image.ToArray();
            int thickness = Math.Max(1, Math.Min(image.Width, image.Height) / 300);
            foreach (var region in result.Regions)
            {
                var points = region.Box.Corners;
                for (int edge = 0; edge < 4; ++edge)
                {
                    var a = points[edge];
                    var b = points[(edge + 1) % 4];
                    int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(b.x - a.x), Math.Abs(b.y - a.y))));
                    for (int step = 0; step <= steps; ++step)
                    {
                        int x = (int)Math.Round(a.x + (b.x - a.x) * step / steps);
                        int y = (int)Math.Round(a.y + (b.y - a.y) * step / steps);
                        for (int dy = 0; dy < thickness; ++dy)
                        for (int dx = 0; dx < thickness; ++dx)
                        {
                            int px = Math.Max(0, Math.Min(image.Width - 1, x + dx));
                            int py = Math.Max(0, Math.Min(image.Height - 1, y + dy));
                            int offset = (py * image.Width + px) * 3;
                            pixels[offset] = 32; pixels[offset + 1] = 192; pixels[offset + 2] = 255;
                        }
                    }
                }
            }
            return new MNNGeneratedImage(pixels, image.Width, image.Height, "OCR text regions", 0);
        }

        internal static byte[] ReadReference(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false)
            {hideFlags = HideFlags.HideAndDontSave};
            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(path)))
                    throw new InvalidDataException("Cannot decode the reference image.");
                float size = Math.Max(texture.width, texture.height);
                float left = (size - texture.width) / 2, top = (size - texture.height) / 2;
                var rgb = new byte[512 * 512 * 3];
                for (int y = 0; y < 512; ++y)
                    for (int x = 0; x < 512; ++x)
                    {
                        float sx = (x + .5f) * size / 512 - left, sy = (y + .5f) * size / 512 - top;
                        Color color = sx < 0 || sx > texture.width || sy < 0 || sy > texture.height ? Color.black : texture.GetPixelBilinear(sx / texture.width, 1 - sy / texture.height);
                        int index = (y * 512 + x) * 3;
                        rgb[index] = (byte)Mathf.RoundToInt(color.r * 255);
                        rgb[index + 1] = (byte)Mathf.RoundToInt(color.g * 255);
                        rgb[index + 2] = (byte)Mathf.RoundToInt(color.b * 255);
                    }

                return rgb;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // Call only on the Unity editor thread. Model output starts at the top left;
        // Texture2D raw storage starts at the bottom left.
        internal static Texture2D CreateTexture(MNNGeneratedImage image)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0 || image.Rgb.Length != checked(image.Width * image.Height * 3))
                throw new ArgumentException("Invalid generated RGB image.", nameof(image));
            byte[] pixels = new byte[image.Rgb.Length];
            int row = image.Width * 3;
            for (int y = 0; y < image.Height; ++y)
                Array.Copy(image.Rgb, y * row, pixels, (image.Height - 1 - y) * row, row);
            var texture = new Texture2D(image.Width, image.Height, TextureFormat.RGB24, false)
            {hideFlags = HideFlags.HideAndDontSave};
            try
            {
                texture.LoadRawTextureData(pixels);
                texture.Apply();
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }
    }
}
