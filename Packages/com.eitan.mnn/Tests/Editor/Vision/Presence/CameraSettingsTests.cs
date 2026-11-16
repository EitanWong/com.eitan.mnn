using System;
using MNN.Unity;
using NUnit.Framework;
namespace MNN.Unity.Tests
{
    public sealed class CameraSettingsTests
    {
        [Test] public void AutomaticDefaultsAreBoundedAndSavedDeviceIsNotSilentlyReplaced()
        {
            var settings = new MNNCameraSettings(); settings.Validate();
            Assert.That(settings.RequestedWidth, Is.EqualTo(1280)); Assert.That(settings.RequestedHeight, Is.EqualTo(720));
            Assert.That(settings.RequestedFramesPerSecond, Is.EqualTo(30)); Assert.That(settings.DetectionFramesPerSecond, Is.EqualTo(10));
            Assert.That(settings.SleepFramesPerSecond, Is.EqualTo(2)); Assert.That(settings.MaximumImageEdge, Is.EqualTo(640));
            var names = new[] { "external", "front" }; var front = new[] {false, true};
            Assert.That(MNNCameraSettings.SelectDevice(names, front, ""), Is.EqualTo(1));
            Assert.That(MNNCameraSettings.SelectDevice(names, front, "external"), Is.EqualTo(0));
            Assert.That(MNNCameraSettings.SelectDevice(names, front, "disconnected"), Is.EqualTo(-1));
            Assert.That(MNNCameraSettings.SelectDevice(new string[0], new bool[0], ""), Is.EqualTo(-1));
        }
        [Test] public void ManualSettingsAreOwnedValidatedAndPreservedAcrossSnapshots()
        {
            var interaction = new MNNInteractionSettings(); interaction.camera.automaticQuality = false;
            interaction.camera.width = 1920; interaction.camera.height = 1080; interaction.camera.framesPerSecond = 25;
            var copy = interaction.Snapshot(); copy.camera.deviceName = "other";
            Assert.That(interaction.camera.deviceName, Is.Empty); Assert.That(copy.camera.RequestedWidth, Is.EqualTo(1920));
            Assert.That(copy.camera.RequestedFramesPerSecond, Is.EqualTo(25));
            copy.camera.sleepFramesPerSecond = 10; copy.camera.detectionFramesPerSecond = 2;
            Assert.Throws<ArgumentException>(() => copy.Validate());
        }
    }
}
