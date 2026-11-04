namespace MNN.Unity
{
    /// <summary>Single-owner monotonic inactivity clock. A new input revision always starts a full window.</summary>
    public class MNNConversationInactivity
    {
        private int _revision = -1;
        private double _lastActivity;
        public bool Tick(double now, int revision, bool realtime, bool activity, int timeoutSeconds)
        {
            if (!realtime || revision != _revision || activity || timeoutSeconds == 0) _lastActivity = now;
            _revision = revision;
            return realtime && timeoutSeconds > 0 && now - _lastActivity >= timeoutSeconds;
        }
    }
}
