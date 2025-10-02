using UnityEngine;
using System;

[RequireComponent(typeof(Rigidbody))]
public class PickUpItem : MonoBehaviour, IInteractable
{
    [Header("Pickup Settings")]
    [SerializeField] private bool disableGravityWhileHeld = true;
    [SerializeField] private bool disableRunning = false;
    [SerializeField] private bool disableJumpingWhileHeld = false;

    [Header("Special Settings")]
    [SerializeField] private bool allowDropOnSpin = false;
    [SerializeField] private float spinDropThreshold = 15f; // sensitivity: how hard you can spin before drop


    [Header("Spring Settings")]
    [SerializeField] private float followStrength = 50f;      // how hard it snaps back
    [SerializeField] private float followDamping = 5f;        // how much it resists overshoot
    [SerializeField] private float maxFollowDistance = 3f;    // break distance (auto drop)
    [SerializeField] private Vector3 holdOffset = new Vector3(0, -0.2f, 0.6f);

    private OutlineController outline;
    private Rigidbody rb;
    private Transform carryAnchor;
    private GameObject currentCarrier;
    private bool isCarried = false;

    private Vector3 velocitySmoothed;

    public event Action<GameObject> OnPickedUp;
    public event Action<GameObject> OnDropped;

    void Awake()
    {
        outline = GetComponent<OutlineController>();
        rb = GetComponent<Rigidbody>();
    }

    public void Interact(GameObject player)
    {
        if (isCarried) Drop();
        else PickUp(player);
    }

    private void PickUp(GameObject player)
    {
        if (isCarried) return;

        currentCarrier = player;
        carryAnchor = player.GetComponent<PlayerCarryAnchor>()?.carryAnchor;

        if (carryAnchor == null)
        {
            Debug.LogWarning("Player has no carryAnchor assigned!");
            return;
        }

        isCarried = true;

        var movement = currentCarrier.GetComponent<PlayerMovement>();
        if (disableRunning && movement != null) movement.SetCanRun(false);
        if (disableJumpingWhileHeld && movement != null) movement.SetCanJump(false);

        rb.useGravity = !disableGravityWhileHeld;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Debug.Log($"{gameObject.name} picked up with spring method!");
        OnPickedUp?.Invoke(currentCarrier);
    }

    private void FixedUpdate()
    {
        if (!isCarried || carryAnchor == null) return;

        // Desired target position = anchor + offset
        Vector3 targetPos = carryAnchor.TransformPoint(holdOffset);
        Vector3 toTarget = targetPos - transform.position;

        // If it’s too far away (player ran off), auto drop
        if (toTarget.magnitude > maxFollowDistance)
        {
            Drop();
            return;
        }

        var movement = currentCarrier.GetComponent<PlayerMovement>();
        float angularSpeed = movement?.GetLookDeltaMagnitude() ?? 0f;

        //  If orb allows drop on spin and angular speed exceeds threshold, drop
        if (allowDropOnSpin && angularSpeed >= spinDropThreshold)
        {
            Debug.Log($"{gameObject.name} dropped due to fast spin!");
            Drop();
            return;
        }

        // Add drag multiplier based on rotation speed
        float dragMultiplier = 1f + angularSpeed * 0.2f;

        // Spring force
        Vector3 springForce = toTarget * followStrength - rb.linearVelocity * (followDamping * dragMultiplier);
        rb.AddForce(springForce, ForceMode.Acceleration);
    }



    public void Drop()
    {
        if (!isCarried) return;
        isCarried = false;

        if (currentCarrier != null)
        {
            var movement = currentCarrier.GetComponent<PlayerMovement>();
            if (disableRunning && movement != null) movement.SetCanRun(true);
            if (disableJumpingWhileHeld && movement != null) movement.SetCanJump(true);

            // Inherit some velocity on drop
            Vector3 carrierVelocity = Vector3.zero;
            var controller = currentCarrier.GetComponent<CharacterController>();
            if (controller != null) carrierVelocity = controller.velocity;
            rb.linearVelocity += carrierVelocity;
        }

        rb.useGravity = true;

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
