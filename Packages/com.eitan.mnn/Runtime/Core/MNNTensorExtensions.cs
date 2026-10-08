using System;
using System.Runtime.CompilerServices;
using UnityEngine;

#if UNITY_COLLECTIONS
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
#endif

namespace MNN.Unity
{
    /// <summary>
    /// MNN张量扩展 - 自适应高性能实现
    /// 自动检测Unity.Collections，如可用则使用零拷贝NativeArray，否则使用优化的托管实现
    /// </summary>
    public static class MNNTensorExtensions
    {
        #region Unity.Collections集成（如果可用）

#if UNITY_COLLECTIONS
        /// <summary>
        /// 获取张量的NativeArray视图（零拷贝）
        /// 需要Unity.Collections包
        /// </summary>
        public static unsafe NativeArray<T> AsNativeArray<T>(this MNNTensor tensor)
            where T : unmanaged
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (tensor.IsDisposed) throw new ObjectDisposedException(nameof(MNNTensor));

            var ptr = Interop.MNNInterop.MNN_Tensor_map(
                tensor.Handle,
                MNNTensorMapType.Write,
                tensor.DimensionType);

            if (ptr == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.OutOfMemory, "Failed to map tensor");

            var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
                ptr.ToPointer(),
                tensor.ElementCount,
                Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var safety = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, safety);
#endif

            return nativeArray;
        }

        /// <summary>
        /// 从NativeArray拷贝到张量（高性能）
        /// </summary>
        public static void CopyFrom<T>(this MNNTensor tensor, NativeArray<T> source)
            where T : unmanaged
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (!source.IsCreated) throw new ArgumentException("NativeArray is not created", nameof(source));
            if (source.Length != tensor.ElementCount)
                throw new ArgumentException($"Length mismatch: {tensor.ElementCount} vs {source.Length}");

            var span = tensor.MapForWrite<T>();
            source.AsReadOnlySpan().CopyTo(span);
            tensor.Unmap();
        }

        /// <summary>
        /// 从张量拷贝到NativeArray（高性能）
        /// </summary>
        public static void CopyTo<T>(this MNNTensor tensor, NativeArray<T> destination)
            where T : unmanaged
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (!destination.IsCreated) throw new ArgumentException("NativeArray is not created", nameof(destination));
            if (destination.Length != tensor.ElementCount)
                throw new ArgumentException($"Length mismatch: {tensor.ElementCount} vs {destination.Length}");

            var span = tensor.MapForRead<T>();
            span.CopyTo(destination.AsSpan());
            tensor.Unmap();
        }
#endif

        #endregion

        #region 自适应Texture转换

        /// <summary>
        /// 从Texture2D拷贝数据（自动选择最优实现）
        /// </summary>
        public static void CopyFromTexture(this MNNTensor tensor, Texture2D texture, bool normalize = true)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (texture == null) throw new ArgumentNullException(nameof(texture));

            // 自动选择实现
#if UNITY_COLLECTIONS && UNITY_BURST
            // 使用Burst优化实现
            CopyFromTextureBurst(tensor, texture, normalize);
#else
            // 使用优化的托管实现
            CopyFromTextureManaged(tensor, texture, normalize);
#endif
        }

        /// <summary>
        /// 拷贝到Texture2D（自动选择最优实现）
        /// </summary>
        public static void CopyToTexture(this MNNTensor tensor, Texture2D texture, bool denormalize = true)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (texture == null) throw new ArgumentNullException(nameof(texture));

#if UNITY_COLLECTIONS && UNITY_BURST
            CopyToTextureBurst(tensor, texture, denormalize);
#else
            CopyToTextureManaged(tensor, texture, denormalize);
#endif
        }

        #endregion

        #region Burst优化实现（如果可用）

#if UNITY_COLLECTIONS && UNITY_BURST
        private static void CopyFromTextureBurst(MNNTensor tensor, Texture2D texture, bool normalize)
        {
            var pixels = texture.GetPixelData<Color32>(0);
            var tensorData = tensor.AsNativeArray<float>();
            var factor = normalize ? 1.0f / 255.0f : 1.0f;

            if (tensor.DimensionType == MNNDimensionType.Caffe)
            {
                // NCHW - 使用Burst Job
                var job = new Performance.TextureToNCHWJob
                {
                    pixels = pixels,
                    output = tensorData,
                    height = texture.height,
                    width = texture.width,
                    channels = 3,
                    normalizeFactor = factor
                };
                job.Schedule(pixels.Length, 64).Complete();
            }
            else
            {
                // NHWC - 使用Burst Job
                var job = new Performance.TextureToNHWCJob
                {
                    pixels = pixels,
                    output = tensorData,
                    channels = 3,
                    normalizeFactor = factor
                };
                job.Schedule(pixels.Length, 64).Complete();
            }

            tensor.Unmap();
        }

        private static void CopyToTextureBurst(MNNTensor tensor, Texture2D texture, bool denormalize)
        {
            var pixels = texture.GetPixelData<Color32>(0);
            var tensorData = tensor.AsNativeArray<float>();
            var factor = denormalize ? 255.0f : 1.0f;

            if (tensor.DimensionType == MNNDimensionType.Caffe)
            {
                var job = new Performance.NCHWToTextureJob
                {
                    input = tensorData,
                    pixels = pixels,
                    height = texture.height,
                    width = texture.width,
                    channels = 3,
                    denormalizeFactor = factor
                };
                job.Schedule(pixels.Length, 64).Complete();
            }
            else
            {
                var job = new Performance.NHWCToTextureJob
                {
                    input = tensorData,
                    pixels = pixels,
                    channels = 3,
                    denormalizeFactor = factor
                };
                job.Schedule(pixels.Length, 64).Complete();
            }

            texture.Apply();
            tensor.Unmap();
        }
#endif

        #endregion

        #region 优化的托管实现（无依赖降级）

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyFromTextureManaged(MNNTensor tensor, Texture2D texture, bool normalize)
        {
            var pixels = texture.GetPixels32();
            var span = tensor.MapForWrite<float>();
            var factor = normalize ? 1.0f / 255.0f : 1.0f;
            var channels = 3;

            if (tensor.DimensionType == MNNDimensionType.Caffe)
            {
                // NCHW格式 - 优化的循环
                int h = texture.height;
                int w = texture.width;
                int hwSize = h * w;

                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    int y = i / w;
                    int x = i % w;
                    int idx = y * w + x;

                    span[0 * hwSize + idx] = pixel.r * factor;
                    span[1 * hwSize + idx] = pixel.g * factor;
                    span[2 * hwSize + idx] = pixel.b * factor;
                }
            }
            else
            {
                // NHWC格式 - 顺序访问优化
                for (int i = 0; i < pixels.Length; i++)
                {
                    var pixel = pixels[i];
                    int baseIdx = i * channels;

                    span[baseIdx + 0] = pixel.r * factor;
                    span[baseIdx + 1] = pixel.g * factor;
                    span[baseIdx + 2] = pixel.b * factor;
                }
            }

            tensor.Unmap();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CopyToTextureManaged(MNNTensor tensor, Texture2D texture, bool denormalize)
        {
            var span = tensor.MapForRead<float>();
            var pixels = new Color32[texture.width * texture.height];
            var factor = denormalize ? 255.0f : 1.0f;
            var channels = 3;

            if (tensor.DimensionType == MNNDimensionType.Caffe)
            {
                // NCHW格式
                int h = texture.height;
                int w = texture.width;
                int hwSize = h * w;

                for (int i = 0; i < pixels.Length; i++)
                {
                    int y = i / w;
                    int x = i % w;
                    int idx = y * w + x;

                    byte r = ClampToByte(span[0 * hwSize + idx] * factor);
                    byte g = ClampToByte(span[1 * hwSize + idx] * factor);
                    byte b = ClampToByte(span[2 * hwSize + idx] * factor);

                    pixels[i] = new Color32(r, g, b, 255);
                }
            }
            else
            {
                // NHWC格式
                for (int i = 0; i < pixels.Length; i++)
                {
                    int baseIdx = i * channels;

                    byte r = ClampToByte(span[baseIdx + 0] * factor);
                    byte g = ClampToByte(span[baseIdx + 1] * factor);
                    byte b = ClampToByte(span[baseIdx + 2] * factor);

                    pixels[i] = new Color32(r, g, b, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            tensor.Unmap();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ClampToByte(float value)
        {
            if (value <= 0) return 0;
            if (value >= 255) return 255;
            return (byte)value;
        }

        #endregion

        #region 归一化操作（自适应）

        /// <summary>
        /// 批量归一化（自动选择最优实现）
        /// </summary>
        public static void Normalize(this MNNTensor tensor, float mean, float std)
        {
            if (tensor == null) throw new ArgumentNullException(nameof(tensor));
            if (std == 0) throw new ArgumentException("Standard deviation cannot be zero", nameof(std));

#if UNITY_COLLECTIONS && UNITY_BURST
            // Burst优化版本
            var data = tensor.AsNativeArray<float>();
            var job = new Performance.TensorNormalizeJob
            {
                data = data,
                mean = mean,
                std = std
            };
            job.Schedule(data.Length, 64).Complete();
            tensor.Unmap();
#else
            // 优化的托管版本
            var span = tensor.MapForWrite<float>();
            var invStd = 1.0f / std;

            // 循环展开优化
            int i = 0;
            int len = span.Length;
            int remainder = len % 4;
            int end = len - remainder;

            for (; i < end; i += 4)
            {
                span[i + 0] = (span[i + 0] - mean) * invStd;
                span[i + 1] = (span[i + 1] - mean) * invStd;
                span[i + 2] = (span[i + 2] - mean) * invStd;
                span[i + 3] = (span[i + 3] - mean) * invStd;
            }

            for (; i < len; i++)
            {
                span[i] = (span[i] - mean) * invStd;
            }

            tensor.Unmap();
#endif
        }

        #endregion
    }
}
