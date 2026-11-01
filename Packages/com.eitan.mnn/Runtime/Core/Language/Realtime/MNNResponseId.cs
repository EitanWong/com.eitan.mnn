using System;

namespace MNN.Unity
{
    /// <summary>Immutable identity captured when a reply starts, including by every asynchronous producer.</summary>
    public readonly struct MNNResponseId : IEquatable<MNNResponseId>
    {
        public long Session { get; }
        public long Epoch { get; }
        public bool IsValid => Session > 0 && Epoch > 0 && (Epoch & 1) != 0;

        internal MNNResponseId(long session, long epoch)
        {
            Session = session;
            Epoch = epoch;
        }

        public bool Equals(MNNResponseId other) => Session == other.Session && Epoch == other.Epoch;
        public override bool Equals(object obj) => obj is MNNResponseId other && Equals(other);
        public override int GetHashCode() => (Session.GetHashCode() * 397) ^ Epoch.GetHashCode();
        public static bool operator ==(MNNResponseId left, MNNResponseId right) => left.Equals(right);
        public static bool operator !=(MNNResponseId left, MNNResponseId right) => !left.Equals(right);
    }
}
