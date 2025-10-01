using UnityEngine;

public class OutlineController : MonoBehaviour
{
    [SerializeField] private Renderer outlineRenderer; // assign the outline mesh

    public void EnableOutline()
    {
        if (outlineRenderer != null)
            outlineRenderer.enabled = true;
    }

    public void DisableOutline()
    {
        if (outlineRenderer != null)
            outlineRenderer.enabled = false;
    }
}
