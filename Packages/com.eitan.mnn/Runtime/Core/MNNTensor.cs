using System;
using System.Runtime.InteropServices;
using MNN.Unity.Interop;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNN张量 - 多维数据容器
    /// </summary>
    public sealed class MNNTensor : IDisposable
    {
        private IntPtr _handle;
        private readonly bool _isOwned;
        private bool _disposed;
        private IntPtr _mappedPtr;
        private MNNTensorMapType _mappedType;

        /// <summary>
        /// 是否已释放
        /// </summary>
        public bool IsDisposed => _disposed;

        /// <summary>
        /// 张量句柄
        /// </summary>
        internal IntPtr Handle => _handle;

        /// <summary>
        /// 张量形状
        /// </summary>
        public int[] Shape { get; private set; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public MNNDataType DataType { get; private set; }

        /// <summary>
        /// 维度类型
        /// </summary>
        public MNNDimensionType DimensionType { get; private set; }

        /// <summary>
        /// 元素数量
        /// </summary>
        public int ElementCount { get; private set; }

        /// <summary>
        /// 字节大小
        /// </summary>
        public int ByteSize { get; private set; }

        /// <summary>
        /// 维度数量
        /// </summary>
        public int Dimensions => Shape.Length;

        internal MNNTensor(IntPtr handle, bool isOwned)
        {
            if (handle == IntPtr.Zero)
            {
                throw new ArgumentException("Invalid tensor handle", nameof(handle));
            }

            _handle = handle;
            _isOwned = isOwned;
            RefreshMetadata();
        }

        /// <summary>
        /// 刷新张量元数据
        /// </summary>
        private void RefreshMetadata()
        {
            // 获取维度类型
            var dimType = MNNInterop.MNN_Tensor_getDimensionType(_handle);
            DimensionType = (MNNDimensionType)dimType;

            // 获取数据类型
            var dataType = MNNInterop.MNN_Tensor_getDataType(_handle);
            DataType = (MNNDataType)dataType;

            // 获取形状
            var dims = MNNInterop.MNN_Tensor_getDimensions(_handle);
            Shape = new int[dims];
            MNNInterop.MNN_Tensor_getShape(_handle, Shape, dims);

            // 获取元素数量和字节大小
            ElementCount = MNNInterop.MNN_Tensor_elementSize(_handle);
            ByteSize = MNNInterop.MNN_Tensor_size(_handle);
        }

        #region 零拷贝数据访问 (推荐)

        /// <summary>
        /// 映射张量内存用于写入（零拷贝）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns>数据的Span视图</returns>
        public unsafe Span<T> MapForWrite<T>() where T : unmanaged
        {
            ThrowIfDisposed();
            ThrowIfAlreadyMapped();

            ValidateDataType<T>();

            _mappedPtr = MNNInterop.MNN_Tensor_map(_handle, MNNTensorMapType.Write, DimensionType);
            _mappedType = MNNTensorMapType.Write;

            if (_mappedPtr == IntPtr.Zero)
            {
                throw new MNNException(MNNErrorCode.OutOfMemory, "Failed to map tensor for write");
            }

            return new Span<T>(_mappedPtr.ToPointer(), ElementCount);
        }

        /// <summary>
        /// 映射张量内存用于读取（零拷贝）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns>数据的只读Span视图</returns>
        public unsafe ReadOnlySpan<T> MapForRead<T>() where T : unmanaged
        {
            ThrowIfDisposed();
            ThrowIfAlreadyMapped();

            ValidateDataType<T>();

            _mappedPtr = MNNInterop.MNN_Tensor_map(_handle, MNNTensorMapType.Read, DimensionType);
            _mappedType = MNNTensorMapType.Read;

            if (_mappedPtr == IntPtr.Zero)
            {
                throw new MNNException(MNNErrorCode.OutOfMemory, "Failed to map tensor for read");
            }

            return new ReadOnlySpan<T>(_mappedPtr.ToPointer(), ElementCount);
        }

        /// <summary>
        /// 取消映射张量内存
        /// </summary>
        public void Unmap()
        {
            if (_mappedPtr == IntPtr.Zero)
            {
                return; // 没有映射，直接返回
            }

            MNNInterop.MNN_Tensor_unmap(_handle, _mappedType, DimensionType, _mappedPtr);
            _mappedPtr = IntPtr.Zero;
        }

        #endregion

        #region 数据拷贝访问 (安全但较慢)

        /// <summary>
        /// 拷贝数据到数组
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <returns>数据数组</returns>
        public T[] CopyToArray<T>() where T : unmanaged
        {
            ThrowIfDisposed();
            ValidateDataType<T>();

            var result = new T[ElementCount];
            var span = MapForRead<T>();
            try
            {
                span.CopyTo(result);
            }
            finally
            {
                Unmap();
            }

            return result;
        }

        /// <summary>
        /// 从数组拷贝数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">源数据数组</param>
        public void CopyFromArray<T>(T[] data) where T : unmanaged
        {
            ThrowIfDisposed();

            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (data.Length != ElementCount)
            {
                throw new ArgumentException(
                    $"Data length mismatch: expected {ElementCount}, got {data.Length}",
                    nameof(data));
            }

            ValidateDataType<T>();

            var span = MapForWrite<T>();
            try
            {
                data.CopyTo(span);
            }
            finally
            {
                Unmap();
            }
        }

        /// <summary>
        /// 从Span拷贝数据
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="data">源数据Span</param>
        public void CopyFromSpan<T>(ReadOnlySpan<T> data) where T : unmanaged
        {
            ThrowIfDisposed();

            if (data.Length != ElementCount)
            {
                throw new ArgumentException(
                    $"Data length mismatch: expected {ElementCount}, got {data.Length}",
                    nameof(data));
            }

            ValidateDataType<T>();

            var span = MapForWrite<T>();
            try
            {
                data.CopyTo(span);
            }
            finally
            {
                Unmap();
            }
        }

        #endregion

        #region Unity集成

        /// <summary>
        /// 从Texture2D拷贝数据（RGB/RGBA）
        /// </summary>
        /// <param name="texture">源纹理</param>
        /// <param name="normalize">是否归一化到[0,1]范围</param>
        public void CopyFromTexture(Texture2D texture, bool normalize = true)
        {
            ThrowIfDisposed();

            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            // 检查张量形状是否匹配
            if (Shape.Length != 4)
            {
                throw new InvalidOperationException(
                    "Tensor must be 4D (NCHW or NHWC) to copy from texture");
            }

            var pixels = texture.GetPixels32();
            var channels = DimensionType == MNNDimensionType.Caffe ? Shape[1] : Shape[3];

            if (channels != 3 && channels != 4)
            {
                throw new InvalidOperationException(
                    $"Tensor must have 3 or 4 channels, got {channels}");
            }

            // 转换为float数组
            var floatData = new float[ElementCount];
            var factor = normalize ? 1.0f / 255.0f : 1.0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                int baseIdx = i * channels;

                if (DimensionType == MNNDimensionType.Caffe) // NCHW
                {
                    // 需要重新排列为CHW格式
                    int h = texture.height;
                    int w = texture.width;
                    int y = i / w;
                    int x = i % w;

                    floatData[0 * h * w + y * w + x] = pixel.r * factor;
                    floatData[1 * h * w + y * w + x] = pixel.g * factor;
                    floatData[2 * h * w + y * w + x] = pixel.b * factor;

                    if (channels == 4)
                    {
                        floatData[3 * h * w + y * w + x] = pixel.a * factor;
                    }
                }
                else // NHWC
                {
                    floatData[baseIdx + 0] = pixel.r * factor;
                    floatData[baseIdx + 1] = pixel.g * factor;
                    floatData[baseIdx + 2] = pixel.b * factor;

                    if (channels == 4)
                    {
                        floatData[baseIdx + 3] = pixel.a * factor;
                    }
                }
            }

            CopyFromArray(floatData);
        }

        /// <summary>
        /// 拷贝数据到Texture2D
        /// </summary>
        /// <param name="texture">目标纹理</param>
        /// <param name="denormalize">是否从[0,1]范围反归一化</param>
        public void CopyToTexture(Texture2D texture, bool denormalize = true)
        {
            ThrowIfDisposed();

            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (Shape.Length != 4)
            {
                throw new InvalidOperationException(
                    "Tensor must be 4D (NCHW or NHWC) to copy to texture");
            }

            var floatData = CopyToArray<float>();
            var pixels = new Color32[texture.width * texture.height];
            var channels = DimensionType == MNNDimensionType.Caffe ? Shape[1] : Shape[3];
            var factor = denormalize ? 255.0f : 1.0f;

            for (int i = 0; i < pixels.Length; i++)
            {
                byte r, g, b, a = 255;

                if (DimensionType == MNNDimensionType.Caffe) // NCHW
                {
                    int h = texture.height;
                    int w = texture.width;
                    int y = i / w;
                    int x = i % w;

                    r = (byte)Mathf.Clamp(floatData[0 * h * w + y * w + x] * factor, 0, 255);
                    g = (byte)Mathf.Clamp(floatData[1 * h * w + y * w + x] * factor, 0, 255);
                    b = (byte)Mathf.Clamp(floatData[2 * h * w + y * w + x] * factor, 0, 255);

                    if (channels == 4)
                    {
                        a = (byte)Mathf.Clamp(floatData[3 * h * w + y * w + x] * factor, 0, 255);
                    }
                }
                else // NHWC
                {
                    int baseIdx = i * channels;
                    r = (byte)Mathf.Clamp(floatData[baseIdx + 0] * factor, 0, 255);
                    g = (byte)Mathf.Clamp(floatData[baseIdx + 1] * factor, 0, 255);
                    b = (byte)Mathf.Clamp(floatData[baseIdx + 2] * factor, 0, 255);

                    if (channels == 4)
                    {
                        a = (byte)Mathf.Clamp(floatData[baseIdx + 3] * factor, 0, 255);
                    }
                }

                pixels[i] = new Color32(r, g, b, a);
            }

            texture.SetPixels32(pixels);
            texture.Apply();
        }

        #endregion

        #region 辅助方法

        private void ValidateDataType<T>() where T : unmanaged
        {
            var expectedType = GetMNNDataType<T>();
            if (DataType != expectedType)
            {
                throw new InvalidOperationException(
                    $"Type mismatch: tensor is {DataType}, requested {expectedType}");
            }
        }

        private static MNNDataType GetMNNDataType<T>() where T : unmanaged
        {
            if (typeof(T) == typeof(float)) return MNNDataType.Float;
            if (typeof(T) == typeof(double)) return MNNDataType.Double;
            if (typeof(T) == typeof(int)) return MNNDataType.Int32;
            if (typeof(T) == typeof(long)) return MNNDataType.Int64;
            if (typeof(T) == typeof(byte)) return MNNDataType.UInt8;
            if (typeof(T) == typeof(sbyte)) return MNNDataType.Int8;

            throw new NotSupportedException($"Type {typeof(T).Name} is not supported");
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MNNTensor));
            }
        }

        private void ThrowIfAlreadyMapped()
        {
            if (_mappedPtr != IntPtr.Zero)
            {
                throw new InvalidOperationException(
                    "Tensor is already mapped. Call Unmap() first.");
            }
        }

        #endregion

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            // 取消映射
            if (_mappedPtr != IntPtr.Zero)
            {
                Unmap();
            }

            // 如果拥有句柄，则销毁
            if (_isOwned && _handle != IntPtr.Zero)
            {
                MNNInterop.MNN_Tensor_destroy(_handle);
            }

            _handle = IntPtr.Zero;
            _disposed = true;
        }

        ~MNNTensor()
        {
            Dispose();
        }

        /// <summary>
        /// 获取张量信息字符串
        /// </summary>
        public override string ToString()
        {
            return $"MNNTensor {{ Shape: [{string.Join(", ", Shape)}], " +
                   $"Type: {DataType}, DimType: {DimensionType}, Elements: {ElementCount} }}";
        }
    }
}
