using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class FaceCameraImageTests
    {
        private static Color32 Pixel(byte value) => new Color32(value, 0, 0, 255);
        private static byte[] Reds(MNNRgbImage image)
        {
            var rgb = image.ToArray();
            var red = new byte[rgb.Length / 3];
            for (int i = 0; i < red.Length; ++i) red[i] = rgb[i * 3];
            return red;
        }

        [Test]
        public void ConvertsBottomLeftRowsAndEveryCameraRotation()
        {
            var pixels = new[] { Pixel(3), Pixel(4), Pixel(1), Pixel(2) };
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, Reds(MNNFaceCameraComponent.ReadImage(pixels, 2, 2, 0, false, 640)));
            CollectionAssert.AreEqual(new byte[] { 3, 1, 4, 2 }, Reds(MNNFaceCameraComponent.ReadImage(pixels, 2, 2, 90, false, 640)));
            CollectionAssert.AreEqual(new byte[] { 4, 3, 2, 1 }, Reds(MNNFaceCameraComponent.ReadImage(pixels, 2, 2, 180, false, 640)));
            CollectionAssert.AreEqual(new byte[] { 2, 4, 1, 3 }, Reds(MNNFaceCameraComponent.ReadImage(pixels, 2, 2, 270, false, 640)));
            CollectionAssert.AreEqual(new byte[] { 3, 4, 1, 2 }, Reds(MNNFaceCameraComponent.ReadImage(pixels, 2, 2, 0, true, 640)));
        }

        [Test]
        public void RotationSwapsDimensionsAndImageBudgetIsBounded()
        {
            var pixels = new[] { Pixel(1), Pixel(2), Pixel(3), Pixel(4), Pixel(5), Pixel(6) };
            var image = MNNFaceCameraComponent.ReadImage(pixels, 3, 2, 90, false, 2);
            Assert.That(image.Width, Is.EqualTo(1));
            Assert.That(image.Height, Is.EqualTo(2));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => MNNFaceCameraComponent.ReadImage(pixels, 3, 2, 45, false, 640));
        }
    }
}
