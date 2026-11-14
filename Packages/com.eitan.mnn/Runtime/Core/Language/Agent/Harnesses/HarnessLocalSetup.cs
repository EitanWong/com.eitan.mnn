using System;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace MNN.Samples.SiriAI.Harnesses
{
    /// <summary>Non-secret result of local host preparation. Read the private token only when connecting.</summary>
    [DataContract]
    public sealed class HarnessLocalSetup
    {
        public static string ScopeFor(string backend, string workspace)
        {
            HarnessBackendCatalog.Get(backend);
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(backend + "\n" + Path.GetFullPath(workspace)))).Replace("-", "").ToLowerInvariant();
        }
        [DataMember] public string ScopeId;
        [DataMember] public string Address;
        [DataMember] public string Backend;
        [DataMember] public string Workspace;
        [DataMember] public string TokenPath;
    }
}
