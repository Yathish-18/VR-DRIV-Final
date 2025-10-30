using System.Collections.Generic;
using UnityEngine;

namespace Gley.UrbanSystem.Editor
{
    public static class RoutesColorUtility
    {
        /// <summary>
        /// Syncs the given RoutesColors object with the provided list of identifiers (e.g., speeds, priorities),
        /// ensuring 1:1 matching in sorted order, removing unused entries, and adding missing ones with defaults.
        /// </summary>
        /// <param name="ids">Current list of active IDs (e.g., maxSpeed or priority)</param>
        /// <param name="routes">RoutesColors instance to modify</param>
        public static void SyncRoutesColors(List<int> ids, RoutesColors routes)
        {
            // Sanity check
            if (routes == null)
                return;

            // Temporary filtered lists
            List<int> filteredIds = new();
            List<Color> filteredColors = new();
            List<bool> filteredActive = new();

            // Keep only items that still exist in new ids list
            for (int i = 0; i < routes.Id.Count; i++)
            {
                if (ids.Contains(routes.Id[i]))
                {
                    filteredIds.Add(routes.Id[i]);
                    filteredColors.Add(routes.RoutesColor[i]);
                    filteredActive.Add(routes.Active[i]);
                }
            }

            // Add missing ids
            foreach (int id in ids)
            {
                if (!filteredIds.Contains(id))
                {
                    filteredIds.Add(id);
                    filteredColors.Add(Color.white);
                    filteredActive.Add(true);
                }
            }

            // Sort all together by id
            List<(int id, Color color, bool active)> sorted = new();
            for (int i = 0; i < filteredIds.Count; i++)
            {
                sorted.Add((filteredIds[i], filteredColors[i], filteredActive[i]));
            }
            sorted.Sort((a, b) => a.id.CompareTo(b.id));

            // Reassign the sorted lists
            routes.Id.Clear();
            routes.RoutesColor.Clear();
            routes.Active.Clear();

            foreach (var item in sorted)
            {
                routes.Id.Add(item.id);
                routes.RoutesColor.Add(item.color);
                routes.Active.Add(item.active);
            }
        }
    }
}
