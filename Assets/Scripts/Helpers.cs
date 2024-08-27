using UnityEngine;

public class Helpers
{
    static public void SetRendererLayerRecursive(GameObject root, int layer)
    {
        Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);

        for(int i = 0; i < rends.Length; ++i)
        {
            rends[i].gameObject.layer = layer;
        }
    }
}
