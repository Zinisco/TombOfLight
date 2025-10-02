using UnityEngine;
using UnityEngine.UI;

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

    [Header("UI")]
    [SerializeField] private Slider carryTimeSlider; // Assign in Inspector
    [SerializeField] private float sliderSmoothSpeed = 5f;   // smoothness for draining UI

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

        if (carryTimeSlider != null)
        {
            carryTimeSlider.minValue = 0f;
            carryTimeSlider.maxValue = maxCarryTime;
            carryTimeSlider.value = maxCarryTime;
            carryTimeSlider.gameObject.SetActive(false); // hidden by default
        }
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

            float remainingTime = Mathf.Clamp(maxCarryTime - carryTimer, 0f, maxCarryTime);

            // Smoothly update UI to make it tick down
            if (carryTimeSlider != null)
            {
                carryTimeSlider.value = Mathf.Lerp(
                    carryTimeSlider.value,
                    remainingTime,
                    Time.deltaTime * sliderSmoothSpeed
                );
            }

            if (carryTimer >= maxCarryTime)
                ForceDrop();
        }
    }

    private void HandlePickedUp(GameObject player)
    {
        carryTimer = 0f;

        if (carryTimeSlider != null)
        {
            carryTimeSlider.value = maxCarryTime; // start full
            carryTimeSlider.gameObject.SetActive(true); // show when carried
        }
    }

    private void HandleDropped(GameObject player)
    {
        lastDropTime = Time.time;

        if (carryTimeSlider != null)
        {
            carryTimeSlider.gameObject.SetActive(false); // hide on drop
        }
    }

    private void ForceDrop()
    {
        carryTimer = 0f;
        pickUp.Drop();

        if (carryTimeSlider != null)
            carryTimeSlider.gameObject.SetActive(false);

        Debug.Log("Orb dropped due to max carry time!");
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (shattered) return;

        if (Time.time - lastDropTime < postDropGrace) return;
        if (Time.time - lastDamageTime < damageCooldown) return;

        float impactSpeed = collision.relativeVelocity.magnitude;
        if (impactSpeed < minBreakSpeed) return;

        lastDamageTime = Time.time;

        ApplyDamage(1, impactSpeed, collision.GetContact(0).point);
    }

    private void ApplyDamage(int amount, float impactSpeed, Vector3 hitPoint)
    {
        if (shattered) return;

        currentDurability -= amount;
        Debug.Log($"Orb took {amount} damage ({impactSpeed:F1} m/s). Durability left: {currentDurability}");

        if (currentDurability <= 0)
            Shatter(hitPoint);
    }

    private void Shatter(Vector3 where)
    {
        if (shattered) return;
        shattered = true;

        if (orbLight != null) orbLight.enabled = false;

        if (carryTimeSlider != null)
            carryTimeSlider.gameObject.SetActive(false);

        Debug.Log("Orb shattered! Game Over.");
        Destroy(gameObject);
    }
}
