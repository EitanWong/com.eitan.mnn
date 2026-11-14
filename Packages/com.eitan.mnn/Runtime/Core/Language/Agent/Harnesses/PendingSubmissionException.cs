using System;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed class PendingSubmissionException : Exception
    {
        public string SubmissionKey { get; }
        public PendingSubmissionException(string key, Exception cause) : base("Admission is unconfirmed; the persisted invocation will be replayed on reconnect.", cause)
        { SubmissionKey = key; }
    }
}
