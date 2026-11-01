using System;

namespace MNN.Unity
{
    [Serializable]
    public class MNNModelInputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public string format;
    }
}
