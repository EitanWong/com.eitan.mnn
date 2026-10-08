using System;
using System.Collections.Generic;
using UnityEngine;

namespace MNN.Unity.Editor
{
    /// <summary>
    /// MNN模型元数据
    /// </summary>
    [Serializable]
    public class MNNModelInfo
    {
        public string id;
        public string name;
        public string displayName;
        public string category;
        public string description;
        public string author;
        public string version;
        public string downloadUrl;
        public long fileSize;
        public string[] tags;
        public string previewImage;
        public ModelInputSpec inputSpec;
        public ModelOutputSpec outputSpec;
        public bool isInstalled;
        public string localPath;
    }

    /// <summary>
    /// 模型输入规格
    /// </summary>
    [Serializable]
    public class ModelInputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public string format; // NCHW or NHWC
    }

    /// <summary>
    /// 模型输出规格
    /// </summary>
    [Serializable]
    public class ModelOutputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public int classCount;
    }

    /// <summary>
    /// MNN模型类别
    /// </summary>
    public enum MNNModelCategory
    {
        All,
        ImageClassification,
        ObjectDetection,
        SemanticSegmentation,
        PoseEstimation,
        FaceDetection,
        FaceRecognition,
        OCR,
        NLP,
        AudioProcessing,
        SuperResolution,
        StyleTransfer,
        Other
    }

    /// <summary>
    /// MNN模型仓库配置
    /// </summary>
    [Serializable]
    public class MNNModelRepository
    {
        public List<MNNModelInfo> models = new List<MNNModelInfo>();

        /// <summary>
        /// 获取默认ModelScope模型列表
        /// </summary>
        public static MNNModelRepository GetDefaultRepository()
        {
            var repo = new MNNModelRepository();

            // 图像分类模型
            repo.models.Add(new MNNModelInfo
            {
                id = "mnn-mobilenet-v2",
                name = "MobileNetV2",
                displayName = "MobileNet V2 (ImageNet)",
                category = "ImageClassification",
                description = "轻量级图像分类模型，适合移动端部署",
                author = "MNN Team",
                version = "1.0",
                downloadUrl = "https://www.modelscope.cn/api/v1/models/MNN/MobileNetV2/repo?Revision=master&FilePath=mobilenetv2.mnn",
                fileSize = 14 * 1024 * 1024, // 14MB
                tags = new[] { "classification", "imagenet", "mobilenet" },
                inputSpec = new ModelInputSpec
                {
                    name = "input",
                    shape = new[] { 1, 3, 224, 224 },
                    dataType = "float32",
                    format = "NCHW"
                },
                outputSpec = new ModelOutputSpec
                {
                    name = "output",
                    shape = new[] { 1, 1000 },
                    dataType = "float32",
                    classCount = 1000
                }
            });

            // 目标检测模型
            repo.models.Add(new MNNModelInfo
            {
                id = "mnn-yolov5",
                name = "YOLOv5s",
                displayName = "YOLOv5s (COCO)",
                category = "ObjectDetection",
                description = "实时目标检测模型，支持80类物体检测",
                author = "MNN Team",
                version = "1.0",
                downloadUrl = "https://www.modelscope.cn/api/v1/models/MNN/YOLOv5/repo?Revision=master&FilePath=yolov5s.mnn",
                fileSize = 28 * 1024 * 1024,
                tags = new[] { "detection", "yolo", "coco" },
                inputSpec = new ModelInputSpec
                {
                    name = "input",
                    shape = new[] { 1, 3, 640, 640 },
                    dataType = "float32",
                    format = "NCHW"
                },
                outputSpec = new ModelOutputSpec
                {
                    name = "output",
                    shape = new[] { 1, 25200, 85 },
                    dataType = "float32",
                    classCount = 80
                }
            });

            // 人脸检测模型
            repo.models.Add(new MNNModelInfo
            {
                id = "mnn-retinaface",
                name = "RetinaFace",
                displayName = "RetinaFace (人脸检测)",
                category = "FaceDetection",
                description = "高精度人脸检测模型，支持关键点检测",
                author = "MNN Team",
                version = "1.0",
                downloadUrl = "https://www.modelscope.cn/api/v1/models/MNN/RetinaFace/repo?Revision=master&FilePath=retinaface.mnn",
                fileSize = 5 * 1024 * 1024,
                tags = new[] { "face", "detection", "landmarks" },
                inputSpec = new ModelInputSpec
                {
                    name = "input",
                    shape = new[] { 1, 3, 640, 640 },
                    dataType = "float32",
                    format = "NCHW"
                }
            });

            // 语义分割模型
            repo.models.Add(new MNNModelInfo
            {
                id = "mnn-deeplabv3",
                name = "DeepLabV3",
                displayName = "DeepLabV3+ (语义分割)",
                category = "SemanticSegmentation",
                description = "语义分割模型，支持21类物体分割",
                author = "MNN Team",
                version = "1.0",
                downloadUrl = "https://www.modelscope.cn/api/v1/models/MNN/DeepLabV3/repo?Revision=master&FilePath=deeplabv3.mnn",
                fileSize = 17 * 1024 * 1024,
                tags = new[] { "segmentation", "deeplab" },
                inputSpec = new ModelInputSpec
                {
                    name = "input",
                    shape = new[] { 1, 3, 512, 512 },
                    dataType = "float32",
                    format = "NCHW"
                }
            });

            // 姿态估计模型
            repo.models.Add(new MNNModelInfo
            {
                id = "mnn-movenet",
                name = "MoveNet",
                displayName = "MoveNet (姿态估计)",
                category = "PoseEstimation",
                description = "轻量级人体姿态估计模型",
                author = "MNN Team",
                version = "1.0",
                downloadUrl = "https://www.modelscope.cn/api/v1/models/MNN/MoveNet/repo?Revision=master&FilePath=movenet.mnn",
                fileSize = 9 * 1024 * 1024,
                tags = new[] { "pose", "skeleton", "keypoints" },
                inputSpec = new ModelInputSpec
                {
                    name = "input",
                    shape = new[] { 1, 3, 192, 192 },
                    dataType = "float32",
                    format = "NCHW"
                }
            });

            // 超分辨率模型
            repo.models.Add(new MNNModelInfo
            {
                id = "mnn-esrgan",
                name = "ESRGAN",
                displayName = "ESRGAN (超分辨率)",
                category = "SuperResolution",
                description = "图像超分辨率模型，2x放大",
                author = "MNN Team",
                version = "1.0",
                downloadUrl = "https://www.modelscope.cn/api/v1/models/MNN/ESRGAN/repo?Revision=master&FilePath=esrgan.mnn",
                fileSize = 65 * 1024 * 1024,
                tags = new[] { "super-resolution", "upscale" },
                inputSpec = new ModelInputSpec
                {
                    name = "input",
                    shape = new[] { 1, 3, 256, 256 },
                    dataType = "float32",
                    format = "NCHW"
                }
            });

            return repo;
        }

        /// <summary>
        /// 按类别过滤模型
        /// </summary>
        public List<MNNModelInfo> GetModelsByCategory(MNNModelCategory category)
        {
            if (category == MNNModelCategory.All)
                return models;

            return models.FindAll(m => m.category == category.ToString());
        }

        /// <summary>
        /// 搜索模型
        /// </summary>
        public List<MNNModelInfo> SearchModels(string query)
        {
            if (string.IsNullOrEmpty(query))
                return models;

            query = query.ToLower();
            return models.FindAll(m =>
                m.name.ToLower().Contains(query) ||
                m.displayName.ToLower().Contains(query) ||
                m.description.ToLower().Contains(query) ||
                Array.Exists(m.tags, tag => tag.ToLower().Contains(query))
            );
        }
    }
}
