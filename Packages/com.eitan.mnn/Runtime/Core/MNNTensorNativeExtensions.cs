using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>
    /// MNNTensor的NativeArray扩展 - 零拷贝高性能访问
    /// </summary>
    public static class MNNTensorNativeExtensions
    {
        /// <summary>
        /// 获取张量的NativeArray视图（零拷贝）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="tensor">源张量</param>
        /// <returns>NativeArray视图，不拥有内存</returns>
        public static unsafe NativeArray<T> AsNativeArray<T>(this MNNTensor tensor)
            where T : unmanaged
        {
            if (tensor == null)
                throw new ArgumentNullException(nameof(tensor));

            if (tensor.IsDisposed)
                throw new ObjectDisposedException(nameof(MNNTensor));

            // 使用Map获取内存指针
            var ptr = Interop.MNNInterop.MNN_Tensor_map(
                tensor.Handle,
                MNNTensorMapType.Write,
                tensor.DimensionType);

            if (ptr == IntPtr.Zero)
                throw new MNNException(MNNErrorCode.OutOfMemory, "Failed to map tensor");

            // 创建NativeArray视图（不分配新内存）
            var nativeArray = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<T>(
                ptr.ToPointer(),
                tensor.ElementCount,
                Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // 设置安全句柄
            var safety = AtomicSafetyHandle.Create();
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref nativeArray, safety);
#endif

            return nativeArray;
        }

        /// <summary>
        /// 从NativeArray拷贝数据到张量
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="tensor">目标张量</param>
        /// <param name="source">源NativeArray</param>
        public static void CopyFromNativeArray<T>(this MNNTensor tensor, NativeArray<T> source)
            where T : unmanaged
        {
            if (tensor == null)
                throw new ArgumentNullException(nameof(tensor));

            if (!source.IsCreated)
                throw new ArgumentException("Source NativeArray is not created", nameof(source));

            if (source.Length != tensor.ElementCount)
            {
                throw new ArgumentException(
                    $"Length mismatch: expected {tensor.ElementCount}, got {source.Length}",
                    nameof(source));
            }

            // 使用Span进行高效拷贝
            var span = tensor.MapForWrite<T>();
            source.AsReadOnlySpan().CopyTo(span);
            tensor.Unmap();
        }

        /// <summary>
        /// 从张量拷贝数据到NativeArray
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="tensor">源张量</param>
        /// <param name="destination">目标NativeArray</param>
        public static void CopyToNativeArray<T>(this MNNTensor tensor, NativeArray<T> destination)
            where T : unmanaged
        {
            if (tensor == null)
                throw new ArgumentNullException(nameof(tensor));

            if (!destination.IsCreated)
                throw new ArgumentException("Destination NativeArray is not created", nameof(destination));

            if (destination.Length != tensor.ElementCount)
            {
                throw new ArgumentException(
                    $"Length mismatch: expected {tensor.ElementCount}, got {destination.Length}",
                    nameof(destination));
            }

            // 使用Span进行高效拷贝
            var span = tensor.MapForRead<T>();
            span.CopyTo(destination.AsSpan());
            tensor.Unmap();
        }

        /// <summary>
        /// 创建张量的NativeArray拷贝（拥有内存）
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="tensor">源张量</param>
        /// <param name="allocator">分配器类型</param>
        /// <returns>新的NativeArray</returns>
        public static NativeArray<T> ToNativeArray<T>(this MNNTensor tensor, Allocator allocator = Allocator.Temp)
            where T : unmanaged
        {
            if (tensor == null)
                throw new ArgumentNullException(nameof(tensor));

            var result = new NativeArray<T>(tensor.ElementCount, allocator, NativeArrayOptions.UninitializedMemory);
            tensor.CopyToNativeArray(result);
            return result;
        }
    }
}
