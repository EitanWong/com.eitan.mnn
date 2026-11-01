using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace MNN.Unity
{
    internal sealed class MNNVoxTurnPrompt
    {
        internal static readonly string[] Tasks = { "eot", "state4", "state_ten", "intent", "invalid", "resume" };
        private readonly MNNVoxTurnTokenizer _tokenizer;
        private readonly object _questions;
        private readonly Dictionary<string, Question> _cache = new Dictionary<string, Question>(StringComparer.Ordinal);
        private readonly int[] _head, _open, _close;
        internal sealed class Question
        {
            internal int[] Ids, Positions; internal string[] Labels;
        }
        internal sealed class Option
        {
            internal int Row; internal string Task; internal int[] Positions; internal string[] Labels;
        }
        internal sealed class Batch
        {
            internal int Count, Length; internal int[] Ids, Mask, Positions, AudioOffsets; internal Option[] Options;
        }
        internal MNNVoxTurnPrompt(string directory)
        {
            _tokenizer = new MNNVoxTurnTokenizer(Path.Combine(directory, "tokenizer.json"));
            _questions = MNNModelData.Read(Path.Combine(directory, "questions.json"));
            _head = _tokenizer.Encode("<|im_start|>system\n<|im_end|>\n<|im_start|>user\n");
            _open = _tokenizer.Encode("<|audio_start|>");
            _close = _tokenizer.Encode("<|audio_end|>");
        }
        private Question GetQuestion(string task, string language)
        {
            string key = task + "/" + language;
            if (_cache.TryGetValue(key, out var value))
                return value;
            object data = MNNModelData.Get(_questions, task);
            var labels = MNNModelData.Array(MNNModelData.Get(data, "labels")).Select(v => (string)v).ToArray();
            string phrasing = (string)MNNModelData.Array(MNNModelData.Get(MNNModelData.Get(data, "phrasings"), language))[0];
            string head = language == "zh" ? "\n问题：" + phrasing + "\n选项：" : "\nQuestion: " + phrasing + "\nOptions:";
            var ids = new List<int>(_tokenizer.Encode(head));
            var positions = new List<int>();
            object choices = MNNModelData.Get(MNNModelData.Get(data, "options"), language);
            foreach (string label in labels)
            {
                ids.AddRange(_tokenizer.Encode(" " + (string)MNNModelData.Get(choices, label)));
                positions.Add(ids.Count - 1);
            }
            value = new Question { Ids = ids.ToArray(), Positions = positions.ToArray(), Labels = labels };
            _cache.Add(key, value);
            return value;
        }
        private List<int> Prefix(MNNVoxTurnRequest request, out int audioOffset)
        {
            var ids = new List<int>(_head);
            audioOffset = -1;
            if (request.HasAudio)
            {
                ids.AddRange(_open);
                audioOffset = ids.Count;
                ids.AddRange(Enumerable.Repeat(-2, 104));
                ids.AddRange(_close);
            }
            int maximum = request.HasAudio ? 64 : 256;
            if (!string.IsNullOrEmpty(request.Text))
            {
                int[] text = _tokenizer.Encode("\n" + request.Text);
                ids.AddRange(text.Skip(Math.Max(0, text.Length - maximum)));
            }
            string marker = request.Language == "zh" ? (request.Speaker == "user" ? "\n【用户正在说话】" : "\n【助手正在说话】") : (request.Speaker == "user" ? "\n[User is speaking]" : "\n[Agent is speaking]");
            ids.AddRange(_tokenizer.Encode(marker));
            return ids;
        }
        internal Batch Build(IReadOnlyList<MNNVoxTurnRequest> requests, string[] tasks, int minimumLength = 0)
        {
            var rows = new List<int[]>();
            var options = new List<Option>();
            var spans = new List<int[]>[requests.Count];
            var offsets = new int[requests.Count];
            for (int b = 0; b < requests.Count; ++b)
            {
                var ids = Prefix(requests[b], out offsets[b]);
                spans[b] = new List<int[]>();
                foreach (string task in tasks)
                {
                    var question = GetQuestion(task, requests[b].Language);
                    int start = ids.Count;
                    ids.AddRange(question.Ids);
                    spans[b].Add(new[] { start, question.Ids.Length });
                    options.Add(new Option { Row = b, Task = task, Positions = question.Positions.Select(p => p + start).ToArray(), Labels = question.Labels });
                }
                rows.Add(ids.ToArray());
            }
            int length = Math.Max(minimumLength, (rows.Max(row => row.Length) + 63) / 64 * 64);
            if (length > 2048)
                throw new InvalidDataException("VoxTurn sequence exceeds rotary positions.");
            var batch = new Batch { Count = rows.Count, Length = length, Ids = Enumerable.Repeat(-1, rows.Count * length).ToArray(), Mask = new int[rows.Count * length * length], Positions = new int[rows.Count * length], AudioOffsets = offsets, Options = options.ToArray() };
            for (int b = 0; b < rows.Count; ++b)
            {
                Array.Copy(rows[b], 0, batch.Ids, b * length, rows[b].Length);
                Mask(batch, b, spans[b]);
            }
            return batch;
        }
        private static void Mask(Batch batch, int row, List<int[]> spans)
        {
            int L = batch.Length, at = row * L * L, prefix = spans[0][0];
            for (int i = 0; i < L; ++i)
            {
                batch.Positions[row * L + i] = i;
                for (int j = 0; j <= i; ++j)
                    batch.Mask[at + i * L + j] = 1;
            }
            foreach (int[] span in spans)
                for (int i = span[0]; i < span[0] + span[1]; ++i)
                {
                    batch.Positions[row * L + i] = prefix + i - span[0];
                    for (int j = prefix; j < span[0]; ++j)
                        batch.Mask[at + i * L + j] = 0;
                }
        }
    }
}
