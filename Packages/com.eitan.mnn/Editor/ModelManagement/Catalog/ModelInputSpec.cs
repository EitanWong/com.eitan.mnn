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
    public class ModelInputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public string format;
    }
}
