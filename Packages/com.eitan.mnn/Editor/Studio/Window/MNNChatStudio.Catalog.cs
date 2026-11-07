namespace MNN.Unity.Editor
{
    public sealed partial class MNNChatStudio
    {
        private MNNStudioTaskCatalog _taskCatalog;
        private bool _catalogFailed;

        private void InvalidateTaskCatalog()
        {
            _taskCatalog = MNNStudioTaskCatalog.CountLocal(_models);
            _catalogFailed = false;
        }

        private string CatalogStatus(MNNStudioTaskInfo task)
        {
            if (!task.Runnable)
                return "Model setup";
            if (_catalogFailed)
                return "Check failed";
            if (_taskCatalog?.IsComplete != true)
                return "Checking…";
            int ready = _taskCatalog.Ready(task.Task);
            return ready > 0 && MNNPlatformSupport.IsSupported ? ready + " ready" : "Choose model";
        }
    }
}
