using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity.Editor
{
    internal static class MNNStudioImage
    {
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
