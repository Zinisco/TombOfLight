using UnityEngine;

[RequireComponent(typeof(PickUpItem), typeof(Rigidbody))]
public class Orb : MonoBehaviour
{
    [Header("Orb Settings")]
    [SerializeField] private float maxCarryTime = 30f;
    [SerializeField] private int maxDurability = 3;
    [SerializeField] private Light orbLight;

    [Header("Impact Settings")]
    [Tooltip("Minimum collision speed (m/s) that counts as damage.")]
    [SerializeField] private float minBreakSpeed = 6f;
    [Tooltip("Small grace window after drop so joint cleanup doesn't count as an impact.")]
    [SerializeField] private float postDropGrace = 0.1f;
    [Tooltip("Cooldown to avoid double-counting multiple contacts in one crash.")]
    [SerializeField] private float damageCooldown = 1f;

    private PickUpItem pickUp;
    private Rigidbody rb;
    private float carryTimer;
    private int currentDurability;
    private float lastDamageTime = -999f;
    private float lastDropTime = -999f;
    private bool shattered = false;

    void Awake()
    {
        pickUp = GetComponent<PickUpItem>();
        rb = GetComponent<Rigidbody>();
        currentDurability = maxDurability;

        pickUp.OnPickedUp += HandlePickedUp;
        pickUp.OnDropped += HandleDropped;
    }

    void OnDestroy()
    {
        if (pickUp != null)
        {
            pickUp.OnPickedUp -= HandlePickedUp;
            pickUp.OnDropped -= HandleDropped;
        }
    }

    void Update()
    {
        if (shattered) return;

        if (pickUp.IsCarried)
        {
            carryTimer += Time.deltaTime;
            if (carryTimer >= maxCarryTime)
                ForceDrop();
        }
    }

    private void HandlePickedUp(GameObject player)
    {
        carryTimer = 0f;
        // No durability change here.
    }

    private void HandleDropped(GameObject player)
    {
        // Just note the time so we ignore immediate "joint pop" collisions.
        lastDropTime = Time.time;
    }

    private void ForceDrop()
    {
        carryTimer = 0f;
        pickUp.Drop();
        Debug.Log("Orb dropped due to weight!");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (shattered) return;

        // Ignore immediate post-drop joint cleanup
        if (Time.time - lastDropTime < postDropGrace) return;

        // Avoid counting multiple contacts for the same crash
        if (Time.time - lastDamageTime < damageCooldown) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minBreakSpeed) return;

        lastDamageTime = Time.time;

        // Always just 1 durability, carried or dropped
        int damage = 1;

        ApplyDamage(damage, impactSpeed, collision.GetContact(0).point);
    }


    private void ApplyDamage(int amount, float impactSpeed, Vector3 hitPoint)
    {
        if (shattered) return;

        currentDurability -= amount;
        Debug.Log($"Orb took {amount} damage from impact ({impactSpeed:F1} m/s). Durability left: {currentDurability}");

        if (currentDurability <= 0)
            Shatter(hitPoint);
    }

    private void Shatter(Vector3 where)
    {
        if (shattered) return;
        shattered = true;

        if (orbLight != null) orbLight.enabled = false;

        // Optional: particles / sound here
        Debug.Log("Orb shattered! Game Over.");

        // TODO: GameManager lose condition
        Destroy(gameObject);
    }
}
