using System;

namespace MNN.Unity
{
    /// <summary>Owned, uniformly sampled, one-track ClearerVoice face crops: 25 FPS, grayscale [frame,112,112].
    /// Capture must align/crop and normalize with MNNClearerVoiceFaceFrames; do not pass full camera images.</summary>
    public sealed class MNNVisualSpeechFrames
    {
        private readonly float[] _frames;
        public int FrameCount { get; }
        public double StartedAt { get; }
        public double EndedAt => StartedAt + FrameCount / FramesPerSecond;
        public int Width { get; }
        public int Height { get; }
        public double FramesPerSecond { get; }
        public string TrackId { get; }
        public MNNVisualSpeechFrames(float[] frames, int frameCount, double startedAt, string trackId,
            int width = 112, int height = 112, double framesPerSecond = 25)
        {
            MNNPresenceOptions.RequireTime(startedAt, nameof(startedAt));
            MNNPresenceOptions.RequireTime(framesPerSecond, nameof(framesPerSecond));
            if (framesPerSecond < 1 || framesPerSecond > 60 || width < 1 || height < 1 || width > 224 || height > 224)
                throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
            if (frameCount < 1 || frameCount / framesPerSecond > 3 || frames == null || frames.Length != (long)frameCount * width * height)
                throw new ArgumentException("Supply at most three seconds of complete normalized grayscale frames.", nameof(frames));
            if (string.IsNullOrWhiteSpace(trackId)) throw new ArgumentException("Supply one stable target track.", nameof(trackId));
            foreach (float value in frames)
                if (float.IsNaN(value) || float.IsInfinity(value)) throw new ArgumentException("Visual samples must be finite.", nameof(frames));
            _frames = (float[])frames.Clone();
            FrameCount = frameCount;
            StartedAt = startedAt;
            TrackId = trackId;
            Width = width; Height = height; FramesPerSecond = framesPerSecond;
        }
        public float[] CopyFrames() => (float[])_frames.Clone();
    }

    /// <summary>Owned mono PCM, at most three seconds. SpeakerCount=0 means unknown, never single speaker.
    /// All timestamps share the presence controller's monotonic clock; this is a worker block, not a device callback.</summary>
    public sealed class MNNMultimodalSpeechInput
    {
        private readonly float[] _audio;
        public int SampleRate { get; }
        public int SampleCount => _audio.Length;
        public double StartedAt { get; }
        public double EndedAt => StartedAt + (double)SampleCount / SampleRate;
        public string TrackId { get; }
        public int SpeakerCount { get; }
        public MNNVisualSpeechFrames Visual { get; }

        public MNNMultimodalSpeechInput(float[] audio, int sampleRate, double startedAt,
            string trackId, int speakerCount = 0, MNNVisualSpeechFrames visual = null)
        {
            MNNPresenceOptions.RequireTime(startedAt, nameof(startedAt));
            if (sampleRate < 8000 || sampleRate > 96000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            if (audio == null || audio.Length == 0 || audio.Length > sampleRate * 3)
                throw new ArgumentException("Supply nonempty mono PCM, at most three seconds.", nameof(audio));
            if (speakerCount < 0) throw new ArgumentOutOfRangeException(nameof(speakerCount));
            ValidatePcm(audio);
            _audio = (float[])audio.Clone();
            SampleRate = sampleRate;
            StartedAt = startedAt;
            TrackId = trackId;
            SpeakerCount = speakerCount;
            Visual = visual;
        }

        public float[] CopyAudio() => (float[])_audio.Clone();

        internal static void ValidatePcm(float[] audio)
        {
            foreach (float value in audio)
                if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1)
                    throw new ArgumentException("Expected finite normalized PCM in [-1,1].", nameof(audio));
        }
    }
}
