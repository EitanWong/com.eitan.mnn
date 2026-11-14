using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    public sealed partial class MNNMiniMindO
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        private async Task<MNNMiniMindOResult> GenerateCooperatively(MNNMiniMindORequest request, MNNMiniMindOOptions options,
            Action<MNNMiniMindOUpdate> callback, CancellationToken cancellation)
        {
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("WebGL asynchronous inference requires the Unity main-thread synchronization context.");
            lock (_gate)
            {
                if (_disposed) throw new ObjectDisposedException(nameof(MNNMiniMindO));
                if (_generating) throw new InvalidOperationException("The WebGL MiniMind-o model already owns a generation.");
                cancellation.ThrowIfCancellationRequested();
                _generating = true;
            }
            MNNMiniMindOResult result = null;
            try
            {
                await Task.Yield();
                foreach (var update in GenerateSteps(request, options, cancellation, value => result = value, cooperative: true))
                {
                    if (update != null) callback?.Invoke(update);
                    await Task.Yield();
                }
                cancellation.ThrowIfCancellationRequested();
                return result;
            }
            catch
            {
                // Decoder sessions own recurrent state; discard it after interruption.
                _decode?.Dispose(); _decode = null;
                _mimi?.Dispose(); _mimi = null;
                throw;
            }
            finally
            {
                lock (_gate) _generating = false;
            }
        }
#endif
    }
}
