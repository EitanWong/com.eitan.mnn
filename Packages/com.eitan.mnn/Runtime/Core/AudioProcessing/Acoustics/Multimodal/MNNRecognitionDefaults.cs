using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    /// <summary>Complete measured pipeline preset. Supply pinned model paths/revisions through its configuration ID.
    /// Reports must come from real recognition evaluation; no preset is manufactured from tensor parity or model size.</summary>
    public sealed class MNNRecognitionPreset
    {
        private readonly MNNAcousticSettings _acoustics;
        private readonly MNNInteractionSettings _interaction;
        public MNNRecognitionQuality Quality { get; }
        public MNNAcousticSettings AcousticSettings => _acoustics.Snapshot();
        public MNNInteractionSettings InteractionSettings => _interaction.Snapshot();
        public MNNRecognitionPreset(MNNRecognitionQuality quality, MNNAcousticSettings acoustics, MNNInteractionSettings interaction)
        {
            Quality = quality ?? throw new ArgumentNullException(nameof(quality));
            _acoustics = (acoustics ?? throw new ArgumentNullException(nameof(acoustics))).Snapshot();
            _interaction = (interaction ?? throw new ArgumentNullException(nameof(interaction))).Snapshot();
            _acoustics.Validate(); _interaction.Validate();
            _interaction.measuredConfigurationId = quality.ConfigurationId;
        }
    }

    /// <summary>Application-owned immutable candidate catalog. Install evaluated candidates once, select at first-run setup.
    /// Users' saved overrides take precedence. Missing comparable evidence returns null.</summary>
    public sealed class MNNRecognitionDefaults
    {
        private readonly Dictionary<string, MNNRecognitionPreset> _presets = new Dictionary<string, MNNRecognitionPreset>(StringComparer.Ordinal);
        public MNNRecognitionDefaults(IEnumerable<MNNRecognitionPreset> presets)
        {
            if (presets == null) throw new ArgumentNullException(nameof(presets));
            foreach (var preset in presets)
            {
                if (preset == null || _presets.ContainsKey(preset.Quality.ConfigurationId)) throw new ArgumentException("Preset identities must be unique.", nameof(presets));
                _presets.Add(preset.Quality.ConfigurationId, preset);
            }
        }
        public MNNRecognitionPreset Select(string corpusId, string deviceId, string language, string metric, double maximumP95Milliseconds)
        {
            var results = new List<MNNRecognitionQuality>();
            foreach (var preset in _presets.Values) results.Add(preset.Quality);
            var selected = MNNRecognitionQuality.Select(results, corpusId, deviceId, language, metric, maximumP95Milliseconds);
            return selected == null ? null : _presets[selected.ConfigurationId];
        }
    }
}
