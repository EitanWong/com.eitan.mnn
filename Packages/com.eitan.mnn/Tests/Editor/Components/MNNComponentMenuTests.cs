using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MNN.Unity.Tests
{
    public sealed class MNNComponentMenuTests
    {
        private GameObject _gameObject;
        private UnityEngine.Object[] _previousSelection;

        [SetUp]
        public void SetUp()
        {
            _previousSelection = Selection.objects;
            _gameObject = new GameObject("MNN Component Menu Test");
            Selection.objects = new UnityEngine.Object[] { _gameObject };
        }

        [TearDown]
        public void TearDown()
        {
            Selection.objects = _previousSelection;
            if (_gameObject != null)
                UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [TestCase(typeof(MNNInferenceComponent), "MNN/Inference/Inference Component")]
        [TestCase(typeof(MNNModelLoader), "MNN/Models/Model Loader")]
        [TestCase(typeof(MNNModelManager), "MNN/Models/Model Manager")]
        [TestCase(typeof(MNNRealtimeAudioDevice), "MNN/Realtime/Audio Device")]
        [TestCase(typeof(MNNRealtimeSessionComponent), "MNN/Realtime/Session")]
        [TestCase(typeof(MNNUnityMicrophoneCapture), "MNN/Realtime/Unity Microphone Capture")]
        public void RuntimeComponentHasMnnRootCategoryAndMatchingHierarchyCommand(Type componentType, string category)
        {
            var attribute = (AddComponentMenu)Attribute.GetCustomAttribute(componentType, typeof(AddComponentMenu));
            Assert.NotNull(attribute, componentType.Name);
            Assert.AreEqual(category, attribute.componentMenu);
            Assert.IsTrue(category.StartsWith("MNN/", StringComparison.Ordinal));
            Assert.IsTrue(typeof(Component).IsAssignableFrom(componentType));

            string menuPath = "GameObject/" + category;
            bool hasCommand = typeof(MNN.Unity.Editor.MNNComponentMenu)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SelectMany(method => Attribute.GetCustomAttributes(method, typeof(MenuItem)).OfType<MenuItem>())
                .Any(item => item.menuItem == menuPath && !item.validate);
            Assert.IsTrue(hasCommand, menuPath);
        }

        [TestCase(typeof(MNNInferenceComponent), "AddInferenceComponent")]
        [TestCase(typeof(MNNModelLoader), "AddModelLoader")]
        [TestCase(typeof(MNNModelManager), "AddModelManager")]
        [TestCase(typeof(MNNRealtimeAudioDevice), "AddRealtimeAudioDevice")]
        [TestCase(typeof(MNNRealtimeSessionComponent), "AddRealtimeSession")]
        [TestCase(typeof(MNNUnityMicrophoneCapture), "AddUnityMicrophoneCapture")]
        public void HierarchyCommandAddsItsMappedComponent(Type componentType, string methodName)
        {
            InvokeMenuCommand(methodName);
            Assert.NotNull(_gameObject.GetComponent(componentType), methodName);
        }

        [Test]
        public void HierarchyCommandAddsSelectedComponentOnceAndCanBeUndone()
        {
            InvokeMenuCommand("AddRealtimeSession");
            Assert.NotNull(_gameObject.GetComponent<MNNRealtimeSessionComponent>());
            InvokeMenuCommand("AddRealtimeSession");
            Assert.AreEqual(1, _gameObject.GetComponents<MNNRealtimeSessionComponent>().Length);

            Undo.PerformUndo();
            Assert.IsNull(_gameObject.GetComponent<MNNRealtimeSessionComponent>());
        }

        [Test]
        public void EveryConcreteRuntimeBehaviourHasOneDistinctMenuAndCommand()
        {
            var components = typeof(MNNModelLoader).Assembly.GetTypes()
                .Where(type => !type.IsAbstract && typeof(MonoBehaviour).IsAssignableFrom(type)).ToArray();
            var categories = components.Select(type =>
            {
                var attribute = type.GetCustomAttribute<AddComponentMenu>();
                Assert.NotNull(attribute, type.FullName);
                Assert.That(attribute.componentMenu, Does.StartWith("MNN/"), type.FullName);
                return attribute.componentMenu;
            }).ToArray();
            Assert.AreEqual(categories.Length, categories.Distinct().Count(), "Component menu paths must be unique.");

            var commands = typeof(MNN.Unity.Editor.MNNComponentMenu)
                .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
                .SelectMany(method => method.GetCustomAttributes<MenuItem>())
                .Where(item => !item.validate).ToArray();
            CollectionAssert.AreEquivalent(categories.Select(path => "GameObject/" + path), commands.Select(item => item.menuItem));
            Assert.That(commands.All(item => item.priority == 10), "GameObject commands must propagate to Hierarchy creation menus.");
        }

        [Test]
        public void RegisteredGameObjectMenuExecutes()
        {
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/MNN/Realtime/Session"));
            Assert.NotNull(_gameObject.GetComponent<MNNRealtimeSessionComponent>());
        }

        [TestCase("MNN.Examples.MNNBasicExample", "MNN/Examples/Basic Status Example")]
        [TestCase("MNN.Examples.MNNInferenceExample", "MNN/Examples/Inference Example")]
        [TestCase("MNN.Examples.MNNUnifiedExample", "MNN/Examples/Unified Inference Example")]
        [TestCase("SiriAIRenderer", "MNN/Examples/Siri.AI/Renderer")]
        public void ImportedSampleHasMatchingComponentAndHierarchyMenus(string typeName, string category)
        {
            var type = TypeCache.GetTypesDerivedFrom<MonoBehaviour>().FirstOrDefault(candidate => candidate.FullName == typeName);
            if (type == null)
                Assert.Ignore("This sample has not been imported into this project.");
            Assert.AreEqual(category, type.GetCustomAttribute<AddComponentMenu>()?.componentMenu);
            Assert.IsTrue(EditorApplication.ExecuteMenuItem("GameObject/" + category));
            if (typeName == "SiriAIRenderer")
            {
                Assert.IsNull(_gameObject.GetComponent(type));
                Assert.NotNull(_gameObject.transform.Find("Visuals/Renderer")?.GetComponent(type));
            }
            else
            {
                Assert.NotNull(_gameObject.GetComponent(type));
            }
        }

        [Test]
        public void EmptySelectionCreatesObjectAndUndoRemovesIt()
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            GameObject created = null;
            try
            {
                InvokeMenuCommand("AddRealtimeSession");
                created = Selection.activeGameObject;
                Assert.NotNull(created);
                Assert.IsTrue(created.scene.IsValid());
                Assert.NotNull(created.GetComponent<MNNRealtimeSessionComponent>());
                Undo.PerformUndo();
                Assert.IsTrue(created == null, "Creation and component addition must undo together.");
            }
            finally
            {
                if (created != null)
                    UnityEngine.Object.DestroyImmediate(created);
            }
        }

        [Test]
        public void MultiSelectionAddsOnlyMissingComponentsAndUndoesTogether()
        {
            var second = new GameObject("Second MNN Target");
            try
            {
                _gameObject.AddComponent<MNNRealtimeSessionComponent>();
                Selection.objects = new UnityEngine.Object[] { _gameObject, second };
                InvokeMenuCommand("AddRealtimeSession");
                Assert.AreEqual(1, _gameObject.GetComponents<MNNRealtimeSessionComponent>().Length);
                Assert.NotNull(second.GetComponent<MNNRealtimeSessionComponent>());
                Undo.PerformUndo();
                Assert.NotNull(_gameObject.GetComponent<MNNRealtimeSessionComponent>());
                Assert.IsNull(second.GetComponent<MNNRealtimeSessionComponent>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(second);
            }
        }

        [Test]
        public void UnselectedContextTargetsOnlyContext()
        {
            var context = new GameObject("Context MNN Target");
            try
            {
                InvokeMenuCommand("AddRealtimeSession", new MenuCommand(context));
                Assert.NotNull(context.GetComponent<MNNRealtimeSessionComponent>());
                Assert.IsNull(_gameObject.GetComponent<MNNRealtimeSessionComponent>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void MultiSelectionSkipsNotEditableObjects()
        {
            var readOnly = new GameObject("Read Only MNN Target") { hideFlags = HideFlags.NotEditable };
            try
            {
                Selection.objects = new UnityEngine.Object[] { _gameObject, readOnly };
                InvokeMenuCommand("AddRealtimeSession");
                Assert.NotNull(_gameObject.GetComponent<MNNRealtimeSessionComponent>());
                Assert.IsNull(readOnly.GetComponent<MNNRealtimeSessionComponent>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readOnly);
            }
        }

        private static void InvokeMenuCommand(string methodName, MenuCommand command = null)
        {
            var method = typeof(MNN.Unity.Editor.MNNComponentMenu).GetMethod(methodName,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method, methodName);
            method.Invoke(null, new object[] { command ?? new MenuCommand(null) });
        }
    }
}
