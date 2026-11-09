using System;

namespace MNN.Unity
{
    /// <summary>Shared conversation geometry: trailing user bubbles, full-width assistant prose and compact
    /// execution rows. The host binds these values to its UI toolkit and supplies its own colors and fonts.</summary>
    public readonly struct MNNConversationRowLayout
    {
        public float Width { get; }
        public float Left { get; }
        public float BodyTop { get; }
        public float HorizontalInset { get; }
        public bool DrawBubble { get; }
        public bool CanDisclose { get; }
        public MNNConversationRowLayout(float availableWidth, MNNChatRole role, MNNConversationEntryKind kind, bool first)
        {
            if (float.IsNaN(availableWidth) || float.IsInfinity(availableWidth) || availableWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(availableWidth));
            DrawBubble = role == MNNChatRole.User && kind == MNNConversationEntryKind.Message;
            CanDisclose = kind != MNNConversationEntryKind.Message;
            Width = DrawBubble ? Math.Min(680, availableWidth * .85f) : availableWidth;
            Left = DrawBubble ? availableWidth - Width : 0;
            BodyTop = first ? 44 : 12; HorizontalInset = 16;
        }
    }
}
