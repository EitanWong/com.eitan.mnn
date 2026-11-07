using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Tests
{
    internal static class StudioWindowFixture
    {
        // State-injection tests supply their own models. Cancel the real startup request
        // first so it cannot race with those fixtures; startup tests use real creation.
        internal static T Create<T>() where T : EditorWindow
        {
            var window = ScriptableObject.CreateInstance<T>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            object discovery = typeof(T).GetField("_modelDiscovery", flags)?.GetValue(window);
            discovery?.GetType().GetMethod("Cancel", flags).Invoke(discovery, null);
            return window;
        }
    }
}
