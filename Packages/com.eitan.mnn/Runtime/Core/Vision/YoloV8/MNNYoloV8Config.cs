using System;
using System.IO;
using UnityEngine;

namespace MNN.Unity
{
    public enum MNNYoloV8BoxEncoding
    {
        CenterSize,
        AnchorDistances
    }

    /// <summary>YOLOv8 box/class graph, RGB float32 NCHW input and no objectness channel.</summary>
    [Serializable]
    public sealed class MNNYoloV8Config
    {
        public const string FileName = "mnn_yolov8.json";
        public string graph = "yolov8n.mnn";
        public string input = "images";
        public string output = "output0";
        public int width = 640;
        public int height = 640;
        public string[] labels;
        public MNNYoloV8BoxEncoding boxEncoding = MNNYoloV8BoxEncoding.CenterSize;
        public int[] strides = {8, 16, 32};

        public static MNNYoloV8Config Read(string directory)
        {
            return JsonUtility.FromJson<MNNYoloV8Config>(File.ReadAllText(Path.Combine(directory, FileName)))
                ?? throw new InvalidDataException("Invalid YOLOv8 profile.");
        }

        internal MNNYoloV8Config Clone() => JsonUtility.FromJson<MNNYoloV8Config>(JsonUtility.ToJson(this));

        public string ResolveGraph(string directory)
        {
            if (width < 32 || height < 32 || width > 4096 || height > 4096 || width % 32 != 0 || height % 32 != 0 ||
                string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(output) || labels == null || labels.Length < 1 || labels.Length > 10000 ||
                string.IsNullOrWhiteSpace(graph) || Path.IsPathRooted(graph))
                throw new InvalidDataException("Invalid YOLOv8 input dimensions, labels, tensor names or relative graph path.");
            if (!Enum.IsDefined(typeof(MNNYoloV8BoxEncoding), boxEncoding))
                throw new InvalidDataException("Unknown YOLOv8 box encoding.");
            if (boxEncoding == MNNYoloV8BoxEncoding.AnchorDistances)
            {
                if (strides == null || strides.Length < 1 || strides.Length > 8)
                    throw new InvalidDataException("Anchor-distance outputs require ordered feature strides.");
                int previous = 0;
                foreach (int stride in strides)
                {
                    if (stride <= previous || width % stride != 0 || height % stride != 0)
                        throw new InvalidDataException("YOLOv8 strides must increase and divide the input dimensions.");
                    previous = stride;
                }
            }
            foreach (string label in labels)
                if (string.IsNullOrWhiteSpace(label))
                    throw new InvalidDataException("YOLOv8 labels must be nonempty.");
            string root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string path = Path.GetFullPath(Path.Combine(root, graph));
            if (!path.StartsWith(root, StringComparison.Ordinal) || !File.Exists(path))
                throw new FileNotFoundException("Missing YOLOv8 graph within the model directory: " + graph);
            return path;
        }
    }
}
