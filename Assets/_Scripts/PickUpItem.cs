using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class PickUpItem : MonoBehaviour, IInteractable
{
    [Header("Pickup Settings")]
    [SerializeField] private bool disableGravityWhileHeld = true;
    [SerializeField] private bool disableRunning = false;
    [SerializeField] private bool disableJumpingWhileHeld = false;

    private OutlineController outline;
    private Rigidbody rb;
    private FixedJoint carryJoint;
    private Transform carryAnchor;
    private GameObject currentCarrier;
    private bool isCarried = false;

    // ?? Events
    public event Action<GameObject> OnPickedUp;  // passes player
    public event Action<GameObject> OnDropped;   // passes player

    void Awake()
    {
        outline = GetComponent<OutlineController>();
        rb = GetComponent<Rigidbody>();
    }

    void OnDestroy()
    {
        if (carryJoint != null)
            Destroy(carryJoint);
    }

    public void Interact(GameObject player)
    {
        if (isCarried) Drop();
        else PickUp(player);
    }

    private void PickUp(GameObject player)
    {
        if (isCarried) return;

        currentCarrier = player; // ✅ assign first
        carryAnchor = player.GetComponent<PlayerCarryAnchor>()?.carryAnchor;

        if (carryAnchor == null)
        {
            Debug.LogWarning("Player has no carryAnchor assigned!");
            return;
        }

        isCarried = true;

        // Disable movement abilities if needed
        var movement = currentCarrier.GetComponent<PlayerMovement>();
        if (disableRunning && movement != null)
            movement.SetCanRun(false);
        if (disableJumpingWhileHeld && movement != null)
            movement.SetCanJump(false);

        // Ensure anchor has a Rigidbody
        Rigidbody anchorRb = carryAnchor.GetComponent<Rigidbody>();
        if (anchorRb == null)
        {
            anchorRb = carryAnchor.gameObject.AddComponent<Rigidbody>();
            anchorRb.isKinematic = true;
        }

        // Snap to anchor
        transform.position = carryAnchor.position;
        transform.rotation = carryAnchor.rotation;

        // Add joint
        carryJoint = gameObject.AddComponent<FixedJoint>();
        carryJoint.connectedBody = anchorRb;
        carryJoint.breakForce = Mathf.Infinity;
        carryJoint.breakTorque = Mathf.Infinity;

        // Configure rigidbody
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (disableGravityWhileHeld) rb.useGravity = false;

        Debug.Log($"{gameObject.name} picked up!");
        OnPickedUp?.Invoke(currentCarrier);
    }

    public void Drop()
    {
        if (!isCarried) return;

        isCarried = false;

        if (carryJoint != null)
        {
            Destroy(carryJoint);
            carryJoint = null;
        }

        rb.useGravity = true;
        rb.isKinematic = false;

        if (currentCarrier != null)
        {
            // ?? Inherit carrier movement velocity if available
            Vector3 carrierVelocity = Vector3.zero;

            var controller = currentCarrier.GetComponent<CharacterController>();
            if (controller != null)
                carrierVelocity = controller.velocity;

            // Optional: if you later add Rigidbody-based players
            var carrierRb = currentCarrier.GetComponent<Rigidbody>();
            if (carrierRb != null)
                carrierVelocity = carrierRb.linearVelocity;

            // ?? Apply inherited velocity + toss forward
            rb.linearVelocity = carrierVelocity;
            rb.AddForce(currentCarrier.GetComponentInChildren<Camera>().transform.forward * 2f, ForceMode.Impulse);
        }

        if (disableRunning && currentCarrier != null)
            currentCarrier.GetComponent<PlayerMovement>()?.SetCanRun(true);

        if (disableJumpingWhileHeld && currentCarrier != null)
            currentCarrier.GetComponent<PlayerMovement>()?.SetCanJump(true);

        Debug.Log($"{gameObject.name} dropped!");
        OnDropped?.Invoke(currentCarrier);

        currentCarrier = null;
        carryAnchor = null;
    }


    public bool IsCarried => isCarried;
    public GameObject CurrentCarrier => currentCarrier;

    public void Highlight() => outline?.EnableOutline();
    public void Unhighlight() => outline?.DisableOutline();
}
