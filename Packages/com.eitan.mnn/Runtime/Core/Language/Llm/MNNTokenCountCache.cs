using System;
using System.Collections.Generic;

namespace MNN.Unity
{
    // Owned by one tokenizer instance; caller holds the model gate. Bounds retained
    // UTF-16 characters as well as entry count, including unusually large prompts.
    internal sealed class MNNTokenCountCache
    {
        private const int EntryLimit = 128;
        private const int CharacterLimit = 131072;
        private const int TextLimit = 32768;
        private readonly Dictionary<string, LinkedListNode<KeyValuePair<string, int>>> _entries =
            new Dictionary<string, LinkedListNode<KeyValuePair<string, int>>>(StringComparer.Ordinal);
        private readonly LinkedList<KeyValuePair<string, int>> _recent = new LinkedList<KeyValuePair<string, int>>();
        internal int Count => _entries.Count;
        internal int Characters { get; private set; }

        internal int Get(string text, Func<string, int> count)
        {
            if (_entries.TryGetValue(text, out var node))
            {
                _recent.Remove(node);
                _recent.AddFirst(node);
                return node.Value.Value;
            }
            int tokens = count(text);
            if (text.Length > TextLimit) return tokens;
            while (_entries.Count >= EntryLimit || Characters + text.Length > CharacterLimit)
            {
                var last = _recent.Last;
                Characters -= last.Value.Key.Length;
                _entries.Remove(last.Value.Key);
                _recent.RemoveLast();
            }
            node = _recent.AddFirst(new KeyValuePair<string, int>(text, tokens));
            _entries.Add(text, node);
            Characters += text.Length;
            return tokens;
        }

        internal void Clear()
        {
            _entries.Clear();
            _recent.Clear();
            Characters = 0;
        }
    }
}
