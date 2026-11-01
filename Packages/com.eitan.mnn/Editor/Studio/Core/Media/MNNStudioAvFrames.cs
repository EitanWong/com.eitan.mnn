using System;
using System.IO;
using System.Linq;

namespace MNN.Unity.Editor
{
    internal static class MNNStudioAvFrames
    {
        // Texture decoding must run on the Editor thread before dispatching the request.
        internal static float[] Read(string directory)
        {
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("Choose the target speaker's 25 FPS face-frame folder.");
            var files = Directory.EnumerateFiles(directory).Where(path =>
                new[] {".png", ".jpg", ".jpeg"}.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal).Take(751).ToArray();
            if (files.Length == 0 || files.Length > 750)
                throw new InvalidDataException("Choose 1–750 target-face PNG/JPEG frames at 25 FPS, named in zero-padded time order.");
            const int pixels = 112 * 112;
            var values = new float[files.Length * pixels];
            for (int frame = 0; frame < files.Length; ++frame)
            {
                var prepared = MNNClearerVoiceFaceFrames.PrepareFrame(MNNStudioImage.ReadRgb(files[frame]));
                Array.Copy(prepared, 0, values, frame * pixels, pixels);
            }
            return values;
        }
    }
}
