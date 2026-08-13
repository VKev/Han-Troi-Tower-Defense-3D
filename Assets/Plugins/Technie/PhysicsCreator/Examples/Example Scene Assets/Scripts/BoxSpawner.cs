using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Technie.PhysicsCreator.Example
{
	public class BoxSpawner : MonoBehaviour
	{
		public float spawnRadius = 1.0f;

		public Vector2 spawnDelaySecs = new Vector2(0.1f, 0.2f);
		public Vector2 spawnSize = new Vector2(0.1f, 0.3f);

		public int maxSpawnedObjects = 32;

		private float nextSpawnTime = 0f;

		private List<GameObject> spawnedObjects = new List<GameObject>();

		private void Start()
		{

		}
		private void Update()
		{
			if (Time.time > nextSpawnTime)
			{
				float x = Random.Range(-spawnRadius, spawnRadius);
				float z = Random.Range(-spawnRadius, spawnRadius);
				float size = Random.Range(spawnSize.x, spawnSize.y);
				float angleX = Random.Range(0f, 360f);
				float angleY = Random.Range(0f, 360f);
				float angleZ = Random.Range(0f, 360f);

				PrimitiveType prim = Random.Range(0, 2) == 0 ? PrimitiveType.Sphere : PrimitiveType.Cube;

				GameObject obj = GameObject.CreatePrimitive(prim);
				nextSpawnTime = Time.time + Random.Range(spawnDelaySecs.x, spawnDelaySecs.y);

				obj.transform.SetParent(this.transform, false);
				obj.transform.localPosition = new Vector3(x, 0, z);
				obj.transform.localRotation = Quaternion.Euler(angleX, angleY, angleZ);
				obj.transform.localScale = new Vector3(size, size, size);

				obj.AddComponent<Rigidbody>();

				spawnedObjects.Add(obj);
			}

			for (int i = spawnedObjects.Count - 1; i >= 0; i--)
			{
				if (spawnedObjects[i].transform.position.y < -1.0f)
				{
					GameObject.Destroy(spawnedObjects[i]);
					spawnedObjects.RemoveAt(i);
				}
			}

			if (spawnedObjects.Count > maxSpawnedObjects)
			{
				GameObject.Destroy(spawnedObjects[0]);
				spawnedObjects.RemoveAt(0);
			}
		}

		private void OnDrawGizmosSelected()
		{
			Gizmos.color = Color.white;
			Gizmos.matrix = this.transform.localToWorldMatrix;
			Gizmos.DrawWireCube(Vector3.zero, new Vector3(spawnRadius * 2, 0f, spawnRadius * 2));
		}
	}

} // namespace Technie.PhysicsCreator.Example
