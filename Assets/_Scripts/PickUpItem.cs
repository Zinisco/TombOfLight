using UnityEngine;
using System;
using UnityEngine.UI;

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

    [Header("Throw Settings")]
    private float throwHoldTime = 0f;   // track how long button held
    [SerializeField] private float holdToThrowTime = 0.25f; // tap < this = drop, hold >= this = throw
    [SerializeField] private float minThrowForce = 4f; // baseline gentle toss
    [SerializeField] private float maxThrowForce = 15f;
    [SerializeField] private float chargeRate = 10f;
    private float currentThrowForce = 0f;
    private bool isChargingThrow = false;

    [Header("Spring Settings")]
    [SerializeField] private float followStrength = 50f;      // how hard it snaps back
    [SerializeField] private float followDamping = 5f;        // how much it resists overshoot
    [SerializeField] private float maxFollowDistance = 3f;    // break distance (auto drop)
    [SerializeField] private Vector3 holdOffset = new Vector3(0, -0.2f, 0.6f);

    [Header("UI")]
    [SerializeField] private Slider chargeSlider; // assign in Inspector

    private OutlineController outline;
    private Rigidbody rb;
    private Transform carryAnchor;
    private GameObject currentCarrier;
    private bool isCarried = false;

    public event Action<GameObject> OnPickedUp;
    public event Action<GameObject> OnDropped;

    void Awake()
    {
        outline = GetComponent<OutlineController>();
        rb = GetComponent<Rigidbody>();

        if (chargeSlider != null)
        {
            chargeSlider.gameObject.SetActive(false); // hide by default
            chargeSlider.minValue = minThrowForce;
            chargeSlider.maxValue = maxThrowForce;
            chargeSlider.value = minThrowForce;
        }
    }

    private void Update()
    {
        if (isCarried && isChargingThrow)
        {
            throwHoldTime += Time.deltaTime;
            currentThrowForce += chargeRate * Time.deltaTime;
            currentThrowForce = Mathf.Min(currentThrowForce, maxThrowForce);

            // 🔹 Update UI while charging
            if (chargeSlider != null)
            {
                chargeSlider.value = currentThrowForce;
            }
        }
    }

    public void Interact(GameObject player)
    {
        if (!isCarried)
            PickUp(player);
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

        // If orb allows drop on spin and angular speed exceeds threshold, drop
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

    private void PickUp(GameObject player)
    {
        if (isCarried)
        {
            Debug.Log("PickUp called but already carried");
            return;
        }

        Debug.Log("PickUp called on " + gameObject.name);

        currentCarrier = player;
        carryAnchor = player.GetComponent<PlayerCarryAnchor>()?.carryAnchor;

        if (carryAnchor == null)
        {
            Debug.LogWarning("Player has no carryAnchor assigned!");
            return;
        }

        isCarried = true;

        // Subscribe to throw events ONCE
        var input = GameInput.Instance;
        if (input != null)
        {
            Debug.Log(">>> Subscribing to throw events via GameInput.Instance");
            input.OnThrowStart += HandleThrowStart;
            input.OnThrowRelease += HandleThrowRelease;
        }
        else
        {
            Debug.LogError("GameInput.Instance not found in scene!");
        }

        // Disable movement if needed
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


    public void Drop()
    {
        if (!isCarried) return;

        // Unsubscribe ONCE when dropped
        var input = currentCarrier?.GetComponent<GameInput>();
        if (input != null)
        {
            Debug.Log("Unsubscribing from throw events");
            input.OnThrowStart -= HandleThrowStart;
            input.OnThrowRelease -= HandleThrowRelease;
        }

        if (chargeSlider != null)
        {
            chargeSlider.value = 0f;
            chargeSlider.gameObject.SetActive(false);
        }

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

    private void HandleThrowStart(object sender, EventArgs e)
    {
        Debug.Log("Throw started (PickUpItem)");
        if (!isCarried) return;

        isChargingThrow = true;
        throwHoldTime = 0f;
        currentThrowForce = minThrowForce;


        // 🔹 Show UI
        if (chargeSlider != null)
        {
            float normalized = (currentThrowForce - minThrowForce) / (maxThrowForce - minThrowForce);
            chargeSlider.value = Mathf.Clamp01(normalized);

            chargeSlider.gameObject.SetActive(true);
        }
    }

    private void HandleThrowRelease(object sender, EventArgs e)
    {
        Debug.Log("Throw released (PickUpItem)");
        if (!isCarried) return;

        isChargingThrow = false;

        // Hide UI
        if (chargeSlider != null)
            chargeSlider.gameObject.SetActive(false);

        if (throwHoldTime < holdToThrowTime)
        {
            Debug.Log($"{gameObject.name} gently dropped (tap)");
            Drop();
        }
        else
        {
            Throw();
        }
    }

    private void Throw()
    {
        float finalForce = Mathf.Clamp(currentThrowForce, minThrowForce, maxThrowForce);

        Camera cam = currentCarrier?.GetComponentInChildren<Camera>();
        Drop();

        if (cam != null)
        {
            rb.AddForce(cam.transform.forward * finalForce, ForceMode.Impulse);
            Debug.Log($"{gameObject.name} thrown with force {finalForce}");
        }

        currentThrowForce = 0f;
        throwHoldTime = 0f;
    }


    public bool IsCarried => isCarried;
    public GameObject CurrentCarrier => currentCarrier;

    public void Highlight() => outline?.EnableOutline();
    public void Unhighlight() => outline?.DisableOutline();
}
