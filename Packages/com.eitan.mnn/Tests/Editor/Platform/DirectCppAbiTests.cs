using System;
using System.Text;
using NUnit.Framework;
using MNN.Unity.Interop;

namespace MNN.Unity.Tests
{
    public class DirectCppAbiTests
    {
        // Exercise native libc++ allocation/destruction on both sides of the
        // short-string threshold, including embedded NUL and multibyte UTF-8.
        [TestCase(""), TestCase("1234567890123456789012"), TestCase("12345678901234567890123"), TestCase("你好，世界🙂一二三四五六七八九十"), TestCase("before\0after")]
        public void NativeString_RoundTripsOwnedUtf8(string value)
        {
            MNNInterop.RequireAbi();
            byte[] copy;
            using (var native = new MNNInterop.NativeString(value))
                copy = MNNInterop.StringBytes(ref native.Value);
            GC.Collect();
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(value), copy);
        }
    }
}
