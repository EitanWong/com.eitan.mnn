using System;
using System.Threading;
using System.Threading.Tasks;

namespace MNN.Unity
{
    /// <summary>Loads managed MNN models away from Unity's render thread.</summary>
    public static class MNNModelLoading
    {
        /// <summary>The factory must use thread-safe model APIs and no Unity objects.
        /// Native loading cannot be interrupted; cancellation disposes the result
        /// before returning it. Exceptions from the factory propagate to the caller.</summary>
        public static Task<T> LoadAsync<T>(Func<T> factory, CancellationToken cancellationToken = default) where T : IDisposable
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
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
        }
    }
}
