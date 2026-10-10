using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using MNN.Unity.Interop;
using MNN.Unity.Interop.Handles;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN模型解释器 - 模型加载和会话管理
    /// </summary>
    public sealed class MNNInterpreter : IDisposable
    {
        private InterpreterHandle _handle;
        private readonly List<MNNSession> _sessions = new List<MNNSession>();
        private bool _disposed;
        private bool _modelReleased;
        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => _disposed;
        /// <summary>
        /// 是否已释放模型数据
        /// </summary>
        public bool IsModelReleased => _modelReleased;
        private MNNInterpreter(InterpreterHandle handle)
        {
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            if (_handle.IsInvalid)
            {
                throw new MNNException(MNNErrorCode.InvalidValue, "Failed to create interpreter: invalid handle");
            }
        }

        /// <summary>
        /// 从文件加载模型
        /// </summary>
        /// <param name = "modelPath">模型文件路径</param>
        /// <returns>MNN解释器实例</returns>
        public static MNNInterpreter CreateFromFile(string modelPath)
        {
            if (string.IsNullOrEmpty(modelPath))
            {
                throw new ArgumentNullException(nameof(modelPath));
            }

            if (!File.Exists(modelPath))
            {
                throw new FileNotFoundException($"Model file not found: {modelPath}");
            }

            var handle = MNNInterop.MNN_Interpreter_createFromFile(modelPath);
            if (handle.IsInvalid)
            {
                throw new MNNException(MNNErrorCode.InvalidValue, $"Failed to create interpreter from file: {modelPath}");
            }

            Debug.Log($"[MNN] Loaded model from: {modelPath}");
            return new MNNInterpreter(handle);
        }

        /// <summary>
        /// 从字节数组加载模型
        /// </summary>
        /// <param name = "buffer">模型数据缓冲区</param>
        /// <returns>MNN解释器实例</returns>
        public static MNNInterpreter CreateFromBuffer(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            // 固定缓冲区内存，防止GC移动
            var gcHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            try
            {
                var ptr = gcHandle.AddrOfPinnedObject();
                var handle = MNNInterop.MNN_Interpreter_createFromBuffer(ptr, buffer.Length);
                if (handle.IsInvalid)
                {
                    throw new MNNException(MNNErrorCode.InvalidValue, "Failed to create interpreter from buffer");
                }

                Debug.Log($"[MNN] Loaded model from buffer ({buffer.Length} bytes)");
                return new MNNInterpreter(handle);
            }
            finally
            {
                gcHandle.Free();
            }
        }

        internal void SetExternalFile(string path)
        {
            ThrowIfDisposed();
            if (_sessions.Count != 0)
                throw new InvalidOperationException("Set external weights before creating a session.");
            MNNInterop.MNN_Interpreter_setExternalFile(_handle, path);
        }

        /// <summary>
        /// 创建推理会话
        /// </summary>
        /// <param name = "config">会话配置</param>
        /// <returns>MNN会话实例</returns>
        public MNNSession CreateSession(MNNSessionConfig config = null)
        {
            ThrowIfDisposed();
            if (_modelReleased)
                throw new InvalidOperationException("Cannot create a session after ReleaseModel.");
            config = (config ?? MNNSessionConfig.CreateForCurrentPlatform()).Clone();
            if (config.ThreadCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(config.ThreadCount));
            var requested = config.BackendType;
            config.BackendType = MNNAcceleration.Resolve(requested);
            config.BackupBackendType = MNNBackendType.CPU;
            IntPtr sessionPtr = CreateNativeSession(config);
            if (sessionPtr == IntPtr.Zero && config.BackendType != MNNBackendType.CPU)
            {
                config.BackendType = MNNBackendType.CPU;
                sessionPtr = CreateNativeSession(config);
            }

            if (sessionPtr == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.NoExecution, $"Cannot create MNN session; requested {requested}, including CPU fallback.");
            MNNBackendType actual;
            try
            {
                actual = MNNInterop.SessionBackend(_handle, sessionPtr);
            }
            catch
            {
                MNNInterop.MNN_Interpreter_releaseSession(_handle.DangerousGetHandle(), sessionPtr);
                throw;
            }

            config.BackendType = actual;
            Debug.Log($"[MNN] Created session: requested={requested}, actual={actual}, threads={config.ThreadCount}");
            var session = new MNNSession(this, _handle, sessionPtr, config, requested);
            _sessions.RemoveAll(existing => existing.IsDisposed);
            _sessions.Add(session);
            return session;
        }

        private IntPtr CreateNativeSession(MNNSessionConfig config)
        {
            var backend = new BackendConfig{memory = config.MemoryMode, power = config.PowerMode, precision = config.PrecisionMode};
            var schedule = new ScheduleConfig{type = config.BackendType, numThread = MNNAcceleration.NativeThreads(config), backupType = MNNBackendType.CPU};
            IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf<BackendConfig>());
            try
            {
                Marshal.StructureToPtr(backend, pointer, false);
                schedule.backendConfig = pointer;
                return MNNInterop.MNN_Interpreter_createSession(_handle, ref schedule);
            }
            finally
            {
                Marshal.FreeHGlobal(pointer);
            }
        }

        /// <summary>
        /// 释放模型数据（在创建所有会话后调用以节省内存）
        /// </summary>
        public void ReleaseModel()
        {
            ThrowIfDisposed();
            if (!_modelReleased)
            {
                MNNInterop.MNN_Interpreter_releaseModel(_handle);
                _modelReleased = true;
                Debug.Log("[MNN] Model data released");
            }
        }

        /// <summary>
        /// 设置GPU缓存文件（加速后续启动）
        /// </summary>
        /// <param name = "cacheFilePath">缓存文件路径</param>
        public void SetCacheFile(string cacheFilePath)
        {
            ThrowIfDisposed();
            if (string.IsNullOrEmpty(cacheFilePath))
            {
                throw new ArgumentNullException(nameof(cacheFilePath));
            }

            MNNInterop.MNN_Interpreter_setCacheFile(_handle, cacheFilePath);
            Debug.Log($"[MNN] Cache file set to: {cacheFilePath}");
        }

        /// <summary>
        /// 更新GPU缓存文件（在resizeSession后调用）
        /// </summary>
        internal void UpdateCacheFile(SessionHandle session)
        {
            ThrowIfDisposed();
            MNNInterop.MNN_Interpreter_updateCacheFile(_handle, session);
        }

        /// <summary>
        /// 运行会话
        /// </summary>
        internal int RunSession(SessionHandle session)
        {
            ThrowIfDisposed();
            return MNNInterop.MNN_Interpreter_runSession(_handle, session);
        }

        /// <summary>
        /// 获取输入张量
        /// </summary>
        internal IntPtr GetSessionInput(SessionHandle session, string name)
        {
            ThrowIfDisposed();
            return MNNInterop.MNN_Interpreter_getSessionInput(_handle, session, name);
        }

        /// <summary>
        /// 获取输出张量
        /// </summary>
        internal IntPtr GetSessionOutput(SessionHandle session, string name)
        {
            ThrowIfDisposed();
            return MNNInterop.MNN_Interpreter_getSessionOutput(_handle, session, name);
        }

        /// <summary>
        /// 调整张量大小
        /// </summary>
        internal void ResizeTensor(IntPtr tensor, int[] shape)
        {
            ThrowIfDisposed();
            var gcHandle = GCHandle.Alloc(shape, GCHandleType.Pinned);
            try
            {
                var ptr = gcHandle.AddrOfPinnedObject();
                MNNInterop.MNN_Interpreter_resizeTensor(_handle, tensor, ptr, shape.Length);
            }
            finally
            {
                gcHandle.Free();
            }
        }

        /// <summary>
        /// 调整会话大小（在ResizeTensor后调用）
        /// </summary>
        internal void ResizeSession(SessionHandle session)
        {
            ThrowIfDisposed();
            MNNInterop.MNN_Interpreter_resizeSession(_handle, session);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MNNInterpreter));
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            foreach (var session in _sessions)
                session.Dispose();
            _sessions.Clear();
            _handle?.Dispose();
            _handle = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~MNNInterpreter()
        {
            Dispose();
        }
    }
}
