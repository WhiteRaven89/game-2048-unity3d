using UnityEngine;

namespace mrathod.Utility
{
    public static class UnityUtils
    {
        public static void RemoveChildrens(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                GameObject.Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
