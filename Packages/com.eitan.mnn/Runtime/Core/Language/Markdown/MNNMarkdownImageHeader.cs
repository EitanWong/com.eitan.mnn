using System;

namespace MNN.Unity
{
    /// <summary>Reads the PNG/JPEG dimensions before calling Unity's native decoder. Unsupported or
    /// incomplete headers are rejected. This validates dimensions, not image integrity.</summary>
    public static class MNNMarkdownImageHeader
    {
        public static bool TryReadDimensions(byte[] bytes, out int width, out int height)
        {
            width = height = 0;
            if (bytes == null || bytes.Length < 4) return false;
            if (bytes.Length >= 33 && bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 &&
                bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10 &&
                Read32(bytes, 8) == 13 && bytes[12] == 73 && bytes[13] == 72 && bytes[14] == 68 && bytes[15] == 82)
            {
                uint w = Read32(bytes, 16), h = Read32(bytes, 20);
                if (w == 0 || h == 0 || w > int.MaxValue || h > int.MaxValue) return false;
                width = (int)w; height = (int)h; return true;
            }
            return TryReadJpeg(bytes, out width, out height);
        }
        private static bool TryReadJpeg(byte[] bytes, out int width, out int height)
        {
            width = height = 0;
            if (bytes[0] != 255 || bytes[1] != 216) return false;
            int offset = 2;
            while (offset < bytes.Length)
            {
                if (bytes[offset++] != 255) return false;
                while (offset < bytes.Length && bytes[offset] == 255) ++offset;
                if (offset >= bytes.Length) return false;
                int marker = bytes[offset++];
                if (marker == 217 || marker == 0 || marker == 216) return false;
                if (marker == 1 || marker >= 208 && marker <= 215) continue;
                if (bytes.Length - offset < 2) return false;
                int length = (bytes[offset] << 8) | bytes[offset + 1];
                if (length < 2 || length > bytes.Length - offset) return false;
                bool frame = marker >= 192 && marker <= 207 && marker != 196 && marker != 200 && marker != 204;
                if (frame)
                {
                    if (marker > 194 || width != 0 || length < 8 || bytes[offset + 2] != 8) return false;
                    height = (bytes[offset + 3] << 8) | bytes[offset + 4];
                    width = (bytes[offset + 5] << 8) | bytes[offset + 6];
                    int components = bytes[offset + 7];
                    if (width == 0 || height == 0 || components == 0 || length != 8 + components * 3) return false;
                }
                if (marker == 218) return width > 0 && height > 0;
                offset += length;
            }
            width = height = 0; return false;
        }
        private static uint Read32(byte[] bytes, int offset) =>
            ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) | ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];
    }
}
