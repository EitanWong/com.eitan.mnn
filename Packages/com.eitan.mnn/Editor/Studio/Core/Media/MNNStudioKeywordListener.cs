using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Editor
{
    // Unity microphone operations stay on the editor thread; only model inference runs on a worker.
    internal sealed class MNNStudioKeywordListener : IDisposable
    {
        private readonly MNNKeywordSpotter _spotter;
        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();
        private readonly List<float> _queued = new List<float>();
        private readonly string _device;
        private AudioClip _clip;
        private Task<MNNKeywordDetection[]> _pending;
        private int _position;
        private double _lastPoll, _started;
        private bool _disposed, _hasStarted;

        internal MNNStudioKeywordListener(string directory, string keywords, float threshold, float score, int threads)
        {
            if (Microphone.devices.Length == 0) throw new InvalidOperationException("No microphone is available. Check Unity's microphone permission in System Settings.");
            _device = Microphone.devices[0];
            if (Microphone.IsRecording(_device)) throw new InvalidOperationException("The microphone is already in use. Stop the other recording first.");
            try
            {
                _spotter = MNNKeywordSpotter.Load(directory, threads);
                _spotter.Reset(keywords, threshold, score);
                _clip = Microphone.Start(_device, true, 10, 16000);
                if (_clip == null) throw new InvalidOperationException("Unity could not start the microphone. Check its permission in System Settings.");
                if (_clip.frequency != 16000 || _clip.channels != 1)
                    throw new InvalidOperationException("Live KWS needs mono 16 kHz microphone capture. Use Record audio or a WAV for this device.");
                _lastPoll = _started = EditorApplication.timeSinceStartup;
            }
            catch { Dispose(); throw; }
        }

        internal MNNKeywordDetection[] Update()
        {
            if (_disposed) return Array.Empty<MNNKeywordDetection>();
            var matches = Array.Empty<MNNKeywordDetection>();
            if (_pending != null && _pending.IsCompleted)
            {
                var finished = _pending;
                _pending = null;
                matches = finished.GetAwaiter().GetResult();
            }
            double now = EditorApplication.timeSinceStartup;
            int position = Microphone.GetPosition(_device);
            if (position < 0 || now - _lastPoll >= 9)
                throw new InvalidOperationException("Microphone audio was interrupted. Start listening again.");
            if (position != 0) _hasStarted = true;
            if ((!Microphone.IsRecording(_device) || !_hasStarted) && now - _started > 3)
                throw new InvalidOperationException("No microphone audio arrived. Check microphone permission and start listening again.");
            int count = (position - _position + _clip.samples) % _clip.samples;
            if (_queued.Count + count > 32000)
                throw new InvalidOperationException("Keyword detection fell behind live audio. Stop other model tasks and start listening again.");
            if (count > 0)
            {
                var data = new float[count];
                // Unity GetData wraps at the clip boundary for looped capture.
                if (!_clip.GetData(data, _position)) throw new InvalidOperationException("Unity could not read microphone audio.");
                _queued.AddRange(data);
                _position = position;
            }
            _lastPoll = now;
            if (_pending == null && _queued.Count >= 1600)
            {
                var block = _queued.ToArray();
                _queued.Clear();
                _pending = Task.Run(() => _spotter.AcceptWaveform(block, _cancel.Token));
            }
            return matches;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_clip != null)
            {
                Microphone.End(_device);
                UnityEngine.Object.DestroyImmediate(_clip);
                _clip = null;
            }
            _cancel.Cancel();
            _queued.Clear();
            if (_pending == null) { _spotter?.Dispose(); _cancel.Dispose(); }
            else _pending.ContinueWith(task =>
            {
                // Observe cancellation/failure and release only after the active native call has returned.
                if (task.IsFaulted) { var observed = task.Exception; }
                _spotter.Dispose(); _cancel.Dispose();
            }, TaskScheduler.Default);
        }
    }
}
