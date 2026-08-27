using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerDefense3D.Enemies.Tests.EditMode
{
    public sealed class EnemyElementStatusAssetTests
    {
        private const string StatusPrefabPath =
            "Assets/Resources/Prefabs/Enemies/ElementStatus/EnemyElementStatusView.prefab";
        private const string AtlasPath =
            "Assets/Resources/Textures/ElementStatus/ElementIconsAtlas.png";
        private const string IconModelPath =
            "Assets/Resources/Models/ElementStatus/ElementStatusIcons.fbx";

        // Atlas tiles: fire top-left, water top-right, earth bottom-left, wind bottom-right.
        private static readonly (string MeshName, float UMin, float VMin)[] IconTiles =
        {
            ("FireIcon", 0f, 0.5f),
            ("WaterIcon", 0.5f, 0.5f),
            ("EarthIcon", 0f, 0f),
            ("WindIcon", 0.5f, 0f)
        };

        private static readonly string[] EnemyPrefabPaths =
        {
            "Assets/Resources/Prefabs/Enemies/BasicEnemy.prefab",
            "Assets/Resources/Prefabs/Enemies/ArmoredEnemy.prefab",
            "Assets/Resources/Prefabs/Enemies/MagicResistant 1.prefab",
            "Assets/Resources/Prefabs/Enemies/Stealth 1.prefab",
            "Assets/Resources/Prefabs/Enemies/SpeedSupportEnemy.prefab",
            "Assets/Resources/Prefabs/Enemies/MiniBossEnemy.prefab",
            "Assets/Resources/Prefabs/Enemies/SummonerBossEnemy.prefab"
        };

        [Test]
        public void StatusPrefab_UsesOneAtlasMaterialAcrossFourQuads()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StatusPrefabPath);
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

            Assert.That(renderers, Has.Length.EqualTo(4));
            var materials = new HashSet<Material>();
            for (int index = 0; index < renderers.Length; index++)
            {
                materials.Add(renderers[index].sharedMaterial);
            }

            Assert.That(materials, Has.Count.EqualTo(1));
            Assert.That(renderers[0].sharedMaterial.mainTexture, Is.SameAs(atlas));
        }

        [Test]
        public void IconMeshes_BakeTheirOwnAtlasTileUpright()
        {
            const float half = 0.5f;
            for (int index = 0; index < IconTiles.Length; index++)
            {
                (string meshName, float uMin, float vMin) = IconTiles[index];
                Mesh mesh = LoadIconMesh(meshName);
                Assert.That(mesh, Is.Not.Null, meshName);

                Vector3[] vertices = mesh.vertices;
                Vector2[] uvs = mesh.uv;
                Assert.That(vertices, Has.Length.EqualTo(4), meshName);
                Assert.That(uvs, Has.Length.EqualTo(4), meshName);

                // Unity mirrors X when importing the Blender export, so the UV bake is rotated
                // 180 degrees on purpose: local +X is the icon's right and local -Y is its top.
                // Combined with EnemyElementStatusView's billboard that renders the icon upright.
                for (int vertexIndex = 0; vertexIndex < vertices.Length; vertexIndex++)
                {
                    Vector3 vertex = vertices[vertexIndex];
                    float expectedU = vertex.x > 0f ? uMin + half : uMin;
                    float expectedV = vertex.y > 0f ? vMin : vMin + half;
                    Assert.That(uvs[vertexIndex].x, Is.EqualTo(expectedU).Within(0.0001f),
                        $"{meshName} U at vertex {vertex}");
                    Assert.That(uvs[vertexIndex].y, Is.EqualTo(expectedV).Within(0.0001f),
                        $"{meshName} V at vertex {vertex}");
                }
            }
        }

        [Test]
        public void EnemyPrefabs_AllContainAuthoredElementStatusView()
        {
            for (int index = 0; index < EnemyPrefabPaths.Length; index++)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPaths[index]);
                EnemyElementStatusView view = prefab.GetComponentInChildren<EnemyElementStatusView>(true);
                Assert.That(view, Is.Not.Null, EnemyPrefabPaths[index]);
                Assert.That(view.transform.localPosition.y, Is.GreaterThan(0f), EnemyPrefabPaths[index]);
            }
        }

        private static Mesh LoadIconMesh(string meshName)
        {
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(IconModelPath);
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is Mesh mesh && mesh.name == meshName)
                {
                    return mesh;
                }
            }

            return null;
        }
    }
}
