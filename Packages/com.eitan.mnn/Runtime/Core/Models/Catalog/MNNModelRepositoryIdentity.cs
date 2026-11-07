using System;

namespace MNN.Unity
{
    /// <summary>Normalized source/repository/revision key, independent of display names and execution support.</summary>
    public readonly struct MNNModelRepositoryIdentity : IEquatable<MNNModelRepositoryIdentity>
    {
        public MNNModelSource Source { get; }
        public string Repository { get; }
        public string Revision { get; }
        private MNNModelRepositoryIdentity(MNNModelSource source, string repository, string revision)
        { Source = source; Repository = repository; Revision = revision; }
        public static string DefaultRevision(MNNModelSource source) => source == MNNModelSource.ModelScope ? "master" : "main";
        public static bool TryCreate(MNNRemoteModel model, out MNNModelRepositoryIdentity identity)
        {
            identity = default;
            if (model == null || !model.isRepository || !Enum.IsDefined(typeof(MNNModelSource), model.provider)) return false;
            string repository = model.repositoryId ?? model.modelScopeId;
            if (repository == null || repository.Length > 512) return false;
            string[] parts = repository.Split('/');
            if (parts.Length != 2) return false;
            foreach (string part in parts)
            {
                if (part.Length == 0 || part == "." || part == "..") return false;
                foreach (char c in part) if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.') return false;
            }
            string revision = string.IsNullOrWhiteSpace(model.revision) ? DefaultRevision(model.provider) : model.revision;
            if (revision.Length > 512) return false;
            foreach (char c in revision) if (char.IsControl(c)) return false;
            identity = new MNNModelRepositoryIdentity(model.provider, repository.ToLowerInvariant(), revision);
            return true;
        }
        public static MNNModelRepositoryIdentity FromModel(MNNRemoteModel model) => TryCreate(model, out var identity)
            ? identity : throw new ArgumentException("Supply a valid model repository identity.", nameof(model));
        public bool Equals(MNNModelRepositoryIdentity other) => Source == other.Source && Repository == other.Repository && Revision == other.Revision;
        public override bool Equals(object obj) => obj is MNNModelRepositoryIdentity other && Equals(other);
        public override int GetHashCode() => ((int)Source * 397 ^ (Repository?.GetHashCode() ?? 0)) * 397 ^ (Revision?.GetHashCode() ?? 0);
        public override string ToString() => Source + ":" + Repository + "@" + Revision;
    }
}
