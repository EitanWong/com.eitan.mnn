using System;

namespace MNN.Unity
{
    public enum MNNChatRole
    {
        System,
        User,
        Assistant
    }

    /// <summary>One role/content pair for the official MNN ChatMessages API.</summary>
    public sealed class MNNChatMessage
    {
        public MNNChatRole Role { get; }

        public string Content { get; }

        public MNNChatMessage(MNNChatRole role, string content)
        {
            if (!Enum.IsDefined(typeof(MNNChatRole), role))
                throw new ArgumentOutOfRangeException(nameof(role));
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            Role = role;
            Content = content;
        }
    }
}
