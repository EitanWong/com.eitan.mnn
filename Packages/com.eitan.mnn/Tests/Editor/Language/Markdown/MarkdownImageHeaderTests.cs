using System;
using NUnit.Framework;

namespace MNN.Unity.Tests
{
    public sealed class MarkdownImageHeaderTests
    {
        [TestCase(3, 2)] [TestCase(16384, 16384)]
        public void PngDimensionsAreReadBeforeAnyTextureAllocation(int width, int height)
        {
            byte[] bytes = Png(width, height);
            Assert.True(MNNMarkdownImageHeader.TryReadDimensions(bytes, out var w, out var h));
            Assert.AreEqual(width, w); Assert.AreEqual(height, h);
            Assert.False(MNNMarkdownImageHeader.TryReadDimensions(new byte[24], out _, out _));
        }
        [Test]
        public void JpegSegmentsAreBoundedAndOnlySupportedFrameHeadersAreAccepted()
        {
            byte[] jpeg = {255,216,255,224,0,4,1,2,255,192,0,11,8,0,20,0,30,1,1,0x11,0,255,218,0,8,1,1,0,0,63,0};
            Assert.True(MNNMarkdownImageHeader.TryReadDimensions(jpeg, out var w, out var h)); Assert.AreEqual(30,w); Assert.AreEqual(20,h);
            jpeg[4] = 127;
            Assert.False(MNNMarkdownImageHeader.TryReadDimensions(jpeg, out _, out _));
        }
        [Test]
        public void MissingZeroOrOverflowingDimensionsAreRejected()
        {
            foreach (var bytes in new[] { Png(0, 1), Png(-1, 1), new byte[] {255,216,255}, new byte[] {255,216,255,192,0,1} })
                Assert.False(MNNMarkdownImageHeader.TryReadDimensions(bytes, out _, out _));
        }
        internal static byte[] Png(int width, int height)
        {
            var bytes = new byte[33];
            byte[] prefix = {137,80,78,71,13,10,26,10,0,0,0,13,73,72,68,82}; Array.Copy(prefix, bytes, prefix.Length);
            for (int i=0;i<4;++i) { bytes[16+i]=(byte)((uint)width>>(24-8*i)); bytes[20+i]=(byte)((uint)height>>(24-8*i)); }
            bytes[24]=8;bytes[25]=6;return bytes;
        }
    }
}
