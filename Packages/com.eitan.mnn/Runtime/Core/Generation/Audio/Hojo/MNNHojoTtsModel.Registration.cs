namespace MNN.Unity
{
    internal static partial class MNNHojoTtsModel
    {
        internal static MNNModelRegistration Registration => new MNNModelRegistration(
            MNNModelSource.ModelScope, Repository, Revision, Name,
            () => new MNNRemoteModel {
                displayName = "Hojo TTS Light 40M V2 (MNN)", author = "HojoAI / EitanWang",
                family = "Hojo", category = MNNModelCatalogCategory.SpeechSynthesis.ToString(), parameterCount = 75544450,
                tags = new[] { "HojoAI", "Hojo-TTS-Light-40M-V2", "MNN", "zh", "en", "apache-2.0" }, tasks = new[] { "text-to-speech" },
                description = "Hojo V2, 17 preset English/Chinese voices, 24 kHz mono. Dedicated managed frontend and bounded KV cache.",
                supportSummary = "Pinned complete MNN deployment; per-file SHA-256 install checks. Buffered speech, no voice cloning. Native loading uses the package's platform/compiler ABI. See model card for measured quality and backend policy."
            }, Files, IsComplete,
            new MNNRegisteredSpeechAdapter(MNNSpeechModelKind.Hojo, Config, MNNHojoTts.InstalledVoices, LoadSpeech));

        private static MNNSpeechSynthesizer LoadSpeech(string directory, MNNSpeechSynthesisOptions options, int threads, MNNBackendType backend)
        {
            var runner = MNNHojoTts.Load(directory, threads, backend);
            return new MNNSpeechSynthesizer(runner, runner.SampleRate, MNNHojoTts.SupportedLanguages,
                (text, token) => runner.Synthesize(text, options.Voice,
                    options.Seed, cancellationToken: token, speed: options.Speed));
        }
    }
}
