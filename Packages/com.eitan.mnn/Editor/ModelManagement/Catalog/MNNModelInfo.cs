using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity.Editor
{
    [Serializable]
    public class MNNModelInfo
    {
        public string id;
        public MNNModelProvider provider = MNNModelProvider.ModelScope;
        public string repositoryId;
        public string revision;
        public string modelScopeId;
        public string name;
        public string displayName;
        public string category;
        public string family;
        public string generation;
        public string description;
        public string author;
        public string version;
        public string downloadUrl;
        public long fileSize;
        public long parameterCount;
        public bool parameterCountInferred;
        public long downloadCount;
        public string[] tags;
        public string[] tasks;
        public string previewImage;
        public ModelInputSpec inputSpec;
        public ModelOutputSpec outputSpec;
        public bool isRepository;
        public bool runtimeAvailable = true;
        public string[] mnnFiles;
        public string supportSummary;
        public bool isInstalled;
        public string localPath;
        internal MNNModelInfo CopyCatalogEntry()
        {
            var copy = (MNNModelInfo)MemberwiseClone();
            copy.tags = (string[])tags?.Clone();
            copy.tasks = (string[])tasks?.Clone();
            copy.mnnFiles = (string[])mnnFiles?.Clone();
            copy.inputSpec = inputSpec == null ? null : new ModelInputSpec{name = inputSpec.name, shape = (int[])inputSpec.shape?.Clone(), dataType = inputSpec.dataType, format = inputSpec.format};
            copy.outputSpec = outputSpec == null ? null : new ModelOutputSpec{name = outputSpec.name, shape = (int[])outputSpec.shape?.Clone(), dataType = outputSpec.dataType, classCount = outputSpec.classCount};
            copy.isInstalled = false;
            copy.localPath = null;
            return copy;
        }
    }

    public enum MNNModelProvider
    {
        ModelScope,
        HuggingFace
    }
}
