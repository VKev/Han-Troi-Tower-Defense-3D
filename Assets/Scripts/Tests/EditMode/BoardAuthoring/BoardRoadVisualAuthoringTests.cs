using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerDefense3D.GridPlacement.Editor;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.GridPlacement.Tests.EditMode
{
    public sealed class BoardGridPlaceableAuthoringTests
    {
        private const string PrefabPath =
            "Assets/Resources/Prefabs/RoadStraightCell.prefab";

        private readonly List<UnityEngine.Object> created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            Undo.ClearAll();
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(created[index]);
                }
            }

            created.Clear();
        }

        [Test]
        public void Planner_UsesOnlyPrefabLayerAndIgnoresRoadRoles()
        {
            GameObject prefab = LoadPrefab();
            BoardDefinition board = CreateBoard(
                new GridDimensions(3, 1, 1),
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.RoadSpawn),
                    Cell(1, 0, 0, BoardCellFlags.Road),
                    Cell(2, 0, 0, BoardCellFlags.RoadEnd),
                },
                new[] { Placement(1, 0, 0, prefab) });

            BoardGeometryPlan plan = BoardGeometryPlanner.Create(board);

            Assert.That(plan.GridPlaceableVisuals, Has.Count.EqualTo(1));
            Assert.That(
                plan.GridPlaceableVisuals[0].Coordinate,
                Is.EqualTo(new GridCell(1, 0, 0)));
            Assert.That(plan.GridPlaceableVisuals[0].Prefab, Is.SameAs(prefab));
        }

        [Test]
        public void Planner_InfersStraightAxesAndUsesCornerVariant()
        {
            GameObject prefab = LoadPrefab();
            GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();
            BoardDefinition board = CreateBoard(
                new GridDimensions(6, 5, 1),
                Array.Empty<BoardCellDefinition>(),
                new[]
                {
                    Placement(0, 0, 0, prefab),
                    Placement(1, 0, 0, prefab),
                    Placement(2, 0, 0, prefab),
                    Placement(5, 0, 0, prefab),
                    Placement(5, 1, 0, prefab),
                    Placement(5, 2, 0, prefab),
                    Placement(1, 3, 0, prefab),
                    Placement(0, 3, 0, prefab),
                    Placement(1, 4, 0, prefab),
                });

            BoardGeometryPlan plan = BoardGeometryPlanner.Create(board);

            Assert.That(plan.GridPlaceableVisuals, Has.Count.EqualTo(9));
            Assert.That(
                Find(plan.GridPlaceableVisuals, 1, 0, 0).Axis,
                Is.EqualTo(GridPlaceableAxis.X));
            Assert.That(
                Find(plan.GridPlaceableVisuals, 5, 1, 0).Axis,
                Is.EqualTo(GridPlaceableAxis.Z));
            Assert.That(
                Find(plan.GridPlaceableVisuals, 1, 3, 0).Topology,
                Is.EqualTo(GridPlaceableTopology.Corner));
            Assert.That(
                Find(plan.GridPlaceableVisuals, 1, 3, 0).Prefab,
                Is.SameAs(placeable.CornerPrefab));
            Assert.That(
                Find(plan.GridPlaceableVisuals, 1, 4, 0).Axis,
                Is.EqualTo(GridPlaceableAxis.Z));
        }

        [Test]
        public void Planner_ResolvesEveryCornerAndJunctionRotation()
        {
            GameObject prefab = LoadPrefab();
            GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();

            AssertTopology(
                prefab,
                GridPlaceableTopology.Corner,
                placeable.CornerPrefab,
                0f,
                Vector2Int.right,
                Vector2Int.up);
            AssertTopology(
                prefab,
                GridPlaceableTopology.Corner,
                placeable.CornerPrefab,
                90f,
                Vector2Int.right,
                Vector2Int.down);
            AssertTopology(
                prefab,
                GridPlaceableTopology.Corner,
                placeable.CornerPrefab,
                180f,
                Vector2Int.left,
                Vector2Int.down);
            AssertTopology(
                prefab,
                GridPlaceableTopology.Corner,
                placeable.CornerPrefab,
                270f,
                Vector2Int.left,
                Vector2Int.up);

            AssertTopology(
                prefab,
                GridPlaceableTopology.ThreeWay,
                placeable.ThreeWayPrefab,
                0f,
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.up);
            AssertTopology(
                prefab,
                GridPlaceableTopology.ThreeWay,
                placeable.ThreeWayPrefab,
                90f,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down);
            AssertTopology(
                prefab,
                GridPlaceableTopology.ThreeWay,
                placeable.ThreeWayPrefab,
                180f,
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.down);
            AssertTopology(
                prefab,
                GridPlaceableTopology.ThreeWay,
                placeable.ThreeWayPrefab,
                270f,
                Vector2Int.left,
                Vector2Int.up,
                Vector2Int.down);

            AssertTopology(
                prefab,
                GridPlaceableTopology.FourWay,
                placeable.FourWayPrefab,
                0f,
                Vector2Int.left,
                Vector2Int.right,
                Vector2Int.up,
                Vector2Int.down);
        }

        [Test]
        public void TopologyPrefabs_ReuseOriginalMaterialWithoutPaintMarkers()
        {
            GameObject prefab = LoadPrefab();
            GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();
            Material material = prefab
                .GetComponentInChildren<Renderer>(true)
                .sharedMaterial;

            AssertVariantPrefab(placeable.CornerPrefab, material, 27);
            AssertVariantPrefab(placeable.ThreeWayPrefab, material, 6);
            AssertVariantPrefab(placeable.FourWayPrefab, material, 4);
        }

        [Test]
        public void CornerPrefab_FillsInsideBendAndMatchesStraightFadeWidth()
        {
            GameObject prefab = LoadPrefab();
            GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();
            Mesh mesh = placeable.CornerPrefab
                .GetComponent<MeshFilter>()
                .sharedMesh;

            Assert.That(mesh.bounds.min.x, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(mesh.bounds.min.z, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(mesh.bounds.max.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(mesh.bounds.max.z, Is.EqualTo(0.5f).Within(0.001f));

            Vector3 bendCenter = new Vector3(0.5f, -0.059f, 0.5f);
            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            bool foundCenter = false;
            for (int index = 0; index < vertices.Length; index++)
            {
                float distance = Vector2.Distance(
                    new Vector2(vertices[index].x, vertices[index].z),
                    new Vector2(bendCenter.x, bendCenter.z));
                if (distance < 0.5f - 0.001f)
                {
                    Assert.That(
                        uv[index].y,
                        Is.LessThanOrEqualTo(0.5f),
                        $"Inside-bend vertex {index} must sample the opaque half of the texture.");
                }

                if (Vector3.Distance(vertices[index], bendCenter) < 0.001f)
                {
                    foundCenter = true;
                    Assert.That(uv[index].y, Is.EqualTo(0.5f).Within(0.001f));
                }
            }

            Assert.That(foundCenter, Is.True, "The bend must contain a solid center vertex.");
        }

        [Test]
        public void TJunctionPrefab_CoversThroughRoadAndFadesOnlyMissingSide()
        {
            GameObject prefab = LoadPrefab();
            GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();
            Mesh mesh = placeable.ThreeWayPrefab
                .GetComponent<MeshFilter>()
                .sharedMesh;

            Assert.That(mesh.bounds.min.x, Is.EqualTo(-0.5f).Within(0.001f));
            Assert.That(mesh.bounds.min.z, Is.EqualTo(-1f).Within(0.001f));
            Assert.That(mesh.bounds.max.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(mesh.bounds.max.z, Is.EqualTo(0.5f).Within(0.001f));

            Vector3[] vertices = mesh.vertices;
            Vector2[] uv = mesh.uv;
            for (int index = 0; index < vertices.Length; index++)
            {
                if (vertices[index].z <= -1f + 0.001f)
                {
                    Assert.That(
                        uv[index].y,
                        Is.EqualTo(1f).Within(0.001f),
                        "Only the missing-branch outer edge should sample transparency.");
                }
                else
                {
                    Assert.That(
                        uv[index].y,
                        Is.InRange(0.05f - 0.001f, 0.55f + 0.001f),
                        "The through-road center and connected side must stay opaque.");
                }
            }
        }

        [Test]
        public void Synchronizer_RendersBoardBeforeAlwaysVisiblePrefabArt()
        {
            GameObject prefab = LoadPrefab();
            GridPlaceableAuthoring placeable = prefab.GetComponent<GridPlaceableAuthoring>();
            Renderer[] prefabRenderers =
                prefab.GetComponentsInChildren<Renderer>(true);
            BoardDefinition board = CreateBoard(
                new GridDimensions(3, 1, 1),
                new[]
                {
                    Cell(0, 0, 0, BoardCellFlags.RoadSpawn),
                    Cell(1, 0, 0, BoardCellFlags.Road),
                    Cell(2, 0, 0, BoardCellFlags.RoadEnd),
                },
                new[] { Placement(1, 0, 0, prefab) },
                visualize: false);
            BoardView presenter = CreatePresenter(board);

            BoardSceneSynchronizer.Synchronize(board);

            Transform boardRoot = presenter.GeneratedRoot;
            Transform prefabRoot = presenter.GeneratedGridPlaceableRoot;
            Assert.That(boardRoot, Is.Not.Null);
            Assert.That(
                boardRoot.GetComponentsInChildren<Renderer>(true),
                Has.All.Matches<Renderer>(renderer =>
                    !renderer.enabled
                    && renderer.sortingOrder
                        == BoardSceneSynchronizer.BoardVisualizationSortingOrder));
            Assert.That(prefabRoot, Is.Not.Null);
            Assert.That(
                prefabRoot.name,
                Is.EqualTo(
                    BoardSceneSynchronizer.GeneratedGridPlaceableRootName));
            Assert.That(prefabRoot.childCount, Is.EqualTo(1));
            AssertGridPlaceableContract(
                prefabRoot.GetChild(0),
                prefab,
                prefabRenderers,
                placeable.RendererSortingOrder);
            Assert.That(
                placeable.RendererSortingOrder,
                Is.GreaterThan(
                    BoardSceneSynchronizer.BoardVisualizationSortingOrder));

            Transform generatedInstance = prefabRoot.GetChild(0);
            int instanceId = generatedInstance.gameObject.GetInstanceID();
            Transform[] generatedHierarchy =
                generatedInstance.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < generatedHierarchy.Length; index++)
            {
                generatedHierarchy[index].gameObject.isStatic = false;
            }

            Assert.That(
                generatedHierarchy,
                Has.All.Matches<Transform>(item => !item.gameObject.isStatic));

            BoardSceneSynchronizer.Synchronize(board);
            Assert.That(
                prefabRoot.GetChild(0).gameObject.GetInstanceID(),
                Is.EqualTo(instanceId),
                "Repairing static flags must not rebuild prefab instances.");
            AssertStaticHierarchy(prefabRoot.GetChild(0));

            SetField(board, "visualizeInScene", true);
            BoardSceneSynchronizer.Synchronize(board);
            Assert.That(
                boardRoot.GetComponentsInChildren<Renderer>(true),
                Has.All.Matches<Renderer>(renderer => renderer.enabled));
            Assert.That(
                prefabRoot.GetChild(0).gameObject.GetInstanceID(),
                Is.EqualTo(instanceId),
                "Board visibility must not rebuild prefab art.");
        }

        [Test]
        public void Synchronizer_RemovesGeneratedRootWhenPrefabLayerIsCleared()
        {
            GameObject prefab = LoadPrefab();
            BoardDefinition board = CreateBoard(
                new GridDimensions(1, 1, 1),
                new[] { Cell(0, 0, 0, BoardCellFlags.Road) },
                new[] { Placement(0, 0, 0, prefab) });
            BoardView presenter = CreatePresenter(board);

            BoardSceneSynchronizer.Synchronize(board);
            Assert.That(presenter.GeneratedGridPlaceableRoot, Is.Not.Null);

            SetField(
                board,
                "gridPlaceables",
                Array.Empty<GridPlaceablePlacement>());
            BoardSceneSynchronizer.Synchronize(board);

            Assert.That(presenter.GeneratedGridPlaceableRoot, Is.Null);
            Assert.That(
                presenter.transform.Find(
                    BoardSceneSynchronizer.GeneratedGridPlaceableRootName),
                Is.Null);
            Assert.That(
                board.Cells[0].Flags,
                Is.EqualTo(BoardCellFlags.Road),
                "Clearing visual prefabs must not clear gameplay road flags.");
        }

        [Test]
        public void PrefabBrush_ReplacesAndErasesWithoutChangingCellFlags()
        {
            GameObject prefab = LoadPrefab();
            GridCell coordinate = new GridCell(1, 1, 0);
            BoardDefinition board = CreateBoard(
                new GridDimensions(3, 3, 1),
                new[]
                {
                    new BoardCellDefinition(coordinate, BoardCellFlags.Road),
                },
                Array.Empty<GridPlaceablePlacement>());
            var document = new BoardAuthoringDocument(board);

            Assert.That(
                BoardPainterWindow.PaintGridPlaceableBrush(
                    document,
                    coordinate,
                    1,
                    prefab),
                Is.True);
            Assert.That(document.GetGridPlaceable(coordinate), Is.SameAs(prefab));
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.Road));

            document.Paint(coordinate, BoardPaintPreset.Buildable);
            Assert.That(document.GetGridPlaceable(coordinate), Is.SameAs(prefab));
            Assert.That(
                document.GetFlags(coordinate),
                Is.EqualTo(
                    BoardCellFlags.Road
                    | BoardCellFlags.SupportsPlacement
                    | BoardCellFlags.Buildable));

            document.Paint(coordinate, BoardPaintPreset.Empty);
            Assert.That(document.GetGridPlaceable(coordinate), Is.SameAs(prefab));
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.Road));

            document.Commit("Paint Prefab Test");
            Assert.That(board.GridPlaceables.Count, Is.EqualTo(1));
            Assert.That(board.Cells.Count, Is.EqualTo(1));
            Assert.That(board.Cells[0].Flags, Is.EqualTo(BoardCellFlags.Road));

            Assert.That(
                BoardPainterWindow.PaintGridPlaceableBrush(
                    document,
                    coordinate,
                    1,
                    null),
                Is.True);
            Assert.That(document.GetGridPlaceable(coordinate), Is.Null);
            Assert.That(document.GetFlags(coordinate), Is.EqualTo(BoardCellFlags.Road));
        }

        [Test]
        public void Document_RejectsObjectsWithoutRootGridPlaceable()
        {
            BoardDefinition board = CreateBoard(
                new GridDimensions(1, 1, 1),
                Array.Empty<BoardCellDefinition>(),
                Array.Empty<GridPlaceablePlacement>());
            var document = new BoardAuthoringDocument(board);
            GameObject invalid = Track(new GameObject("Invalid Placeable"));

            Assert.Throws<ArgumentException>(() =>
                document.SetGridPlaceable(new GridCell(0, 0, 0), invalid));
        }

        private BoardDefinition CreateBoard(
            GridDimensions dimensions,
            BoardCellDefinition[] cells,
            GridPlaceablePlacement[] placements,
            bool visualize = false)
        {
            BoardDefinition board =
                Track(ScriptableObject.CreateInstance<BoardDefinition>());
            SetField(board, "dimensions", dimensions);
            SetField(board, "cellSize", 1f);
            SetField(board, "heightUnit", 1f);
            SetField(board, "visualizeInScene", visualize);
            SetField(board, "cells", cells);
            SetField(board, "gridPlaceables", placements);
            return board;
        }

        private BoardView CreatePresenter(BoardDefinition board)
        {
            GameObject presenterObject =
                Track(new GameObject("Grid Placeable Test Presenter"));
            BoardView presenter =
                presenterObject.AddComponent<BoardView>();
            SetField(presenter, "board", board);
            return presenter;
        }

        private static GameObject LoadPrefab()
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<GridPlaceableAuthoring>(), Is.Not.Null);
            return prefab;
        }

        private static BoardCellDefinition Cell(
            int x,
            int z,
            int y,
            BoardCellFlags flags) =>
            new BoardCellDefinition(new GridCell(x, z, y), flags);

        private static GridPlaceablePlacement Placement(
            int x,
            int z,
            int y,
            GameObject prefab) =>
            new GridPlaceablePlacement(new GridCell(x, z, y), prefab);

        private static BoardGridPlaceableVisual Find(
            IReadOnlyList<BoardGridPlaceableVisual> visuals,
            int x,
            int z,
            int y)
        {
            GridCell coordinate = new GridCell(x, z, y);
            for (int index = 0; index < visuals.Count; index++)
            {
                if (visuals[index].Coordinate == coordinate)
                {
                    return visuals[index];
                }
            }

            Assert.Fail($"Missing visual at {coordinate}.");
            return default;
        }

        private void AssertTopology(
            GameObject prefab,
            GridPlaceableTopology expectedTopology,
            GameObject expectedPrefab,
            float expectedRotationY,
            params Vector2Int[] neighborOffsets)
        {
            var placements = new List<GridPlaceablePlacement>
            {
                Placement(1, 1, 0, prefab),
            };
            for (int index = 0; index < neighborOffsets.Length; index++)
            {
                Vector2Int offset = neighborOffsets[index];
                placements.Add(Placement(
                    1 + offset.x,
                    1 + offset.y,
                    0,
                    prefab));
            }

            BoardDefinition board = CreateBoard(
                new GridDimensions(3, 3, 1),
                Array.Empty<BoardCellDefinition>(),
                placements.ToArray());
            BoardGridPlaceableVisual visual = Find(
                BoardGeometryPlanner.Create(board).GridPlaceableVisuals,
                1,
                1,
                0);

            Assert.That(visual.Topology, Is.EqualTo(expectedTopology));
            Assert.That(visual.Prefab, Is.SameAs(expectedPrefab));
            Assert.That(
                Quaternion.Angle(
                    visual.LocalRotation,
                    Quaternion.Euler(0f, expectedRotationY, 0f)),
                Is.LessThan(0.001f));
        }

        private static void AssertVariantPrefab(
            GameObject prefab,
            Material expectedMaterial,
            int expectedVertexCount)
        {
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<GridPlaceableAuthoring>(), Is.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);

            MeshFilter filter = prefab.GetComponent<MeshFilter>();
            MeshRenderer renderer = prefab.GetComponent<MeshRenderer>();
            Assert.That(filter, Is.Not.Null);
            Assert.That(filter.sharedMesh, Is.Not.Null);
            Assert.That(filter.sharedMesh.vertexCount, Is.EqualTo(expectedVertexCount));
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.SameAs(expectedMaterial));
            Assert.That(renderer.sharedMaterial.name, Does.Not.Contain("(Instance)"));
            // The road lies flat on the ground, so it has nothing to cast onto - but it does
            // have to receive. The Progressive Lightmapper honours Receive Shadows: while this
            // was off, every road cell baked a fully unoccluded shadowmask chart, so the path
            // stayed lit even where the surrounding ground was in shadow.
            Assert.That(
                renderer.shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
            Assert.That(renderer.receiveShadows, Is.True);
            Assert.That(renderer.sortingOrder, Is.EqualTo(0));
        }

        private static void AssertGridPlaceableContract(
            Transform instance,
            GameObject prefab,
            Renderer[] prefabRenderers,
            int sortingOrder)
        {
            Assert.That(
                PrefabUtility.GetCorrespondingObjectFromOriginalSource(
                    instance.gameObject),
                Is.SameAs(prefab));
            AssertStaticHierarchy(instance);

            Renderer[] instanceRenderers =
                instance.GetComponentsInChildren<Renderer>(true);
            Assert.That(instanceRenderers, Has.Length.EqualTo(prefabRenderers.Length));
            for (int index = 0; index < instanceRenderers.Length; index++)
            {
                Renderer renderer = instanceRenderers[index];
                Assert.That(renderer.enabled, Is.EqualTo(prefabRenderers[index].enabled));
                Assert.That(renderer.sortingOrder, Is.EqualTo(sortingOrder));
                Assert.That(
                    renderer.sharedMaterials,
                    Is.EqualTo(prefabRenderers[index].sharedMaterials));
                Assert.That(
                    renderer.sharedMaterials,
                    Has.All.Matches<Material>(material =>
                        material != null
                        && !material.name.Contains("(Instance)")));
                Assert.That(
                    renderer.shadowCastingMode,
                    Is.EqualTo(prefabRenderers[index].shadowCastingMode));
                Assert.That(
                    renderer.receiveShadows,
                    Is.EqualTo(prefabRenderers[index].receiveShadows));
            }
        }

        private static void AssertStaticHierarchy(Transform root)
        {
            Transform[] hierarchy =
                root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < hierarchy.Length; index++)
            {
                Assert.That(
                    hierarchy[index].gameObject.isStatic,
                    Is.True,
                    $"{hierarchy[index].name} must be fully static.");
            }
        }

        private static void SetField(
            UnityEngine.Object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(
                field,
                Is.Not.Null,
                $"Missing field '{fieldName}' on {target.GetType().Name}.");
            field.SetValue(target, value);
        }

        private T Track<T>(T value)
            where T : UnityEngine.Object
        {
            created.Add(value);
            return value;
        }
    }
}
