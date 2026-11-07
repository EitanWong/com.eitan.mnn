using NUnit.Framework;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class SharedKeyboardGeometryTests
    {
        [Test]
        public void OcclusionRespectsDesktopClientOriginScalingAndAlreadyResizedWindows()
        {
            var client = new Rect(-1600, 100, 1200, 900);
            var keyboard = new Rect(-1800, 700, 1800, 400);
            Assert.AreEqual(new Rect(0, 0, 800, 200),
                MNNSoftKeyboard.MapOcclusion(keyboard, client, new Vector2(800, 600)));
            Assert.AreEqual(Rect.zero, MNNSoftKeyboard.MapOcclusion(keyboard,
                new Rect(-1600, 100, 1200, 600), new Vector2(800, 400)));
            Assert.AreEqual(Rect.zero, MNNSoftKeyboard.MapOcclusion(keyboard, Rect.zero, Vector2.one));
        }
    }
}
