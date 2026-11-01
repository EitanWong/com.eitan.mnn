using System;

namespace MNN.Unity
{
    [Serializable]
    public class MNNModelOutputSpec
    {
        public string name;
        public int[] shape;
        public string dataType;
        public int classCount;
    }
}
