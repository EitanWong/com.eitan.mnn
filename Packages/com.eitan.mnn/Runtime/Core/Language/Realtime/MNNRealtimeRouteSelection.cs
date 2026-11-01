using System;
using System.Collections.Generic;
using System.Text;

namespace MNN.Unity
{
    /// <summary>Immutable startup/control-path decision. Does not own, create or switch inference resources.</summary>
    public sealed class MNNRealtimeRouteSelection
    {
        public MNNRealtimeReport Selected { get; }
        public bool CanStart => Selected != null;
        public IReadOnlyList<MNNRealtimeReport> Attempts { get; }
        public IReadOnlyList<MNNRealtimeRoute> ExcludedRoutes { get; }

        internal MNNRealtimeRouteSelection(MNNRealtimeReport[] attempts, MNNRealtimeRoute[] excludedRoutes)
        {
            Attempts = Array.AsReadOnly(attempts);
            ExcludedRoutes = Array.AsReadOnly(excludedRoutes);
            if (attempts.Length > 0 && attempts[attempts.Length - 1].CanStart)
                Selected = attempts[attempts.Length - 1];
        }

        /// <summary>Returns the report consumed by MNNRealtimeSessionComponent.Configure, or explains all rejected routes.</summary>
        public MNNRealtimeReport RequireSelected()
        {
            if (Selected == null)
                throw new NotSupportedException(ToString());
            return Selected;
        }

        public override string ToString()
        {
            var result = new StringBuilder();
            result.Append(Selected == null ? "No eligible conversation route." : $"Selected {Selected.Route}.");
            foreach (var excluded in ExcludedRoutes)
                result.Append(" Excluded: ").Append(excluded).Append('.');
            foreach (var attempt in Attempts)
                result.Append(' ').Append(attempt);
            return result.ToString();
        }
    }
}
