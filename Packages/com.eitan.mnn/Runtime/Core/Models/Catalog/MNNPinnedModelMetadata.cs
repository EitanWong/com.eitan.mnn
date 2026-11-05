using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MNN.Unity
{
    /// <summary>Bounded metadata-only proof checks; never downloads weights or claims inference compatibility.</summary>
    public static partial class MNNPinnedModelMetadata
    {
        public sealed class Proof
        {
            public string Repository, Revision, Path, Sha256;
            public bool ModelScope;
            public Action<bool> SetOnline;
        }
        [Serializable] private sealed class Repository { public string id, sha; }
        [Serializable] private sealed class ScopeResponse { public int Code; public bool Success; public ScopeData Data; }
        [Serializable] private sealed class ScopeData { public ScopeFile[] Files; }
        [Serializable] private sealed class ScopeFile { public string Path, Sha256; }

        public static async Task<int> RefreshAsync(IEnumerable<Proof> proofs, CancellationToken token, HttpClient client = null)
        {
            if (proofs == null) throw new ArgumentNullException(nameof(proofs));
            // Snapshot before awaiting so a UI cannot change the request identities in flight.
            var snapshot = proofs.Select(p => p == null ? throw new ArgumentException("Null metadata proof.") : new Proof
            { Repository = p.Repository, Revision = p.Revision, Path = p.Path, Sha256 = p.Sha256, ModelScope = p.ModelScope, SetOnline = p.SetOnline }).ToArray();
            bool owns = client == null;
            if (owns) client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            int found = 0;
            try
            {
                using (var gate = new SemaphoreSlim(4))
                {
                    await Task.WhenAll(snapshot.Select(async proof =>
                    {
                        await gate.WaitAsync(token).ConfigureAwait(false);
                        try
                        {
                            proof.SetOnline?.Invoke(false);
                            if (string.IsNullOrEmpty(proof.Repository)) return;
                            foreach (string host in proof.ModelScope ? new[] { "https://www.modelscope.cn" } : new[] { "https://huggingface.co", "https://hf-mirror.com" })
                            {
                                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                                {
                                    timeout.CancelAfter(TimeSpan.FromSeconds(8));
                                    string url = proof.ModelScope ? host + "/api/v1/models/" + proof.Repository + "/repo/files?Revision=" + Uri.EscapeDataString(proof.Revision) + "&Recursive=true"
                                        : host + "/api/models/" + proof.Repository + "/revision/" + Uri.EscapeDataString(proof.Revision);
                                    try
                                    {
                                        using (var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false))
                                        {
                                            if (!response.IsSuccessStatusCode) continue;
                                            string json = await MNNModelMetadata.ReadTextAsync(response.Content, timeout.Token, 4 * 1024 * 1024).ConfigureAwait(false);
                                            bool online;
                                            if (proof.ModelScope)
                                            {
                                                var data = JsonUtility.FromJson<ScopeResponse>(json);
                                                online = data?.Success == true && data.Code == 200 && data.Data?.Files != null && data.Data.Files.Any(file =>
                                                    file.Path == proof.Path && string.Equals(file.Sha256, proof.Sha256, StringComparison.OrdinalIgnoreCase));
                                            }
                                            else
                                            {
                                                var data = JsonUtility.FromJson<Repository>(json);
                                                online = data != null && string.Equals(data.id, proof.Repository, StringComparison.OrdinalIgnoreCase) && string.Equals(data.sha, proof.Revision, StringComparison.OrdinalIgnoreCase);
                                            }
                                            if (!online) continue;
                                            proof.SetOnline?.Invoke(true); Interlocked.Increment(ref found); return;
                                        }
                                    }
                                    catch (OperationCanceledException) when (!token.IsCancellationRequested) { }
                                    catch (HttpRequestException) { }
                                    catch (ArgumentException) { }
                                    catch (System.IO.InvalidDataException) { }
                                }
                            }
                        }
                        finally { gate.Release(); }
                    })).ConfigureAwait(false);
                }
                token.ThrowIfCancellationRequested(); return found;
            }
            finally { if (owns) client.Dispose(); }
        }
    }
}
