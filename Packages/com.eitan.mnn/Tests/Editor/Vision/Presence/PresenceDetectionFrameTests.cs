using System;
using MNN.Unity;
using NUnit.Framework;
namespace MNN.Unity.Tests
{
    public sealed class PresenceDetectionFrameTests
    {
        [Test] public void DisplayGeometryIsOwnedAndNeverGrantsAdmission()
        {
            var detections = new[] {new MNNDetection(0,"face",.9f,10,20,30,40)};
            var frame = new MNNPresenceDetectionFrame(640,480,detections,0);
            detections[0] = null;
            Assert.That(frame.Detections[0].X,Is.EqualTo(10));
            var observation = MNNPresenceObservation.Absent(1,frame);
            Assert.That(new MNNPresenceController().Observe(observation,1).CanStartInteraction,Is.False);
            Assert.That(observation.DetectionFrame.SelectedIndex,Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(()=>new MNNPresenceDetectionFrame(640,480,new MNNDetection[0],0));
            Assert.Throws<ArgumentException>(()=>new MNNPresenceDetectionFrame(640,480,new[]{new MNNDetection(0,"face",.9f,float.NaN,0,20,20)}));
        }
    }
}
