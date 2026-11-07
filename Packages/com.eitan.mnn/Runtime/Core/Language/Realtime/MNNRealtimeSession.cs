using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>
    /// Serializes session lifecycle and recovery while allowing output to be silenced synchronously on interruption.
    /// This coordinates implemented backends; it does not provide a device or model backend itself.
    /// Operation tasks are published before runtime dispatch. Runtime methods and cancellation callbacks never
    /// run under the session state lock or on the lifecycle caller's synchronous call stack.
    /// </summary>
    public sealed class MNNRealtimeSession
    {
        private readonly object _sync = new object();
        private readonly IMNNRealtimeSessionRuntime _runtime;
        private readonly MNNRealtimeOutputPipeline _output;
        private MNNRealtimeSessionState _state = MNNRealtimeSessionState.Created;
        private Task _startTask;
        private Task _interruptTask;
        private Task _stopTask;
        private Task<Exception> _runtimeShutdownTask;
        private CancellationTokenSource _startupCancellation;
        private bool _runtimeDisposed;
        private Exception _fault;
        private Exception _reportedRuntimeFault;
        private MNNResponseId _reportedFaultResponse;

        public MNNRealtimeSessionState State
        {
            get { lock (_sync) return _state; }
        }

        public Exception Fault
        {
            get { lock (_sync) return _fault; }
        }

        /// <summary>
        /// True only after the runtime has quiesced and disposal has succeeded. A retained Fault can
        /// coexist with released resources; failed shutdown keeps ownership here for StopAsync to retry.
        /// </summary>
        public bool ResourcesReleased
        {
            get { lock (_sync) return _runtimeDisposed; }
        }

        public MNNRealtimeOutputPipeline Output => _output;

        public MNNRealtimeSession(IMNNRealtimeSessionRuntime runtime, MNNRealtimeOutputPipeline output)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _output = output ?? throw new ArgumentNullException(nameof(output));
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            Task start;
            TaskCompletionSource<bool> launch;
            lock (_sync)
            {
                if (_state == MNNRealtimeSessionState.Running)
                    return Task.CompletedTask;
                if (_state == MNNRealtimeSessionState.Preparing)
                    return _startTask;
                if (_state != MNNRealtimeSessionState.Created)
                    throw new InvalidOperationException("A realtime session can only be started once.");
                _state = MNNRealtimeSessionState.Preparing;
                _startupCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                launch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _startTask = StartCoreAsync(_startupCancellation.Token, launch.Task);
                start = _startTask;
            }
            // Publish the task before runtime code can reenter us. The private latch schedules
            // the control transition outside _sync and the caller's synchronization context.
            // Model/device owners still dispatch their work; no audio or token work is queued here.
            launch.SetResult(true);
            return start;
        }

        /// <summary>Starts a new reply epoch. Only one live reply may own the shared output pipeline.</summary>
        public MNNResponseId BeginResponse()
        {
            lock (_sync)
            {
                if (_state != MNNRealtimeSessionState.Running)
                    throw new InvalidOperationException("The session must be running before a response can begin.");
                var current = _output.CurrentResponse;
                if (current.IsValid && _output.Accepts(current))
                {
                    if (!_output.CanBeginNextResponse(current))
                        throw new InvalidOperationException("The current response must finish and its speech must be presented before beginning another.");
                }
                return _output.BeginResponse();
            }
        }

        /// <summary>Signals model EOS after its producer has stopped; playback continues until presentation is acknowledged.</summary>
        public bool CompleteResponse(MNNResponseId response)
        {
            lock (_sync)
            {
                if (_state != MNNRealtimeSessionState.Running)
                    return false;
                return _output.CompleteResponse(response);
            }
        }

        /// <summary>
        /// Silences audio before returning to the caller, then serially cancels, joins, and restores the old response.
        /// Repeated calls during the same recovery share one operation.
        /// </summary>
        public Task InterruptAsync()
        {
            Task interrupted;
            TaskCompletionSource<bool> launch;
            lock (_sync)
            {
                if (_state == MNNRealtimeSessionState.Stopping || _state == MNNRealtimeSessionState.Stopped ||
                    _state == MNNRealtimeSessionState.Faulted)
                    return Task.CompletedTask;
                var response = _output.Interrupt();
                if (!response.IsValid)
                    return _interruptTask ?? Task.CompletedTask;
                if (_interruptTask != null && !_interruptTask.IsCompleted)
                    return _interruptTask;
                _state = MNNRealtimeSessionState.Recovering;
                launch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _interruptTask = RecoverCoreAsync(response, launch.Task);
                interrupted = _interruptTask;
            }
            launch.SetResult(true);
            return interrupted;
        }

        /// <summary>Silences immediately, then stops input and disposes resources only after startup/recovery has settled.</summary>
        public Task StopAsync() => BeginShutdown(null, default);

        /// <summary>
        /// Reports a fatal device/worker failure, silences synchronously and joins the runtime before disposal.
        /// The first reported failure is retained in Fault and the returned shutdown task. This is a control
        /// operation, not an audio callback API. A worker that shutdown must join must report without waiting
        /// and exit; an external owner observes the task. Awaiting it inside that worker deadlocks.
        /// </summary>
        public Task ReportFaultAsync(Exception error)
        {
            if (error == null)
                throw new ArgumentNullException(nameof(error));
            return BeginShutdown(error, default);
        }

        /// <summary>
        /// Reports a fatal response-producer failure only while that response is current. Errors from an
        /// interrupted, replaced or foreign response are ignored. Repeated reports of the fault that initiated
        /// shutdown share the shutdown task. Use the session-wide overload for device and recovery failures.
        /// </summary>
        public Task ReportFaultAsync(MNNResponseId response, Exception error)
        {
            if (!response.IsValid)
                throw new ArgumentException("A valid response identity is required.", nameof(response));
            if (error == null)
                throw new ArgumentNullException(nameof(error));
            return BeginShutdown(error, response);
        }

        private Task BeginShutdown(Exception reportedFault, MNNResponseId response)
        {
            Task stop;
            TaskCompletionSource<bool> launch;
            lock (_sync)
            {
                if (_state == MNNRealtimeSessionState.Stopped)
                    return Task.CompletedTask;
                if (response.IsValid && response != _output.CurrentResponse && response != _reportedFaultResponse)
                    return Task.CompletedTask;
                if (reportedFault != null && _reportedRuntimeFault == null && !_runtimeDisposed)
                {
                    _reportedRuntimeFault = reportedFault;
                    _reportedFaultResponse = response;
                    _fault = reportedFault;
                }
                if (_stopTask != null)
                {
                    if (!_stopTask.IsCompleted || _runtimeDisposed)
                        return _stopTask;
                    _stopTask = null;
                }
                _output.Interrupt();
                _state = MNNRealtimeSessionState.Stopping;
                launch = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _stopTask = StopCoreAsync(launch.Task, _startupCancellation);
                stop = _stopTask;
            }
            launch.SetResult(true);
            return stop;
        }

        private async Task StartCoreAsync(CancellationToken cancellationToken, Task launch)
        {
            await launch.ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await _runtime.PrepareAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    if (_state != MNNRealtimeSessionState.Preparing)
                        throw new OperationCanceledException("Session startup was superseded by shutdown.", cancellationToken);
                }
                await _runtime.StartListeningAsync(cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                lock (_sync)
                {
                    if (_state != MNNRealtimeSessionState.Preparing)
                        throw new OperationCanceledException("Session startup was superseded by shutdown.", cancellationToken);
                    _state = MNNRealtimeSessionState.Running;
                }
            }
            catch (Exception error)
            {
                _output.Interrupt();
                Exception cleanupFailure = null;
                bool stopping;
                lock (_sync)
                {
                    stopping = _state == MNNRealtimeSessionState.Stopping;
                }
                if (!stopping)
                    cleanupFailure = await ShutdownRuntimeAsync().ConfigureAwait(false);
                lock (_sync)
                {
                    if (!stopping && _state != MNNRealtimeSessionState.Stopping)
                    {
                        _fault = cleanupFailure == null ? error : new AggregateException(error, cleanupFailure);
                        _state = MNNRealtimeSessionState.Faulted;
                    }
                }
                throw;
            }
        }

        private async Task RecoverCoreAsync(MNNResponseId response, Task launch)
        {
            await launch.ConfigureAwait(false);
            try
            {
                await _runtime.RequestCancellationAsync(response).ConfigureAwait(false);
                await _runtime.WaitForInferenceQuiescenceAsync(response).ConfigureAwait(false);
                await _runtime.RestoreContextAsync(response).ConfigureAwait(false);
                lock (_sync)
                {
                    if (_state == MNNRealtimeSessionState.Recovering)
                        _state = MNNRealtimeSessionState.Running;
                }
            }
            catch (Exception error)
            {
                _output.Interrupt();
                lock (_sync)
                {
                    if (_state != MNNRealtimeSessionState.Stopping)
                    {
                        _fault = error;
                        _state = MNNRealtimeSessionState.Faulted;
                    }
                }
                throw;
            }
        }

        private async Task StopCoreAsync(Task launch, CancellationTokenSource startupCancellation)
        {
            await launch.ConfigureAwait(false);
            // Cancellation invokes user callbacks. Keep it off the caller and outside _sync;
            // do not dispose the source until these callbacks and startup have both returned.
            Exception cancellationFailure = null;
            try { startupCancellation?.Cancel(); }
            catch (Exception error) { cancellationFailure = error; }
            if (_startTask != null)
                await IgnoreFailureAsync(_startTask).ConfigureAwait(false);
            if (_interruptTask != null)
                await IgnoreFailureAsync(_interruptTask).ConfigureAwait(false);
            Exception shutdownFailure = await ShutdownRuntimeAsync().ConfigureAwait(false);
            Exception failure;
            lock (_sync)
            {
                failure = CombineFailures(_reportedRuntimeFault, cancellationFailure, shutdownFailure);
                if (failure != null)
                {
                    _fault = failure;
                    _state = MNNRealtimeSessionState.Faulted;
                }
                else
                {
                    _state = MNNRealtimeSessionState.Stopped;
                }
            }
            _startupCancellation?.Dispose();
            _startupCancellation = null;
            if (failure != null)
                throw new InvalidOperationException(shutdownFailure != null || cancellationFailure != null
                    ? "Realtime session shutdown failed; backend resources may remain live."
                    : "Realtime session stopped after a runtime fault.", failure);
        }

        private Task<Exception> ShutdownRuntimeAsync()
        {
            TaskCompletionSource<Exception> completion;
            lock (_sync)
            {
                if (_runtimeDisposed)
                    return Task.FromResult<Exception>(null);
                if (_runtimeShutdownTask != null)
                    return _runtimeShutdownTask;
                completion = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
                _runtimeShutdownTask = completion.Task;
            }
            _ = CompleteRuntimeShutdownAsync(completion);
            return completion.Task;
        }

        private async Task CompleteRuntimeShutdownAsync(TaskCompletionSource<Exception> completion)
        {
            Exception failure;
            try
            {
                failure = await ShutdownRuntimeCoreAsync().ConfigureAwait(false);
            }
            catch (Exception error)
            {
                failure = error;
            }
            lock (_sync)
            {
                if (_runtimeShutdownTask == completion.Task)
                {
                    if (_runtimeDisposed || failure == null)
                        _runtimeDisposed = true;
                    else
                        _runtimeShutdownTask = null;
                }
            }
            completion.TrySetResult(failure);
        }

        private async Task<Exception> ShutdownRuntimeCoreAsync()
        {
            Exception stopFailure = null;
            Exception quiescenceFailure = null;
            Exception disposeFailure = null;
            try { await _runtime.StopListeningAsync().ConfigureAwait(false); }
            catch (Exception error) { stopFailure = error; }
            try { await _runtime.WaitForQuiescenceAsync().ConfigureAwait(false); }
            catch (Exception error) { quiescenceFailure = error; }
            if (quiescenceFailure == null)
            {
                try
                {
                    await _runtime.DisposeAsync().ConfigureAwait(false);
                    lock (_sync)
                        _runtimeDisposed = true;
                }
                catch (Exception error) { disposeFailure = error; }
            }
            return CombineFailures(stopFailure, quiescenceFailure, disposeFailure);
        }

        private static Exception CombineFailures(Exception first, Exception second, Exception third)
        {
            if (first == null) first = second;
            else if (second != null) first = new AggregateException(first, second);
            if (first == null) first = third;
            else if (third != null) first = new AggregateException(first, third);
            return first;
        }

        private static async Task IgnoreFailureAsync(Task task)
        {
            try { await task.ConfigureAwait(false); }
            catch { }
        }
    }
}
