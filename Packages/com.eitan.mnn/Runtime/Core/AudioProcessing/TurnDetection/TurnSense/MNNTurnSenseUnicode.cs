using System;
using System.IO;
using System.Linq;

namespace MNN.Unity
{
    internal sealed class MNNTurnSenseUnicode
    {
        private readonly int[] _letters, _numbers, _spaces;

        internal MNNTurnSenseUnicode(string path)
        {
            var root = MNNModelData.Object(MNNModelData.Read(path));
            if (!Equals(MNNModelData.Get(root, "tokenizers_version"), "0.23.3"))
                throw new InvalidDataException("Unsupported frozen TurnSense Unicode properties.");
            _letters = Read(root, "letter");
            _numbers = Read(root, "number");
            _spaces = Read(root, "space");
        }

        internal bool IsLetter(string text, int at) => Contains(_letters, char.ConvertToUtf32(text, at));
        internal bool IsNumber(string text, int at) => Contains(_numbers, char.ConvertToUtf32(text, at));
        internal bool IsSpace(string text, int at) => Contains(_spaces, char.ConvertToUtf32(text, at));

        private static int[] Read(object root, string name)
        {
            var values = MNNModelData.Array(MNNModelData.Get(root, name)).Select(MNNModelData.Integer).ToArray();
            if (values.Length == 0 || values.Length % 2 != 0) throw new InvalidDataException("Invalid Unicode property ranges.");
            for (int i = 0; i < values.Length; i += 2)
                if (values[i] < 0 || values[i + 1] > 0x10ffff || values[i] > values[i + 1] || i > 0 && values[i] <= values[i - 1])
                    throw new InvalidDataException("Unordered Unicode property ranges.");
            return values;
        }

        private static bool Contains(int[] ranges, int scalar)
        {
            int low = 0, high = ranges.Length / 2 - 1;
            while (low <= high)
            {
                int middle = (low + high) / 2, start = ranges[middle * 2], end = ranges[middle * 2 + 1];
                if (scalar < start) high = middle - 1;
                else if (scalar > end) low = middle + 1;
                else return true;
            }
            return false;
        }
    }
}
