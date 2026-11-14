using System;
using System.IO;

namespace MNN.Samples.SiriAI.Harnesses
{
    public sealed class TaskHostRequestException : IOException
    {
        public int StatusCode { get; }
        public bool Retryable => StatusCode == 408 || StatusCode == 429 || StatusCode >= 500;
        public TaskHostRequestException(int statusCode, string message) : base("Task host " + statusCode + ": " + message)
        { StatusCode = statusCode; }
    }
}
