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
    [Tooltip("Speed that instantly breaks all remaining durability.")]
    [SerializeField] private float oneHitBreakSpeed = 14f;
    [Tooltip("Small grace window after drop so joint cleanup doesn't count as an impact.")]
    [SerializeField] private float postDropGrace = 0.08f;
    [Tooltip("Cooldown to avoid double-counting multiple contacts in one crash.")]
    [SerializeField] private float damageCooldown = 0.15f;

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
        // Do NOT reduce durability here.
        // Just note the time so we ignore immediate joint pop collisions.
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

        // Ignore while carried or during tiny grace window after dropping.
        if (pickUp != null && pickUp.IsCarried) return;
        if (Time.time - lastDropTime < postDropGrace) return;

        // Avoid counting a single crash multiple times
        if (Time.time - lastDamageTime < damageCooldown) return;

        // How hard did we hit?
        // relativeVelocity is reliable for "how fast the two bodies were moving into each other".
        float impactSpeed = collision.relativeVelocity.magnitude;

        if (impactSpeed < minBreakSpeed) return;

        lastDamageTime = Time.time;

        int damage;
        if (impactSpeed >= oneHitBreakSpeed)
        {
            // Big slam: break all remaining durability
            damage = currentDurability;
        }
        else
        {
            // Normal hard impact
            damage = 1;
        }

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

        // Optional: particles / sound here, then disable or destroy
        // e.g., Instantiate(shatterVfx, where, Quaternion.identity);

        Debug.Log("Orb shattered! Game Over.");
        // TODO: trigger GameManager lose condition
        // Destroy(gameObject);
    }
}
