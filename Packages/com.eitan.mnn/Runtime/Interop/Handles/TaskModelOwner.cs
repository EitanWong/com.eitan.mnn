using System;
using System.Runtime.InteropServices;

namespace MNN.Unity.Interop.Handles
{
    // Serializes inference/disposal and owns only automatically-created caches.
    internal sealed class TaskModelOwner<T> : IDisposable where T : SafeHandle
    {
        private readonly object _gate = new object ();
        private T _handle;
        private readonly string _cache;
        private readonly MNNModelOptions _options;
        private readonly Func<string, string, T> _create;
        internal MNNBackendType Backend
        {
            get
            {
                lock (_gate)
                {
                    if (_handle == null)
                        throw new ObjectDisposedException(typeof(T).Name);
                    return _options.Backend;
                }
            }
        }

        private TaskModelOwner(T handle, MNNModelOptions options, Func<string, string, T> create)
        {
            _handle = handle;
            _cache = options.OwnedCache;
            _options = options;
            _create = create;
        }

        internal static TaskModelOwner<T> Load(string directory, int threads, string cache, MNNPrecisionMode precision, Func<string, string, T> create, MNNBackendType backendType = MNNBackendType.Auto, string cpuCompatibilityReason = null)
        {
            var options = MNNModelOptions.Prepare(directory, threads, false, cache, precision, backendType: backendType);
            if (cpuCompatibilityReason != null)
                options.UseCpuForCompatibility(cpuCompatibilityReason);
            try
            {
                return new TaskModelOwner<T>(options.Load(create), options, create);
            }
            catch
            {
                MNNModelOptions.RemoveOwnedCache(options.OwnedCache);
                throw;
            }
        }

        internal TResult Use<TResult>(Func<T, TResult> operation)
        {
            lock (_gate)
            {
                if (_handle == null)
                    throw new ObjectDisposedException(typeof(T).Name);
                try
                {
                    return operation(_handle);
                }
                catch (MNNException error)when (Backend != MNNBackendType.CPU)
                {
                    _handle.Dispose();
                    _handle = null;
                    _options.FallBackToCpu(error);
                    _handle = _options.Load(_create);
                    return operation(_handle);
                }
            }
        }

        public void Dispose()
        {
            lock (_gate)
            {
                _handle?.Dispose();
                _handle = null;
                MNNModelOptions.RemoveOwnedCache(_cache);
            }
        }
    }
}
