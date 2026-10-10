using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace MNN.Unity.Editor
{
    internal sealed class MNNStudioAudio : IDisposable
    {
        private AudioClip _recording, _preview;
        private UnityWebRequest _decodeRequest;
        private string _playbackFile, _playbackError;
        private bool _decodingPlayback;
        private string _device;
        private double _started;
        private double _playStarted;
        private float[] _playSamples;
        private int _playRate;
        private static readonly BindingFlags AudioFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo PlayPreviewMethod = AudioUtility?.GetMethod("PlayPreviewClip", AudioFlags, null, new[]{typeof(AudioClip), typeof(int), typeof(bool)}, null);
        private static readonly MethodInfo StopPreviewMethod = AudioUtility?.GetMethod("StopAllPreviewClips", AudioFlags);
        private static readonly MethodInfo IsPlayingMethod = AudioUtility?.GetMethod("IsPreviewClipPlaying", AudioFlags);
        private static readonly MethodInfo PositionMethod = AudioUtility?.GetMethod("GetPreviewClipPosition", AudioFlags);
        private readonly float[] _meter = new float[1024];
        internal static bool EditorAudioMuted => EditorUtility.audioMasterMute;
        internal bool IsLoading => _decodingPlayback;
        internal bool IsPlaying => IsLoading || (_preview != null && _playSamples != null && IsPlayingMethod != null && (bool)IsPlayingMethod.Invoke(null, null));
        internal string ConsumePlaybackError()
        {
            string error = _playbackError;
            _playbackError = null;
            return error;
        }

        internal float Level { get; private set; }

        internal void UpdateLevel()
        {
            Level = 0;
            if (IsRecording)
            {
                int position = Microphone.GetPosition(_device);
                int frames = _meter.Length / Math.Max(1, _recording.channels);
                if (position >= frames && _recording.GetData(_meter, position - frames))
                    Level = Rms(_meter, 0, _meter.Length);
            }
            else if (IsPlaying)
            {
                double seconds = PositionMethod == null ? EditorApplication.timeSinceStartup - _playStarted : Convert.ToDouble(PositionMethod.Invoke(null, null));
                int offset = Math.Max(0, (int)(seconds * _playRate));
                Level = Rms(_playSamples, offset, Math.Min(256, _playSamples.Length - offset));
            }
        }

        internal static float Rms(float[] samples, int offset, int count)
        {
            if (samples == null || offset < 0 || count <= 0 || offset > samples.Length - count)
                return 0;
            double sum = 0;
            for (int i = offset; i < offset + count; ++i)
                sum += samples[i] * samples[i];
            return Mathf.Clamp01((float)Math.Sqrt(sum / count) * 4);
        }

        internal bool IsRecording => _recording != null;
        internal bool CaptureFinished => IsRecording && !Microphone.IsRecording(_device);
        internal double RecordingSeconds => IsRecording ? Math.Min(30, EditorApplication.timeSinceStartup - _started) : 0;
        internal void StartRecording()
        {
            if (IsRecording)
                throw new InvalidOperationException("Recording is already running.");
            if (Microphone.devices.Length == 0)
                throw new InvalidOperationException("No microphone is available. Check Unity's microphone permission in System Settings.");
            _device = Microphone.devices[0];
            if (Microphone.IsRecording(_device))
                throw new InvalidOperationException("The microphone is already in use.");
            StopPreview();
            _recording = Microphone.Start(_device, false, 30, 16000);
            if (_recording == null)
                throw new InvalidOperationException("Unity could not start microphone capture.");
            _started = EditorApplication.timeSinceStartup;
        }

        internal string StopRecording()
        {
            if (!IsRecording)
                throw new InvalidOperationException("There is no active recording.");
            int frames = Microphone.GetPosition(_device);
            if (!Microphone.IsRecording(_device) && RecordingSeconds >= 29)
                frames = _recording.samples;
            Microphone.End(_device);
            try
            {
                if (frames <= 0)
                    throw new InvalidOperationException("The microphone captured no audio. Check microphone permission.");
                frames = Math.Min(frames, _recording.samples);
                var data = new float[_recording.samples * _recording.channels];
                if (!_recording.GetData(data, 0))
                    throw new InvalidOperationException("Unity could not read captured audio.");
                var mono = new float[frames];
                for (int i = 0; i < frames; ++i)
                {
                    float sum = 0;
                    for (int channel = 0; channel < _recording.channels; ++channel)
                        sum += data[i * _recording.channels + channel];
                    mono[i] = sum / _recording.channels;
                }

                string folder = Path.GetFullPath(Path.Combine("Library", "MNN", "Studio", "Recordings"));
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".wav");
                File.WriteAllBytes(path, EncodeWave(mono, _recording.frequency));
                return path;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(_recording);
                _recording = null;
            }
        }

        internal static byte[] EncodeWave(float[] samples, int sampleRate)
        {
            if (samples == null)
                throw new ArgumentNullException(nameof(samples));
            if (samples.Length == 0)
                throw new ArgumentException("Audio is empty.", nameof(samples));
            if (sampleRate < 8000 || sampleRate > 192000)
                throw new ArgumentOutOfRangeException(nameof(sampleRate));
            using (var stream = new MemoryStream(checked(44 + samples.Length * 2)))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + samples.Length * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(samples.Length * 2);
                foreach (float sample in samples)
                {
                    if (float.IsNaN(sample) || float.IsInfinity(sample))
                        throw new ArgumentException("PCM samples must be finite.", nameof(samples));
                    writer.Write((short)(Math.Max(-1f, Math.Min(1f, sample)) * 32767f));
                }

                return stream.ToArray();
            }
        }

        internal void Play(float[] samples, int sampleRate)
        {
            if (samples == null || samples.Length == 0 || sampleRate < 8000 || sampleRate > 192000)
                throw new ArgumentException("No valid generated speech is available.");
            if (EditorAudioMuted)
                throw new InvalidOperationException("Unity Editor audio is muted. Enable Editor audio, then press Play again.");
            foreach (float sample in samples)
                if (float.IsNaN(sample) || float.IsInfinity(sample))
                    throw new ArgumentException("PCM samples must be finite.", nameof(samples));
            StopPreview();
            if (PlayPreviewMethod == null || IsPlayingMethod == null || StopPreviewMethod == null)
                throw new NotSupportedException("Audio preview is unavailable in this Unity version. Save the WAV to play it externally.");
            try
            {
                string folder = Path.GetFullPath(Path.Combine("Library", "MNN", "Studio", "Playback"));
                Directory.CreateDirectory(folder);
                _playbackFile = Path.Combine(folder, Guid.NewGuid().ToString("N") + ".wav");
                File.WriteAllBytes(_playbackFile, EncodeWave(samples, sampleRate));
                _playSamples = samples;
                _playRate = sampleRate;
                _playStarted = EditorApplication.timeSinceStartup;
                _decodeRequest = UnityWebRequestMultimedia.GetAudioClip(new Uri(_playbackFile).AbsoluteUri, AudioType.WAV);
                _decodeRequest.SendWebRequest();
                _decodingPlayback = true;
                EditorApplication.update += PollPlaybackDecode;
            }
            catch
            {
                StopPreview();
                throw;
            }
        }

        private void PollPlaybackDecode()
        {
            if (_decodeRequest == null || !_decodeRequest.isDone)
                return;
            EditorApplication.update -= PollPlaybackDecode;
            _decodingPlayback = false;
            try
            {
                if (_decodeRequest.result != UnityWebRequest.Result.Success)
                    throw new IOException("Unity could not decode the generated WAV: " + _decodeRequest.error);
                _preview = DownloadHandlerAudioClip.GetContent(_decodeRequest);
                if (_preview == null || _preview.samples == 0)
                    throw new InvalidDataException("Unity decoded an empty generated WAV.");
                PlayPreviewMethod.Invoke(null, new object[]{_preview, 0, false});
                if (!IsPlayingMethod.Invoke(null, null).Equals(true))
                    throw new InvalidOperationException("Unity decoded the generated WAV but did not start Editor preview playback.");
            }
            catch (Exception error)
            {
                _playbackError = error.GetBaseException().Message;
                StopPreview();
            }
            finally
            {
                DeletePlaybackFile();
            }
        }

        private static Type AudioUtility => typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AudioUtil");
        internal void StopPreview()
        {
            EditorApplication.update -= PollPlaybackDecode;
            try
            {
                if (_preview != null || _decodeRequest != null)
                    StopPreviewMethod?.Invoke(null, null);
            }
            finally
            {
                if (_decodeRequest != null)
                {
                    if (!_decodeRequest.isDone)
                        _decodeRequest.Abort();
                    _decodeRequest.Dispose();
                    _decodeRequest = null;
                }

                _decodingPlayback = false;
                if (_preview != null)
                    UnityEngine.Object.DestroyImmediate(_preview);
                _preview = null;
                _playSamples = null;
                Level = 0;
                DeletePlaybackFile();
            }
        }

        private void DeletePlaybackFile()
        {
            if (string.IsNullOrEmpty(_playbackFile))
                return;
            try
            {
                if (File.Exists(_playbackFile))
                    File.Delete(_playbackFile);
            }
            finally
            {
                _playbackFile = null;
            }
        }

        internal void CancelRecording()
        {
            if (_recording == null)
                return;
            Microphone.End(_device);
            UnityEngine.Object.DestroyImmediate(_recording);
            _recording = null;
        }

        public void Dispose()
        {
            CancelRecording();
            StopPreview();
        }
    }
}
