using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using UnityEditor;

namespace MNN.Unity.Editor
{
    [InitializeOnLoad]
    internal static class MNNStudioJobs
    {
        private static readonly List<MNNStudioSession> Active = new List<MNNStudioSession>();
        static MNNStudioJobs()
        {
            EditorApplication.update += Update;
            EditorApplication.quitting += BeforeQuit;
        }

        internal static void Track(MNNStudioSession session)
        {
            if (Active.Contains(session))
                return;
            if (Active.Count == 0)
                EditorApplication.LockReloadAssemblies();
            Active.Add(session);
        }

        internal static void Update()
        {
            if (Active.Count == 0)
                return;
            try
            {
                for (int i = Active.Count - 1; i >= 0; --i)
                {
                    var session = Active[i];
                    session.Poll();
                    if (!session.Busy)
                        Active.RemoveAt(i);
                }
            }
            finally
            {
                if (Active.Count == 0)
                    EditorApplication.UnlockReloadAssemblies();
            }
        }

        private static void BeforeQuit()
        {
            foreach (var session in Active)
                session.Dispose();
        // Model handles remain owned by active sessions until process exit. Do not
        // block Unity's main thread waiting for a long native generation here.
        }
    }
}
