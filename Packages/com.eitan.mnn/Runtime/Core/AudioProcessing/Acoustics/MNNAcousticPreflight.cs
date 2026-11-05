using System;
using System.Collections.Generic;
using System.IO;

namespace MNN.Unity
{
    /// <summary>Configuration and local file checks only. Does not load models or prove graph integrity/compatibility.</summary>
    public static class MNNAcousticPreflight
    {
        private const string RepairModel = "Install or repair this model with the model manager, or select its installed folder.";

        /// <summary>Returns all active-stage issues. No downloads, preference changes, graph loading or file writes.</summary>
        public static MNNAcousticCheckReport Check(MNNAcousticSettings settings, string modelsDirectory)
        {
            var entries = new List<MNNAcousticCheckEntry>();
            try
            {
                if (settings == null) throw new ArgumentNullException(nameof(settings));
                settings.Validate();
            }
            catch (ArgumentException error)
            {
                entries.Add(new MNNAcousticCheckEntry("settings.invalid", "Configuration", MNNAcousticCheckSeverity.Error,
                    error.Message, null, "Correct the acoustic settings before starting."));
                return new MNNAcousticCheckReport(entries);
            }
            if (!settings.enabled)
            {
                entries.Add(new MNNAcousticCheckEntry("pipeline.disabled", "Speech input processing", MNNAcousticCheckSeverity.Information,
                    "Shared acoustic processing is disabled; no acoustic models are required."));
                return new MNNAcousticCheckReport(entries);
            }

            if (settings.denoise)
                CheckModel(entries, modelsDirectory, settings.denoiserDirectory, "Noise reduction routing", MNNDtlnConfig.FileName, MNNAcousticModelFiles.Denoiser, MNNNoiseSuppressor.ProfileFile);
            if (settings.wakeWord)
                foreach (var group in MNNWakeWordSettings.Group(settings))
                    CheckModel(entries, modelsDirectory, MNNWakeWordSettings.ModelDirectory(settings, group.Key),
                        group.Key == MNNKeywordLanguage.English ? "English wake model" : "Wake word", MNNKeywordSpotterConfig.FileName, MNNAcousticModelFiles.Wake);
            if (settings.profile == MNNAcousticProfile.AutomaticSpeaker)
            {
                CheckModel(entries, modelsDirectory, settings.separationDirectory, "Separation model folder", MNNMossFormer2Config.FileName, MNNAcousticModelFiles.Separation, MNNSpeechSeparator.ProfileFile);
                CheckModel(entries, modelsDirectory, settings.speakerDirectory, "Speaker embedding model folder", MNNSpeakerModelConfig.FileName, MNNAcousticModelFiles.Speaker, MNNSpeakerEncoder.ProfileFile);
                entries.Add(new MNNAcousticCheckEntry("selection.offline", "Speaker selection", MNNAcousticCheckSeverity.Warning,
                    "Automatic selection runs after the utterance and adds latency."));
            }
            AddRoutingAdvice(entries, settings);
            return new MNNAcousticCheckReport(entries);
        }

        private static void CheckModel(List<MNNAcousticCheckEntry> entries, string root, string configured,
            string stage, string profile, Func<string, string[]> readFiles, Func<string, string> selectProfile = null)
        {
            string directory = configured;
            try
            {
                directory = MNNAcousticModelResources.ResolvePath(root, configured);
                if (!Directory.Exists(directory))
                {
                    entries.Add(Failure("directory.missing", stage, "Model folder was not found.", directory));
                    return;
                }
                profile = selectProfile?.Invoke(directory) ?? profile;
                if (!CheckFile(entries, stage, Path.Combine(directory, profile))) return;
                string[] files;
                try { files = readFiles(directory); }
                catch (Exception error) when (IsFileError(error))
                {
                    entries.Add(Failure("profile.invalid", stage, "Model profile could not be read. " + error.Message,
                        Path.Combine(directory, profile)));
                    return;
                }
                bool complete = true;
                foreach (string file in files)
                    complete &= CheckFile(entries, stage, Path.Combine(directory, file));
                if (complete)
                    entries.Add(new MNNAcousticCheckEntry("model.files-present", stage, MNNAcousticCheckSeverity.Information,
                        "Required local files are present.", directory));
            }
            catch (Exception error) when (IsFileError(error))
            {
                entries.Add(Failure("path.invalid", stage, error.Message, directory));
            }
        }

        private static bool CheckFile(List<MNNAcousticCheckEntry> entries, string stage, string path)
        {
            if (!File.Exists(path))
            {
                entries.Add(Failure("file.missing", stage, "Required model file was not found.", path));
                return false;
            }
            if (new FileInfo(path).Length > 0) return true;
            entries.Add(Failure("file.empty", stage, "Required model file is empty.", path));
            return false;
        }

        private static MNNAcousticCheckEntry Failure(string code, string stage, string message, string path) =>
            new MNNAcousticCheckEntry(code, stage, MNNAcousticCheckSeverity.Error, message, path, RepairModel);

        private static bool IsFileError(Exception error) => error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
            error is ArgumentException || error is NotSupportedException || error is System.Security.SecurityException;

        private static void AddRoutingAdvice(List<MNNAcousticCheckEntry> entries, MNNAcousticSettings settings)
        {
            if (settings.denoise && !settings.enhanceVoiceActivity && !settings.enhanceRecognition && !(settings.wakeWord && settings.enhanceKeywords))
                entries.Add(new MNNAcousticCheckEntry("routing.unused", "Noise reduction routing", MNNAcousticCheckSeverity.Warning,
                    "Noise reduction is enabled but no active consumer uses its output.", null,
                    "Disable noise reduction to save compute, or enable a waveform route."));
            if (settings.denoise && settings.enhanceRecognition)
                entries.Add(new MNNAcousticCheckEntry("recognition.enhanced", "Enhance speech recognition", MNNAcousticCheckSeverity.Warning,
                    "Recognition uses enhanced audio. Compare recognition accuracy with the original waveform."));
        }
    }
}
