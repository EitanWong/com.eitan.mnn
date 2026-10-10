// 此文件仅在Unity.Burst和Unity.Collections可用时编译
#if UNITY_COLLECTIONS && UNITY_BURST

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MNN.Unity.Performance
{
    // 此命名空间仅在Unity.Burst和Unity.Collections可用时编译
    // 由MNNTensorExtensions通过条件编译自动使用

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

    [BurstCompile]
    public struct TextureToNCHWJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<UnityEngine.Color32> pixels;
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

    [BurstCompile]
    public struct NCHWToTextureJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> input;
        [WriteOnly] public NativeArray<UnityEngine.Color32> pixels;
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

            pixels[pixelIndex] = new UnityEngine.Color32(r, g, b, a);
        }
    }

    [BurstCompile]
    public struct TextureToNHWCJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<UnityEngine.Color32> pixels;
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

    [BurstCompile]
    public struct NHWCToTextureJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> input;
        [WriteOnly] public NativeArray<UnityEngine.Color32> pixels;
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

            pixels[pixelIndex] = new UnityEngine.Color32(r, g, b, a);
        }
    }
}

#endif // UNITY_COLLECTIONS && UNITY_BURST
