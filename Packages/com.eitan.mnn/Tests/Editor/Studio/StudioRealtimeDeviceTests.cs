using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MNN.Unity.Tests
{
    public sealed class StudioRealtimeDeviceTests
    {
        [UnityTest]
        public IEnumerator EditorDevicePullsPcmFromTheSharedPipeline()
        {
            var go = new GameObject("Studio output probe") { hideFlags = HideFlags.HideAndDontSave };
            var device = go.AddComponent<MNNRealtimeAudioDevice>();
            try
            {
                var output = new MNNRealtimeOutputPipeline(new MNNPcmFormat(AudioSettings.outputSampleRate, 1), 512);
                device.Bind(output, new MNNPcmFormat(16000, 1));
                device.StartOutput();
                double until = UnityEditor.EditorApplication.timeSinceStartup + 2;
                while (device.RenderedOutputFrames == 0 && UnityEditor.EditorApplication.timeSinceStartup < until) yield return null;
                Assert.Greater(device.RenderedOutputFrames, 0, "Editor playback must actually pull PCM, not only mark a source as playing.");
            }
            finally { device.StopOutput(); Object.DestroyImmediate(go); }
        }
    }
}
