using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class PresenceComponentTests
    {
        [UnityTest]
        public IEnumerator ConfirmationAndFadeUseUnscaledTimeAndDisableRestoresCanvas()
        {
            var root = new GameObject("presence-test");
            root.SetActive(false);
            var canvas = root.AddComponent<CanvasGroup>();
            canvas.alpha = .7f;
            var component = root.AddComponent<MNNPresenceComponent>();
            typeof(MNNPresenceComponent).GetField("_content", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, canvas);
            component.Configure(new MNNPresenceOptions(confirmationSeconds: .02));
            float originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                root.SetActive(true);
                Assert.That(canvas.alpha, Is.EqualTo(0));
                component.Report(new MNNPresenceObservation(Time.realtimeSinceStartupAsDouble, MNNPresenceKind.Face, "a", 1));
                yield return new WaitForSecondsRealtime(.03f);
                Assert.That(canvas.alpha, Is.EqualTo(0));
                component.Report(new MNNPresenceObservation(Time.realtimeSinceStartupAsDouble, MNNPresenceKind.Face, "a", 1));
                yield return null;
                Assert.That(component.Current.CanStartInteraction, Is.True);
                Assert.That(canvas.alpha, Is.GreaterThan(0));
                Assert.That(canvas.alpha, Is.LessThan(1));
                root.SetActive(false);
                Assert.That(canvas.alpha, Is.EqualTo(.7f));
                Assert.That(component.Current.CanStartInteraction, Is.False);
            }
            finally { Time.timeScale = originalTimeScale; Object.DestroyImmediate(root); }
        }

        [UnityTest]
        public IEnumerator AllowListSurvivesDisableAndEnable()
        {
            var root = new GameObject("presence-authorization-test");
            root.SetActive(false);
            var component = root.AddComponent<MNNPresenceComponent>();
            component.Configure(new MNNPresenceOptions(confirmationSeconds: 0), new[] { "operator" });
            try
            {
                root.SetActive(true);
                component.Report(new MNNPresenceObservation(Time.realtimeSinceStartupAsDouble, MNNPresenceKind.Face, "a", 1, "visitor"));
                Assert.That(component.Current.CanStartInteraction, Is.False);
                root.SetActive(false);
                root.SetActive(true);
                component.Report(new MNNPresenceObservation(Time.realtimeSinceStartupAsDouble, MNNPresenceKind.Face, "a", 1, "visitor"));
                Assert.That(component.Current.CanStartInteraction, Is.False);
                component.Report(new MNNPresenceObservation(Time.realtimeSinceStartupAsDouble, MNNPresenceKind.Face, "a", 1, "operator"));
                Assert.That(component.Current.CanStartInteraction, Is.True);
                yield return null;
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
