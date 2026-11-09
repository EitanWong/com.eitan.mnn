using System;
using System.Collections.Generic;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Immutable conversation reading position. Text identity and pixel offset survive reflow;
    /// expansion indices refer to append-only transcript entries, not recycled UI rows.</summary>
    [Serializable]
    public sealed class MNNConversationViewState
    {
        [SerializeField] private int pageStart, firstEntry = -1, characterOffset;
        [SerializeField] private float scrollOffset;
        [SerializeField] private bool followingLatest = true;
        [SerializeField] private int[] expanded = Array.Empty<int>();
        public int PageStart => pageStart;
        public int FirstEntry => firstEntry;
        public int CharacterOffset => characterOffset;
        public float ScrollOffset => scrollOffset;
        public bool FollowingLatest => followingLatest;
        public IReadOnlyList<int> ExpandedEntries => Array.AsReadOnly(expanded);
        public static MNNConversationViewState Latest => new MNNConversationViewState(0, -1, 0, 0, true, Array.Empty<int>());
        public MNNConversationViewState(int pageStart, int firstEntry, int characterOffset, float scrollOffset,
            bool followingLatest, IReadOnlyList<int> expandedEntries)
        {
            this.pageStart = pageStart; this.firstEntry = firstEntry; this.characterOffset = characterOffset;
            this.scrollOffset = scrollOffset; this.followingLatest = followingLatest;
            if (expandedEntries == null || expandedEntries.Count > 256) throw new ArgumentException("Expansion state exceeds its budget.");
            expanded = new int[expandedEntries.Count]; for (int i = 0; i < expanded.Length; ++i) expanded[i] = expandedEntries[i];
            Array.Sort(expanded); Validate();
        }
        internal void Validate()
        {
            if (pageStart < 0 || firstEntry < -1 || characterOffset < 0 || characterOffset > MNNConversationMailbox.MaximumTextCharacters ||
                float.IsNaN(scrollOffset) || float.IsInfinity(scrollOffset) || Math.Abs(scrollOffset) > 1000000 || expanded == null || expanded.Length > 256)
                throw new ArgumentException("Invalid conversation reading position.");
            for (int i = 0; i < expanded.Length; ++i)
                if (expanded[i] < 0 || (i > 0 && expanded[i] <= expanded[i - 1])) throw new ArgumentException("Invalid expansion identity.");
        }
        internal bool SameAs(MNNConversationViewState other)
        {
            if (other == null || pageStart != other.pageStart || firstEntry != other.firstEntry || characterOffset != other.characterOffset ||
                scrollOffset != other.scrollOffset || followingLatest != other.followingLatest || expanded.Length != other.expanded.Length) return false;
            for (int i = 0; i < expanded.Length; ++i) if (expanded[i] != other.expanded[i]) return false;
            return true;
        }
    }
}
