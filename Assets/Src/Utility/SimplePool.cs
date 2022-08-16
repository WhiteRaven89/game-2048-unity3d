using UnityEngine;

namespace Arma.Utility
{
    /// <summary>
    /// Wrapper to create simple object pool
    /// </summary>
    public class SimplePool
    {
        private GameObject[] objectStore;
        private bool[] availableObjects;
        private int nextFreeFromPool = 0;

        public SimplePool(GameObject prefab, int poolSize)
        {
            this.Init(prefab, poolSize, null);
        }

        public SimplePool(GameObject prefab, int poolSize, GameObject parent)
        {
            this.Init(prefab, poolSize, parent);
        }

        void Init(GameObject prefab, int poolSize, GameObject parent)
        {
            if (prefab == null)
            {
                Debug.LogError(":: SimplePool :: Prefab ref is null, Not creating pool");
                return;
            }

            objectStore = new GameObject[poolSize];
            availableObjects = new bool[poolSize];

            for (int i = 0; i < poolSize; i++)
            {
                // Create a new instance and set ourself as the recycleBin
                GameObject newTransform = Object.Instantiate(prefab, prefab.transform.position, prefab.transform.rotation) as GameObject;
                newTransform.name = prefab.name + "_" + i;
                newTransform.SetActive(false);

                // Add it to our objectStore and set it to available
                objectStore.SetValue(newTransform, i);
                availableObjects[i] = true;

                if (parent)
                {
                    newTransform.transform.parent = parent.transform;
                }
            }
        }

        // Gets the next available free object from pool
        public GameObject Spawn
        {
            get
            {
                for (; nextFreeFromPool < availableObjects.Length; nextFreeFromPool++)
                {
                    if (availableObjects[nextFreeFromPool])
                    {
                        // Set the object to unavailable and return it
                        availableObjects[nextFreeFromPool] = false;
                        return objectStore.GetValue(nextFreeFromPool) as GameObject;
                    }
                }
                return null;
            }
        }


        // Return an object to the inactive pool.
        public void Despawn(GameObject objectToFree)
        {
            int index = System.Array.IndexOf(objectStore, objectToFree);
            if (index >= 0)
            {
                // Reset the nextFreeLoopStart if this object has a lower index
                if (index < nextFreeFromPool)
                    nextFreeFromPool = index;

                //  Reset the position
                objectToFree.transform.position = Vector3.zero;

                // Make the object inactive
                objectToFree.SetActive(false);

                // Set the object to available
                availableObjects[index] = true;
            }
        }
    }
}
