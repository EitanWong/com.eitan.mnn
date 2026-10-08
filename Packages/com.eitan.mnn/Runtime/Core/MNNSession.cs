using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MNN.Unity.Interop.Handles;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN推理会话 - 管理推理执行和张量访问
    /// </summary>
    public sealed class MNNSession : IDisposable
    {
        private readonly MNNInterpreter _interpreter;
        private readonly InterpreterHandle _interpreterHandle;
        private readonly SessionHandle _sessionHandle;
        private readonly MNNSessionConfig _config;
        private readonly Dictionary<string, MNNTensor> _inputTensors;
        private readonly Dictionary<string, MNNTensor> _outputTensors;
        private bool _disposed;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 会话配置
        /// </summary>
        public MNNSessionConfig Config => _config;

        internal MNNSession(
            MNNInterpreter interpreter,
            InterpreterHandle interpreterHandle,
            IntPtr sessionPtr,
            MNNSessionConfig config)
        {
            _interpreter = interpreter ?? throw new ArgumentNullException(nameof(interpreter));
            _interpreterHandle = interpreterHandle ?? throw new ArgumentNullException(nameof(interpreterHandle));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (sessionPtr == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid session pointer", nameof(sessionPtr));
            }

            // 直接创建SessionHandle，不使用SetHandle
            _sessionHandle = new SessionHandle(interpreterHandle, sessionPtr);

            _inputTensors = new Dictionary<string, MNNTensor>();
            _outputTensors = new Dictionary<string, MNNTensor>();
        }

        /// <summary>
        /// 获取输入张量
        /// </summary>
        /// <param name="name">张量名称（null表示第一个输入）</param>
        /// <returns>输入张量</returns>
        public MNNTensor GetInput(string name = null)
        {
            ThrowIfDisposed();

            // 缓存张量引用
            if (_inputTensors.TryGetValue(name ?? "", out var cachedTensor))
            {
                return cachedTensor;
            }

            var tensorPtr = _interpreter.GetSessionInput(_sessionHandle, name);

            if (tensorPtr == IntPtr.Zero)
            {
                throw new MNNException(MNNErrorCode.InvalidValue,
                    $"Failed to get input tensor: {name ?? "default"}");
            }

            var tensor = new MNNTensor(tensorPtr, isOwned: false);
            _inputTensors[name ?? ""] = tensor;
            return tensor;
        }

        /// <summary>
        /// 获取输出张量
        /// </summary>
        /// <param name="name">张量名称（null表示第一个输出）</param>
        /// <returns>输出张量</returns>
        public MNNTensor GetOutput(string name = null)
        {
            ThrowIfDisposed();

            // 缓存张量引用
            if (_outputTensors.TryGetValue(name ?? "", out var cachedTensor))
            {
                return cachedTensor;
            }

            var tensorPtr = _interpreter.GetSessionOutput(_sessionHandle, name);

            if (tensorPtr == IntPtr.Zero)
            {
                throw new MNNException(MNNErrorCode.InvalidValue,
                    $"Failed to get output tensor: {name ?? "default"}");
            }

            var tensor = new MNNTensor(tensorPtr, isOwned: false);
            _outputTensors[name ?? ""] = tensor;
            return tensor;
        }

        /// <summary>
        /// 运行推理（同步）
        /// </summary>
        public void Run()
        {
            ThrowIfDisposed();

            var errorCode = _interpreter.RunSession(_sessionHandle);
            MNNException.ThrowIfError(errorCode, "RunSession");
        }

        /// <summary>
        /// 运行推理（异步）
        /// </summary>
        /// <returns>异步任务</returns>
        public Task RunAsync()
        {
            ThrowIfDisposed();

            return Task.Run(() =>
            {
                var errorCode = _interpreter.RunSession(_sessionHandle);
                MNNException.ThrowIfError(errorCode, "RunSessionAsync");
            });
        }

        /// <summary>
        /// 调整输入张量大小
        /// </summary>
        /// <param name="name">张量名称</param>
        /// <param name="shape">新的形状</param>
        public void ResizeInput(string name, int[] shape)
        {
            ThrowIfDisposed();

            if (shape == null || shape.Length == 0)
            {
                throw new ArgumentNullException(nameof(shape));
            }

            var tensor = GetInput(name);
            _interpreter.ResizeTensor(tensor.Handle, shape);
            _interpreter.ResizeSession(_sessionHandle);

            // 清除缓存的张量，因为形状已改变
            _inputTensors.Clear();
            _outputTensors.Clear();

            Debug.Log($"[MNN] Resized input '{name}' to [{string.Join(", ", shape)}]");
        }

        /// <summary>
        /// 更新GPU缓存（在ResizeInput后调用）
        /// </summary>
        public void UpdateCache()
        {
            ThrowIfDisposed();
            _interpreter.UpdateCacheFile(_sessionHandle);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MNNSession));
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            // 清理缓存的张量
            foreach (var tensor in _inputTensors.Values)
            {
                tensor?.Dispose();
            }
            _inputTensors.Clear();

            foreach (var tensor in _outputTensors.Values)
            {
                tensor?.Dispose();
            }
            _outputTensors.Clear();

            // 释放会话句柄
            _sessionHandle?.Dispose();

            _disposed = true;

            Debug.Log("[MNN] Session disposed");
        }

        ~MNNSession()
        {
            Dispose();
        }
    }
}
