using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GameFlow.Tests.EditMode
{
    public sealed class LevelLifecycleSourceTests
    {
        private const string PlacementControllerTypeName =
            "TowerDefense3D.GridPlacement.GridPlacementController, "
            + "TowerDefense3D.GridPlacement.Runtime";
        private const string BoardDefinitionTypeName =
            "TowerDefense3D.GridPlacement.BoardDefinition, "
            + "TowerDefense3D.GridPlacement.Runtime";
        private const string PlacementControllerSourcePath =
            "Assets/Scripts/Placement/Scripts/GridPlacementController.cs";

        [Test]
        public void PlacementController_SourceHasNoSelfStartAndGatesUpdate()
        {
            MonoScript sourceAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(
                PlacementControllerSourcePath);

            Assert.That(sourceAsset, Is.Not.Null);
            string source = sourceAsset.text.Replace("\r\n", "\n");
            StringAssert.DoesNotContain("private void Awake()", source);
            StringAssert.DoesNotContain("private void Start()", source);
            StringAssert.Contains(
                "private void Update()\n        {\n            if (!IsInitialized)",
                source);
        }

        [Test]
        public void PlacementController_UsesExplicitIdempotentLifecycleAndSupportsReentry()
        {
            GameObject owner = new GameObject("Placement Lifecycle Test");
            Component controller = CreateConfiguredController(owner, out ScriptableObject boardDefinition);

            try
            {
                Assert.That(GetDeclaredLifecycleMethod(controller, "Awake"), Is.Null);
                Assert.That(GetDeclaredLifecycleMethod(controller, "Start"), Is.Null);
                Assert.That(GetIsInitialized(controller), Is.False);
                Assert.That(GetOccupancy(controller), Is.Null);

                Invoke(controller, "Initialize");
                object firstOccupancy = GetOccupancy(controller);
                Assert.That(GetIsInitialized(controller), Is.True);
                Assert.That(firstOccupancy, Is.Not.Null);

                Invoke(controller, "Initialize");
                Assert.That(GetOccupancy(controller), Is.SameAs(firstOccupancy));

                Invoke(controller, "Shutdown");
                Invoke(controller, "Shutdown");
                Assert.That(GetIsInitialized(controller), Is.False);
                Assert.That(GetOccupancy(controller), Is.Null);

                Invoke(controller, "Initialize");
                Assert.That(GetIsInitialized(controller), Is.True);
                object secondOccupancy = GetOccupancy(controller);
                Assert.That(secondOccupancy, Is.Not.Null);
                Assert.That(secondOccupancy, Is.Not.SameAs(firstOccupancy));
            }
            finally
            {
                Invoke(controller, "Shutdown");
                UnityEngine.Object.DestroyImmediate(boardDefinition);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlacementSceneAdapter_DelegatesInitializeShutdownAndReentry()
        {
            GameObject owner = new GameObject("Placement Adapter Test");
            Component controller = CreateConfiguredController(owner, out ScriptableObject boardDefinition);
            GridPlacementSceneAdapter adapter = owner.AddComponent<GridPlacementSceneAdapter>();
            SetPrivateField(adapter, "placementController", controller);
            var runtimeContext = new LevelSceneRuntimeContext(1, () => { });

            try
            {
                adapter.Initialize(runtimeContext);
                object firstOccupancy = GetOccupancy(controller);
                Assert.That(GetIsInitialized(controller), Is.True);

                adapter.Initialize(runtimeContext);
                Assert.That(GetOccupancy(controller), Is.SameAs(firstOccupancy));

                adapter.Shutdown();
                adapter.Shutdown();
                Assert.That(GetIsInitialized(controller), Is.False);
                Assert.That(GetOccupancy(controller), Is.Null);

                adapter.Initialize(runtimeContext);
                Assert.That(GetIsInitialized(controller), Is.True);
            }
            finally
            {
                adapter.Shutdown();
                UnityEngine.Object.DestroyImmediate(boardDefinition);
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PlacementSceneAdapter_MissingControllerFailsClearly()
        {
            GameObject owner = new GameObject("Missing Placement Controller Test");
            GridPlacementSceneAdapter adapter = owner.AddComponent<GridPlacementSceneAdapter>();

            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => adapter.Initialize(new LevelSceneRuntimeContext(1, () => { })));

                StringAssert.Contains("requires a GridPlacementController", exception.Message);
                adapter.Shutdown();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        private static Component CreateConfiguredController(
            GameObject owner,
            out ScriptableObject boardDefinition)
        {
            Type controllerType = Type.GetType(PlacementControllerTypeName, true);
            Type definitionType = Type.GetType(BoardDefinitionTypeName, true);
            Component controller = owner.AddComponent(controllerType);
            boardDefinition = ScriptableObject.CreateInstance(definitionType);
            SetPrivateField(controller, "boardDefinition", boardDefinition);
            return controller;
        }

        private static MethodInfo GetDeclaredLifecycleMethod(Component controller, string methodName)
        {
            return controller.GetType().GetMethod(
                methodName,
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic
                | BindingFlags.DeclaredOnly);
        }

        private static bool GetIsInitialized(Component controller)
        {
            PropertyInfo property = controller.GetType().GetProperty("IsInitialized");
            Assert.That(property, Is.Not.Null);
            return (bool)property.GetValue(controller);
        }

        private static object GetOccupancy(Component controller)
        {
            PropertyInfo property = controller.GetType().GetProperty("Occupancy");
            Assert.That(property, Is.Not.Null);
            return property.GetValue(controller);
        }

        private static void Invoke(Component target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(method, Is.Not.Null, "Missing public method " + methodName);
            method.Invoke(target, null);
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, "Missing private field " + fieldName);
            field.SetValue(target, value);
        }
    }
}
