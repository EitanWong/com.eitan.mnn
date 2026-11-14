using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MNN.Samples.SiriAI.Harnesses;
using NUnit.Framework;

namespace MNN.Samples.SiriAI.Tests
{
    public sealed class SiriAIHarnessSkillsTests
    {
        [Test] public void PendingSkillAdmissionReopensAndUsesTheManagementRoute() => Fixtures.HarnessTestCase.Run(VerifyAsync);
        public async Task VerifyAsync()
        {
            string folder = Fixtures.HarnessTestCase.NewDirectory();
            var handler = new LostResponseHandler();
            try
            {
                using (var remote = new RemoteTaskClient(new Uri("http://localhost:8765"), "test-token", handler))
                {
                    var request = new HarnessSkillRequest { SubmissionKey = "stable-key", Operation = "install", Source = "owner/repo", Names = new[] { "review" } };
                    using (var client = new HarnessSkillClient(remote, folder))
                    {
                        bool pending = false;
                        try { await client.SubmitAsync(request, CancellationToken.None); }
                        catch (PendingSubmissionException) { pending = true; }
                        Assert.True(pending); Assert.AreEqual(1, client.PendingCount);
                        request.Source = "caller changed"; request.Names[0] = "changed";
                    }
                    using (var client = new HarnessSkillClient(remote, folder))
                    {
                        Assert.AreEqual(1, client.PendingCount);
                        await client.RetryAsync(CancellationToken.None);
                        Assert.AreEqual(0, client.PendingCount);
                    }
                    Assert.AreEqual(2, handler.Calls);
                }
            }
            finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        }
        [Test] public void SkillRequestsRequireExplicitNamesAndClosedOperations()
        {
            foreach (var request in new[]
            {
                new HarnessSkillRequest { SubmissionKey = "k", Operation = "shell", Source = "anything" },
                new HarnessSkillRequest { SubmissionKey = "k", Operation = "install", Source = "owner/repo" },
                new HarnessSkillRequest { SubmissionKey = "k", Operation = "install", Source = "--all", Names = new[] { "review" } },
                new HarnessSkillRequest { SubmissionKey = "k", Operation = "install", Source = "owner/repo", Names = new[] { "--all" } },
                new HarnessSkillRequest { SubmissionKey = "k", Operation = "update", Backends = new[] { "qwen" } }
            }) Assert.Throws<ArgumentException>(request.Validate);
            var valid = new HarnessSkillRequest { SubmissionKey = "k", Operation = "source-list", Source = "https://example.org/repo?a=1&b=2" };
            var copy = TaskHostJson.Read<HarnessSkillRequest>(TaskHostJson.Write(valid)); Assert.AreEqual(valid.Source, copy.Source);
        }
        private sealed class LostResponseHandler : HttpMessageHandler
        {
            internal int Calls;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                ++Calls;
                Assert.AreEqual("/v1/skills/operations", request.RequestUri.AbsolutePath);
                var body = TaskHostJson.Read<HarnessSkillRequest>(await request.Content.ReadAsByteArrayAsync());
                Assert.AreEqual("stable-key", body.SubmissionKey); Assert.AreEqual("owner/repo", body.Source); Assert.AreEqual("review", body.Names[0]);
                if (Calls == 1) throw new HttpRequestException("Lost admission response");
                return new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new StringContent("{\"Id\":\"operation-a\",\"ConversationId\":\"skill-library\",\"SubmissionKey\":\"stable-key\",\"State\":3}", Encoding.UTF8, "application/json") };
            }
        }
    }
}
