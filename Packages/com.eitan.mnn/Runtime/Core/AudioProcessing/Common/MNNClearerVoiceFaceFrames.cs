using System;

namespace MNN.Unity
{
    /// <summary>Prepares an aligned target-face crop for ClearerVoice audio-visual models at 25 frames per second.</summary>
    public static class MNNClearerVoiceFaceFrames
    {
        /// <summary>Accepts a 112x112 model crop or a 224x224 aligned face (center 112x112).
        /// RGB rows run from top to bottom; output uses OpenCV grayscale and (gray/255-0.4161)/0.1688.</summary>
        public static float[] PrepareFrame(MNNRgbImage crop)
        {
            if (crop == null) throw new ArgumentNullException(nameof(crop));
            if (crop.Width != crop.Height || crop.Width != 112 && crop.Width != 224)
                throw new ArgumentException("Supply a 112x112 model crop or a 224x224 aligned target-face crop.", nameof(crop));
            var result = new float[112 * 112];
            int border = (crop.Width - 112) / 2;
            for (int y = 0; y < 112; ++y)
                for (int x = 0; x < 112; ++x)
                {
                    int source = ((y + border) * crop.Width + x + border) * 3;
                    // Integer BT.601 coefficients match OpenCV's 8-bit RGB-to-gray conversion.
                    int gray = (4899 * crop.Pixels[source] + 9617 * crop.Pixels[source + 1] + 1868 * crop.Pixels[source + 2] + 8192) >> 14;
                    result[y * 112 + x] = (gray / 255f - .4161f) / .1688f;
                }
            return result;
        }
    }
}
