using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace MNN.Unity
{
    public sealed class MNNVoxTurnResult
    {
        public IReadOnlyDictionary<string, MNNVoxTurnAnswer> Answers
        {
            get;
        }
        internal MNNVoxTurnResult(Dictionary<string, MNNVoxTurnAnswer> answers) => Answers = new ReadOnlyDictionary<string, MNNVoxTurnAnswer>(answers);
    }
}
