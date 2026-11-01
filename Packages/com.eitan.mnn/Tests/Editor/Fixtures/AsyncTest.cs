using System.Collections;
using System.Threading.Tasks;

namespace MNN.Unity.Tests
{
    // Unity 2021's test runner consumes IEnumerator, including failures after an await.
    internal static class AsyncTest
    {
        internal static IEnumerator Await(Task operation)
        {
            while (!operation.IsCompleted)
                yield return null;
            operation.GetAwaiter().GetResult();
        }
    }
}
