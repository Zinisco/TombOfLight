using UnityEngine;

public interface IInteractable
{
    void Interact(GameObject player);
    void Highlight();
    void Unhighlight();
}
