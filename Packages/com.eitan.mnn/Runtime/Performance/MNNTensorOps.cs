using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MNN.Unity.Performance
{
    /// <summary>
    /// Burst优化的张量预处理作业
    /// </summary>
    [BurstCompile]
    public struct TensorNormalizeJob : IJobParallelFor
    {
        public NativeArray<float> data;
        public float mean;
        public float std;

        public void Execute(int index)
        {
            data[index] = (data[index] - mean) / std;
        }
    }

    /// <summary>
    /// Burst优化的Texture到NCHW转换
    /// </summary>
    [BurstCompile]
    public struct TextureToNCHWJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> pixels;
        [WriteOnly] public NativeArray<float> output;
        public int height;
        public int width;
        public int channels;
        public float normalizeFactor;

        public void Execute(int pixelIndex)
        {
            int y = pixelIndex / width;
            int x = pixelIndex % width;
            var pixel = pixels[pixelIndex];

            // NCHW: [C, H, W]
            int hwSize = height * width;
            int baseIdx = y * width + x;

            output[0 * hwSize + baseIdx] = pixel.r * normalizeFactor;
            output[1 * hwSize + baseIdx] = pixel.g * normalizeFactor;
            output[2 * hwSize + baseIdx] = pixel.b * normalizeFactor;

            if (channels == 4)
            {
                output[3 * hwSize + baseIdx] = pixel.a * normalizeFactor;
            }
        }
    }

    /// <summary>
    /// Burst优化的NCHW到Texture转换
    /// </summary>
    [BurstCompile]
    public struct NCHWToTextureJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> input;
        [WriteOnly] public NativeArray<Color32> pixels;
        public int height;
        public int width;
        public int channels;
        public float denormalizeFactor;

        public void Execute(int pixelIndex)
        {
            int y = pixelIndex / width;
            int x = pixelIndex % width;

            int hwSize = height * width;
            int baseIdx = y * width + x;

            byte r = (byte)math.clamp(input[0 * hwSize + baseIdx] * denormalizeFactor, 0, 255);
            byte g = (byte)math.clamp(input[1 * hwSize + baseIdx] * denormalizeFactor, 0, 255);
            byte b = (byte)math.clamp(input[2 * hwSize + baseIdx] * denormalizeFactor, 0, 255);
            byte a = 255;

            if (channels == 4)
            {
                a = (byte)math.clamp(input[3 * hwSize + baseIdx] * denormalizeFactor, 0, 255);
            }

            pixels[pixelIndex] = new Color32(r, g, b, a);
        }
    }

    /// <summary>
    /// Burst优化的Texture到NHWC转换
    /// </summary>
    [BurstCompile]
    public struct TextureToNHWCJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Color32> pixels;
        [WriteOnly] public NativeArray<float> output;
        public int channels;
        public float normalizeFactor;

        public void Execute(int pixelIndex)
        {
            var pixel = pixels[pixelIndex];
            int baseIdx = pixelIndex * channels;

            output[baseIdx + 0] = pixel.r * normalizeFactor;
            output[baseIdx + 1] = pixel.g * normalizeFactor;
            output[baseIdx + 2] = pixel.b * normalizeFactor;

            if (channels == 4)
            {
                output[baseIdx + 3] = pixel.a * normalizeFactor;
            }
        }
    }

    /// <summary>
    /// Burst优化的NHWC到Texture转换
    /// </summary>
    [BurstCompile]
    public struct NHWCToTextureJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> input;
        [WriteOnly] public NativeArray<Color32> pixels;
        public int channels;
        public float denormalizeFactor;

        public void Execute(int pixelIndex)
        {
            int baseIdx = pixelIndex * channels;

            byte r = (byte)math.clamp(input[baseIdx + 0] * denormalizeFactor, 0, 255);
            byte g = (byte)math.clamp(input[baseIdx + 1] * denormalizeFactor, 0, 255);
            byte b = (byte)math.clamp(input[baseIdx + 2] * denormalizeFactor, 0, 255);
            byte a = 255;

            if (channels == 4)
            {
                a = (byte)math.clamp(input[baseIdx + 3] * denormalizeFactor, 0, 255);
            }

            pixels[pixelIndex] = new Color32(r, g, b, a);
        }
    }

    /// <summary>
    /// Burst优化的张量操作工具类
    /// </summary>
    public static class MNNTensorOps
    {
        /// <summary>
        /// 批量归一化（Burst优化）
        /// </summary>
        public static JobHandle Normalize(NativeArray<float> data, float mean, float std, JobHandle dependency = default)
        {
            var job = new TensorNormalizeJob
            {
                data = data,
                mean = mean,
                std = std
            };

            return job.Schedule(data.Length, 64, dependency);
        }

        /// <summary>
        /// Texture转NCHW格式（Burst优化）
        /// </summary>
        public static JobHandle TextureToNCHW(
            Texture2D texture,
            NativeArray<float> output,
            bool normalize = true,
            JobHandle dependency = default)
        {
            var pixels = texture.GetPixelData<Color32>(0);
            int channels = 3; // 默认RGB

            var job = new TextureToNCHWJob
            {
                pixels = pixels,
                output = output,
                height = texture.height,
                width = texture.width,
                channels = channels,
                normalizeFactor = normalize ? 1.0f / 255.0f : 1.0f
            };

            return job.Schedule(pixels.Length, 64, dependency);
        }

        /// <summary>
        /// NCHW格式转Texture（Burst优化）
        /// </summary>
        public static JobHandle NCHWToTexture(
            NativeArray<float> input,
            Texture2D texture,
            bool denormalize = true,
            JobHandle dependency = default)
        {
            var pixels = texture.GetPixelData<Color32>(0);
            int channels = 3;

            var job = new NCHWToTextureJob
            {
                input = input,
                pixels = pixels,
                height = texture.height,
                width = texture.width,
                channels = channels,
                denormalizeFactor = denormalize ? 255.0f : 1.0f
            };

            return job.Schedule(pixels.Length, 64, dependency);
        }

        /// <summary>
        /// Texture转NHWC格式（Burst优化）
        /// </summary>
        public static JobHandle TextureToNHWC(
            Texture2D texture,
            NativeArray<float> output,
            bool normalize = true,
            JobHandle dependency = default)
        {
            var pixels = texture.GetPixelData<Color32>(0);
            int channels = 3;

            var job = new TextureToNHWCJob
            {
                pixels = pixels,
                output = output,
                channels = channels,
                normalizeFactor = normalize ? 1.0f / 255.0f : 1.0f
            };

            return job.Schedule(pixels.Length, 64, dependency);
        }

        /// <summary>
        /// NHWC格式转Texture（Burst优化）
        /// </summary>
        public static JobHandle NHWCToTexture(
            NativeArray<float> input,
            Texture2D texture,
            bool denormalize = true,
            JobHandle dependency = default)
        {
            var pixels = texture.GetPixelData<Color32>(0);
            int channels = 3;

            var job = new NHWCToTextureJob
            {
                input = input,
                pixels = pixels,
                channels = channels,
                denormalizeFactor = denormalize ? 255.0f : 1.0f
            };

            return job.Schedule(pixels.Length, 64, dependency);
        }
    }
}
