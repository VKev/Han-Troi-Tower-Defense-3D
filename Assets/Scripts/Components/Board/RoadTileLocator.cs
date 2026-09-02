using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerDefense3D.GridPlacement
{
    /// <summary>
    /// Finds the road tiles standing on the board.
    ///
    /// Most boards stop marking cells as road before the road tiles stop being laid - a route is
    /// drawn a cell or two inside the board while the tiles carry on running off the edge - so the
    /// tiles are the only record of how far the road actually goes. Anything that needs to follow
    /// road past the last painted cell has to ask them.
    ///
    /// This lives beside the tiles rather than with the road pathing that wants it: pathing is in
    /// the system layer, which cannot see scene components, so it takes the answer as plain points.
    /// </summary>
    public static class RoadTileLocator
    {
        /// <summary>The authored display name every road tile carries.</summary>
        private const string RoadTileDisplayName = "Road";

        /// <summary>
        /// Every road tile on the board, as world positions.
        ///
        /// Straight tiles are found by their authoring component. Corners and junctions are not:
        /// the board generator swaps a corner, T-junction or crossroads mesh in for the straight
        /// tile, and those variants carry no authoring component at all. They are picked up by name
        /// from the same container - miss them and the road appears to break at every bend, which
        /// is precisely where it turns off the board.
        /// </summary>
        public static List<Vector3> CollectWorldPositions()
        {
            var positions = new List<Vector3>();
            var containers = new HashSet<Transform>();

            // Inactive tiles count too: a board with its visualisation switched off still has its
            // road laid out, and the road has to read the same either way.
            GridPlaceableAuthoring[] placeables = UnityEngine.Object.FindObjectsByType<GridPlaceableAuthoring>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int index = 0; index < placeables.Length; index++)
            {
                if (!IsRoadName(placeables[index].DisplayName))
                {
                    continue;
                }

                positions.Add(placeables[index].transform.position);
                if (placeables[index].transform.parent != null)
                {
                    containers.Add(placeables[index].transform.parent);
                }
            }

            foreach (Transform container in containers)
            {
                for (int index = 0; index < container.childCount; index++)
                {
                    Transform child = container.GetChild(index);
                    if (child.GetComponent<GridPlaceableAuthoring>() == null && IsRoadName(child.name))
                    {
                        positions.Add(child.position);
                    }
                }
            }

            return positions;
        }

        /// <summary>
        /// A generated tile is named after the placeable it came from, so both "Road" and
        /// "Road Cell (3, 0, 4)" belong to the road.
        /// </summary>
        private static bool IsRoadName(string name)
        {
            return name != null
                && name.StartsWith(RoadTileDisplayName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
