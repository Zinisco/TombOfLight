using UnityEngine;

public class PlayerInteractor : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameInput gameInput;
    [SerializeField] private Camera playerCamera;

    [Header("Interaction Settings")]
    [SerializeField] private float interactRange = 3f;
    [SerializeField] private LayerMask interactMask;

    private IInteractable currentTarget;

    private void Start()
    {
        if (gameInput == null)
            gameInput = GameInput.Instance;

        gameInput.OnInteractAction += HandleInteract;
    }

    private void OnDestroy()
    {
        if (gameInput != null)
            gameInput.OnInteractAction -= HandleInteract;
    }

    private void Update()
    {
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * interactRange, Color.green);

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
        {

            // Important: use GetComponentInParent so it works even if collider is on a child
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();

            if (interactable != null)
            {

                if (currentTarget != interactable)
                {
                    ClearHighlight();
                    currentTarget = interactable;
                    currentTarget.Highlight();
                }
                return;
            }
            else
            {
                Debug.Log("Hit object does not implement IInteractable.");
            }
        }
        else
        {
            Debug.Log("Raycast did not hit anything.");
        }

        // If nothing hit, clear highlight
        ClearHighlight();
    }

    private void ClearHighlight()
    {
        if (currentTarget != null)
        {
            currentTarget.Unhighlight();
            currentTarget = null;
        }
    }

    private void HandleInteract(object sender, System.EventArgs e)
    {
        if (currentTarget != null)
        {
            currentTarget.Interact(gameObject);
        }
        else
        {
            Debug.Log("Interact pressed, but no current target.");
        }
    }
}
