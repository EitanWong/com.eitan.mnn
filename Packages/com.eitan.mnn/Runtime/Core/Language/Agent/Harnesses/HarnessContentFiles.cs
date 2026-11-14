using System;
using System.IO;
using System.Linq;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Explicit local file operations. No URI fetch, execution, or overwrite.</summary>
    public static class HarnessContentFiles
    {
        public const int MaximumFileBytes = 180 * 1024;
        public static HarnessContentPart Import(string path)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var output = new MemoryStream())
            {
                if (input.Length > MaximumFileBytes) throw new ArgumentException("Attachment exceeds the 180 KiB file limit. Use a URL reference for larger files.");
                var buffer = new byte[8192];
                int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length + count > MaximumFileBytes) throw new ArgumentException("Attachment grew beyond the file limit.");
                    output.Write(buffer, 0, count);
                }
                if (output.Length == 0) throw new ArgumentException("Choose a nonempty attachment.");
                return new HarnessContentPart(HarnessContentKind.Base64, Convert.ToBase64String(output.ToArray()),
                    MediaType(path), Path.GetFileName(path));
            }
        }
        public static string Export(string directory, HarnessContentPart part)
        {
            if (part == null) throw new ArgumentNullException(nameof(part));
            part.Validate();
            if (part.Kind == HarnessContentKind.Url) throw new ArgumentException("Open URL references explicitly; they are not downloaded by export.");
            byte[] bytes = part.Kind == HarnessContentKind.Base64 ? Convert.FromBase64String(part.Payload) : Encoding.UTF8.GetBytes(part.Payload);
            string name = SafeName(part.FileName ?? (part.Kind == HarnessContentKind.Json ? "result.json" : "result.txt"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(Path.GetFullPath(directory), Guid.NewGuid().ToString("N") + "-" + name);
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes, 0, bytes.Length); output.Flush(); }
            return path;
        }
        private static string SafeName(string value)
        {
            value = value.Replace('\\', '/').Split('/').Last();
            var safe = new string(value.Take(100).Select(c => char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' ? c : '_').ToArray()).Trim('.');
            return string.IsNullOrEmpty(safe) ? "result.bin" : safe;
        }
        private static string MediaType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".txt": case ".md": case ".csv": return "text/plain";
                case ".json": return "application/json";
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".wav": return "audio/wav";
                case ".mp3": return "audio/mpeg";
                case ".pdf": return "application/pdf";
                default: return "application/octet-stream";
            }
        }
    }
}
