using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Main-thread camera polling with one bounded inference job and no queued frames.
    /// Borrow a playing, permission-approved WebCamTexture and caller-owned pipeline. StopAsync before disposing either.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Face Camera")]
    public sealed class MNNFaceCameraComponent : MonoBehaviour
    {
        [SerializeField] private MNNPresenceComponent _presence;
        [SerializeField, Range(2, 30)] private int _framesPerSecond = 10;
        [SerializeField, Range(2, 10)] private int _sleepFramesPerSecond = 2;
        [SerializeField, Range(160, 1280)] private int _maximumImageEdge = 640;
        private WebCamTexture _camera;
        private IMNNPresenceSource _pipeline;
        private CancellationTokenSource _cancellation;
        private Task<MNNPresenceObservation> _pending;
        private Color32[] _pixels;
        private double _nextCapture;
        private bool _paused;

        public event Action<MNNPresenceObservation> Observation;
        public event Action<MNNRgbImage, double> FrameCaptured;
        public event Action<Exception> Failed;
        public Task Completion => _pending ?? Task.CompletedTask;
        public int CurrentDetectionFramesPerSecond
        {
            get
            {
                if (_presence == null) _presence = GetComponent<MNNPresenceComponent>();
                return _presence != null && _presence.enabled && _presence.Current.State == MNNPresenceState.Sleeping
                    ? _sleepFramesPerSecond : _framesPerSecond;
            }
        }
        internal void BindPresence(MNNPresenceComponent presence) => _presence = presence;
        /// <summary>Latest completed observation on the main thread; cleared on stop, failure or replacement.
        /// Consumers must check CapturedAt before displaying geometry over a live frame.</summary>
        public MNNPresenceObservation? LatestObservation { get; private set; }

        /// <summary>Main thread only. Does not request permissions, start the camera, load or dispose models.</summary>
        public void StartTracking(WebCamTexture camera, IMNNPresenceSource pipeline)
        {
            if (!isActiveAndEnabled) throw new InvalidOperationException("Enable the camera component before tracking.");
            if (_pending != null && !_pending.IsCompleted) throw new InvalidOperationException("Await StopAsync before replacing the pipeline.");
            if (camera == null || !camera.isPlaying) throw new ArgumentException("Supply a playing permission-approved camera.", nameof(camera));
            if (pipeline == null) throw new ArgumentNullException(nameof(pipeline));
            StopTracking();
            if (_pending != null) { var error = _pending.Exception; _pending = null; }
            _pipeline = pipeline;
            if (_presence == null) _presence = GetComponent<MNNPresenceComponent>();
            _pipeline.Reset();
            _camera = camera;
            _cancellation = new CancellationTokenSource();
            _nextCapture = 0;
        }

        /// <summary>Apply bounded analysis polling independently of the negotiated camera capture rate.</summary>
        public void Configure(MNNCameraSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            settings.Validate();
            _framesPerSecond = settings.DetectionFramesPerSecond;
            _sleepFramesPerSecond = settings.SleepFramesPerSecond;
            _maximumImageEdge = settings.MaximumImageEdge;
        }

        public async Task StopAsync()
        {
            var pending = _pending;
            StopTracking();
            if (pending != null)
                try { await pending; } catch (OperationCanceledException) { }
        }

        private void StopTracking()
        {
            LatestObservation = null;
            var cancellation = _cancellation;
            _cancellation = null;
            _camera = null;
            _pipeline = null;
            _pixels = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            if (_pending == null || _pending.IsCompleted)
            { if (_pending != null) { var error = _pending.Exception; } cancellation.Dispose(); }
            else _pending.ContinueWith(task => { var error = task.Exception; cancellation.Dispose(); }, TaskScheduler.Default);
        }

        private void Update()
        {
            if (_pending != null && _pending.IsCompleted)
            {
                var completed = _pending;
                _pending = null;
                if (completed.IsFaulted)
                {
                    var error = completed.Exception.GetBaseException();
                    StopTracking();
                    Failed?.Invoke(error);
                    return;
                }
                if (!completed.IsCanceled && _pipeline != null)
                {
                    var result = completed.Result;
                    LatestObservation = result;
                    if (_presence != null) _presence.Report(result);
                    Observation?.Invoke(result);
                }
            }
            if (_paused || _pipeline == null || _camera == null || !_camera.isPlaying || _pending != null || !_camera.didUpdateThisFrame)
                return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < _nextCapture || _camera.width <= 16 || _camera.height <= 16) return;
            _nextCapture = now + 1.0 / Mathf.Max(1, CurrentDetectionFramesPerSecond);
            try
            {
                _pixels = _camera.GetPixels32(_pixels);
                var image = ReadImage(_pixels, _camera.width, _camera.height, _camera.videoRotationAngle,
                    _camera.videoVerticallyMirrored, _maximumImageEdge);
                FrameCaptured?.Invoke(image, now);
                var pipeline = _pipeline;
                var token = _cancellation.Token;
                _pending = Task.Run(() => pipeline.Process(image, now, token), token);
            }
            catch (Exception error) { StopTracking(); Failed?.Invoke(error); }
        }

        internal static MNNRgbImage ReadImage(Color32[] pixels, int width, int height, int clockwiseRotation,
            bool verticallyMirrored, int maximumEdge)
        {
            if (pixels == null || width < 1 || height < 1 || pixels.Length != (long)width * height || maximumEdge < 1 || maximumEdge > 1280)
                throw new ArgumentException("Expected complete camera pixels and positive dimensions.");
            int rotation = ((clockwiseRotation % 360) + 360) % 360;
            if (rotation % 90 != 0) throw new ArgumentOutOfRangeException(nameof(clockwiseRotation));
            int orientedWidth = rotation % 180 == 0 ? width : height;
            int orientedHeight = rotation % 180 == 0 ? height : width;
            double scale = Math.Min(1, (double)maximumEdge / Math.Max(orientedWidth, orientedHeight));
            int outputWidth = Math.Max(1, (int)(orientedWidth * scale));
            int outputHeight = Math.Max(1, (int)(orientedHeight * scale));
            var rgb = new byte[checked(outputWidth * outputHeight * 3)];
            for (int y = 0; y < outputHeight; ++y)
                for (int x = 0; x < outputWidth; ++x)
                {
                    int ox = Math.Min(orientedWidth - 1, (int)((x + .5) * orientedWidth / outputWidth));
                    int oy = Math.Min(orientedHeight - 1, (int)((y + .5) * orientedHeight / outputHeight));
                    int sx, sy;
                    switch (rotation)
                    {
                        case 90: sx = oy; sy = height - 1 - ox; break;
                        case 180: sx = width - 1 - ox; sy = height - 1 - oy; break;
                        case 270: sx = width - 1 - oy; sy = ox; break;
                        default: sx = ox; sy = oy; break;
                    }
                    int row = verticallyMirrored ? sy : height - 1 - sy;
                    var pixel = pixels[row * width + sx];
                    int index = (y * outputWidth + x) * 3;
                    rgb[index] = pixel.r; rgb[index + 1] = pixel.g; rgb[index + 2] = pixel.b;
                }
            return new MNNRgbImage(rgb, outputWidth, outputHeight);
        }

        private void OnApplicationPause(bool paused) => _paused = paused;
        private void OnDisable() => StopTracking();
    }
}
