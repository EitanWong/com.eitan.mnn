using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Loads managed MNN models on a worker, or after yielding one Unity
    /// frame on single-thread WebGL. WebGL native loading still blocks that frame.</summary>
    public static class MNNModelLoading
    {
        /// <summary>The factory must use thread-safe model APIs and no Unity objects.
        /// Native loading cannot be interrupted; cancellation disposes the result
        /// before returning it. Exceptions from the factory propagate to the caller.</summary>
        public static Task<T> LoadAsync<T>(Func<T> factory, CancellationToken cancellationToken = default) where T : IDisposable
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
#if UNITY_WEBGL && !UNITY_EDITOR
            return LoadOnUnityThread(factory, cancellationToken);
#else
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                T model = factory();
                if (cancellationToken.IsCancellationRequested)
                {
                    model?.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                return model;
            }, cancellationToken);
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        private static async Task<T> LoadOnUnityThread<T>(Func<T> factory, CancellationToken cancellation) where T : IDisposable
        {
            if (SynchronizationContext.Current == null)
                throw new InvalidOperationException("WebGL model loading requires the Unity main-thread synchronization context.");
            cancellation.ThrowIfCancellationRequested();
            await Task.Yield();
            cancellation.ThrowIfCancellationRequested();
            T model = factory();
            if (cancellation.IsCancellationRequested)
            {
                model?.Dispose();
                cancellation.ThrowIfCancellationRequested();
            }
            return model;
        }
#endif
    }
}
