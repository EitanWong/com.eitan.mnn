using System.Collections.Generic;
using System.Threading;

namespace MNN.Unity.Editor
{
    /// <summary>Catalog counters owned by the discovery worker until publication.
    /// Drawing a task row only reads integers, never validates a model.</summary>
    internal sealed class MNNStudioTaskCatalog
    {
        private readonly int[] _local = new int[MNNStudioTasks.All.Length];
        private readonly int[] _ready = new int[MNNStudioTasks.All.Length];
        internal bool IsComplete { get; private set; }
        internal int Local(MNNStudioTask task) => _local[(int)task];
        internal int Ready(MNNStudioTask task) => _ready[(int)task];

        internal static MNNStudioTaskCatalog CountLocal(IReadOnlyList<MNNStudioModel> models)
        {
            var catalog = new MNNStudioTaskCatalog();
            foreach (var task in MNNStudioTasks.All)
                foreach (var model in models)
                    if (task.Accepts(model))
                        ++catalog._local[(int)task.Task];
            return catalog;
        }

        internal static MNNStudioTaskCatalog Prepare(IReadOnlyList<MNNStudioModel> models, CancellationToken cancellation)
        {
            var catalog = CountLocal(models);
            foreach (var model in models)
            {
                cancellation.ThrowIfCancellationRequested();
                // Piper readiness consults an EditorPrefs dictionary override. Finish
                // those small checks on the Editor thread before publishing the counts.
                if (!model.IsPiper)
                    catalog.AddReady(model);
            }
            return catalog;
        }

        private void AddReady(MNNStudioModel model)
        {
            foreach (var task in MNNStudioTasks.All)
                if (task.CanRun(model))
                    ++_ready[(int)task.Task];
        }

        internal void CompleteEditorSettings(IReadOnlyList<MNNStudioModel> models)
        {
            if (IsComplete)
                return;
            foreach (var model in models)
                if (model.IsPiper)
                    AddReady(model);
            IsComplete = true;
        }
    }
}
