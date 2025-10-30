// ---------------------------------------------------
// Copyright (c) Pampel Games e.K. All Rights Reserved.
// https://www.pampelgames.com
// ---------------------------------------------------

using UnityEngine;

namespace PampelGames.Shared.Tools
{
    /// <summary>
    ///     Attached to all <see cref="PGPool" /> spawned objects in the scene.
    /// </summary>
    public class PGPoolable : MonoBehaviour
    {
        public GameObject prefab { get; set; }

        /* Public ***********************************************************************************************************************/

        /// <summary>
        ///     Despawn this GameObject into the pool. Optionally call PGPool.Release(obj) directly.
        /// </summary>
        public void Release()
        {
            PGPool.Release(gameObject);
        }

        /********************************************************************************************************************************/

        internal virtual void OnPoolSpawn()
        {
        }

        internal virtual void OnPoolUnSpawn()
        {
        }

        /// <summary>
        ///     Object is currently inside a pool.
        /// </summary>
        internal bool pooled = true;


    }
}