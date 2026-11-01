using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Owns one realtime session across Unity lifecycle events; concrete backends are injected by the application.</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MNN/Realtime/Session")]
    public sealed class MNNRealtimeSessionComponent : MonoBehaviour
    {
        private int _mainThreadId;
        private MNNRealtimeSession _session;
        private Task _stopTask;
        private MNNRealtimeSessionState _lastReportedState;
        private Exception _lastReportedFault;
        private Exception _lifecycleError;
        private MNNRealtimeReport _preflight;
        private MNNRealtimeFallbackRuntime _fallbackRuntime;

        public event Action<MNNRealtimeSessionState, Exception> StateChanged;

        public MNNRealtimeSessionState State => _session?.State ?? MNNRealtimeSessionState.Created;
        public Exception Fault => _session?.Fault ?? Volatile.Read(ref _lifecycleError);
        public MNNRealtimeSession Session => _session;
        public MNNRealtimeOutputPipeline Output => _session?.Output;
        public MNNRealtimeReport Preflight => _fallbackRuntime != null ? _fallbackRuntime.CurrentReport : _preflight;
        public bool IsConfigured => _session != null;

        private void Awake()
        {
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        /// <summary>Injects fallback through the same session owner, using its shared output and live route report.</summary>
        public void Configure(MNNRealtimeFallbackRuntime runtime)
        {
            if (runtime == null)
                throw new ArgumentNullException(nameof(runtime));
            Configure(runtime, runtime.Output, runtime.CurrentReport);
        }

        /// <summary>Injects the sole session runtime and shared output pipeline before starting.</summary>
        public void Configure(IMNNRealtimeSessionRuntime runtime, MNNRealtimeOutputPipeline output,
            MNNRealtimeReport preflight)
        {
            EnsureMainThread();
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("The realtime session component must be active when configured.");
            if (_session != null)
                throw new InvalidOperationException("A realtime session component can only be configured once.");
            if (preflight == null)
                throw new ArgumentNullException(nameof(preflight));
            preflight.RequireSupported();
            var fallback = runtime as MNNRealtimeFallbackRuntime;
            if (fallback != null && (!ReferenceEquals(output, fallback.Output) ||
                !ReferenceEquals(preflight, fallback.CurrentReport)))
                throw new ArgumentException("Fallback must use its own shared output and current preflight report.");
            _session = new MNNRealtimeSession(runtime, output);
            _preflight = preflight;
            _fallbackRuntime = fallback;
            _lastReportedState = MNNRealtimeSessionState.Created;
            Volatile.Write(ref _lifecycleError, null);
        }

        /// <summary>Starts the configured session. The returned task can be awaited by application control code.</summary>
        public Task StartSessionAsync(CancellationToken cancellationToken = default)
        {
            EnsureMainThread();
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("The realtime session component must be active before starting.");
            return RequireSession().StartAsync(cancellationToken);
        }

        /// <summary>Closes the response gate immediately, then cancels, joins and releases the runtime asynchronously.</summary>
        public Task StopSessionAsync()
        {
            EnsureMainThread();
            var session = _session;
            if (session == null)
                return Task.CompletedTask;
            if (_stopTask != null)
            {
                if (!_stopTask.IsCompleted || session.State != MNNRealtimeSessionState.Faulted)
                    return _stopTask;
                _stopTask = null;
            }

            session.Output.Interrupt();
            _stopTask = StopCoreAsync(session);
            return _stopTask;
        }

        /// <summary>Interrupts the current response without stopping microphone input or the session.</summary>
        public Task InterruptAsync()
        {
            EnsureMainThread();
            return RequireSession().InterruptAsync();
        }

        private async Task StopCoreAsync(MNNRealtimeSession session)
        {
            await session.StopAsync().ConfigureAwait(false);
        }

        private void Update()
        {
            var session = _session;
            if (session == null)
                return;
            var state = session.State;
            var fault = session.Fault;
            if (state == _lastReportedState && ReferenceEquals(fault, _lastReportedFault))
                return;
            _lastReportedState = state;
            _lastReportedFault = fault;
            try
            {
                StateChanged?.Invoke(state, fault);
            }
            catch (Exception error)
            {
                Volatile.Write(ref _lifecycleError, error);
            }
        }

        private MNNRealtimeSession RequireSession()
        {
            return _session ?? throw new InvalidOperationException("Configure the realtime session component before use.");
        }

        private void OnDisable()
        {
            StopFromUnityLifecycle();
        }

        private void OnDestroy()
        {
            StopFromUnityLifecycle();
        }

        private void StopFromUnityLifecycle()
        {
            if (_mainThreadId == 0 || Thread.CurrentThread.ManagedThreadId != _mainThreadId || _session == null ||
                (_stopTask != null && !_stopTask.IsCompleted))
                return;
            try
            {
                _ = ObserveLifecycleStopAsync(StopSessionAsync());
            }
            catch (Exception error)
            {
                Volatile.Write(ref _lifecycleError, error);
            }
        }

        private async Task ObserveLifecycleStopAsync(Task stopTask)
        {
            try
            {
                await stopTask.ConfigureAwait(false);
            }
            catch (Exception error)
            {
                Volatile.Write(ref _lifecycleError, error);
            }
        }

        private void EnsureMainThread()
        {
            if (_mainThreadId == 0)
                _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
                throw new InvalidOperationException("Realtime session lifecycle methods must run on Unity's main thread.");
        }
    }
}
