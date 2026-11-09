using System;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    public static class HarnessArtifactPresentation
    {
        public static int DecodedBytes(HarnessContentPart part) => part.Kind == HarnessContentKind.Base64 ?
            Convert.FromBase64String(part.Payload).Length : Encoding.UTF8.GetByteCount(part.Payload);
        public static string Describe(HarnessContentPart part) => part.Kind + " · " + (part.FileName ?? "content") +
            " · " + (part.MediaType ?? "text/plain") + " · " + DecodedBytes(part) + " bytes";
        public static string Summary(HarnessArtifact[] artifacts, int maximumCharacters = 2000)
        {
            if (maximumCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
            var text = new StringBuilder();
            foreach (var artifact in artifacts ?? new HarnessArtifact[0])
            {
                text.Append("\nArtifact: ").Append(artifact.Name ?? artifact.Id);
                if (artifact.SourceTaskId != null) text.Append(" · source task ").Append(artifact.SourceTaskId);
                foreach (var part in artifact.Parts)
                {
                    text.Append("\n").Append(Describe(part));
                    if (part.Kind != HarnessContentKind.Base64) text.Append(": ").Append(Clip(part.Payload, 500));
                    if (text.Length >= maximumCharacters) break;
                }
                if (text.Length >= maximumCharacters) break;
            }
            return Clip(text.ToString(), maximumCharacters);
        }
        private static string Clip(string text, int maximum) => text.Length <= maximum ? text :
            text.Substring(0, char.IsHighSurrogate(text[maximum - 1]) ? maximum - 1 : maximum);
    }
}
