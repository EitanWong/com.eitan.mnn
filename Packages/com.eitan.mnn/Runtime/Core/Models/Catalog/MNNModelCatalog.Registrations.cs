namespace MNN.Unity
{
    public partial class MNNModelCatalog
    {
        /// <summary>Refresh shared deployment registrations without duplicating catalog rows.</summary>
        public void IncludeRegisteredProfiles()
        {
            foreach (var registration in MNNModelRegistry.All)
            {
                models.RemoveAll(model => model.provider == registration.Source &&
                    string.Equals(model.repositoryId, registration.Repository, System.StringComparison.OrdinalIgnoreCase));
                models.Add(registration.CreateModel());
            }
        }
    }
}
