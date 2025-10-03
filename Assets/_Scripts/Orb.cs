using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PickUpItem), typeof(Rigidbody))]
public class Orb : MonoBehaviour
{

    [Header("Orb Settings")]
    [SerializeField] private float maxCarryTime = 30f;
    [SerializeField] private int maxDurability = 3;
    [SerializeField] private Light orbLight;
    [SerializeField] private float maxLifespanTime = 120f; // total life before burn-out
    [SerializeField] private float pickupResetCooldown = 0.75f; // how long before pickup resets timer

    private GameObject lastCarrier;
    private float lifespanTimer;

    [Header("Impact Settings")]
    [SerializeField] private float lifespanDamagePenalty = 30f;
    [Tooltip("Minimum collision speed (m/s) that counts as damage.")]
    [SerializeField] private float minBreakSpeed = 6f;
    [Tooltip("Small grace window after drop so joint cleanup doesn't count as an impact.")]
    [SerializeField] private float postDropGrace = 0.1f;
    [Tooltip("Cooldown to avoid double-counting multiple contacts in one crash.")]
    [SerializeField] private float damageCooldown = 1f;

    [Header("UI")]
    [SerializeField] private Slider carryTimeSlider; // Assign in Inspector
    [SerializeField] private float sliderSmoothSpeed = 5f;   // smoothness for draining UI

    [Header("Light Temperature Settings")]
    [SerializeField] private float startTemperature = 8000f; // cool white
    [SerializeField] private float endTemperature = 1500f;   // warm red
    [SerializeField] private Renderer orbRenderer;  // assign the orb mesh in Inspector
    private Material orbMaterial;

    [Header("Light Intensity & Range")]
    [SerializeField] private float startIntensity = 5f;
    [SerializeField] private float endIntensity = 0.2f;
    [SerializeField] private float startRange = 30f;
    [SerializeField] private float endRange = 2f;

    [Header("Flicker Settings")]
    [SerializeField] private float flickerSpeed = 15f;
    [SerializeField] private float flickerIntensity = 0.3f; // how strong the flicker is

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

        if (orbRenderer != null)
        {
            orbMaterial = orbRenderer.material; // gets a unique instance
            orbMaterial.EnableKeyword("_EMISSION");
        }

        if (carryTimeSlider != null)
        {
            carryTimeSlider.minValue = 0f;
            carryTimeSlider.maxValue = maxCarryTime;
            carryTimeSlider.value = maxCarryTime;
            carryTimeSlider.gameObject.SetActive(false); // hidden by default
        }

        if (orbLight != null)
        {
            orbLight.useColorTemperature = true; // enable Kelvin mode
            orbLight.colorTemperature = startTemperature;
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

        // --- Lifespan drains always ---
        lifespanTimer += Time.deltaTime;
        float remainingLife = Mathf.Clamp(maxLifespanTime - lifespanTimer, 0f, maxLifespanTime);
        float lifeFactor = remainingLife / maxLifespanTime; // 1 - 0

        // --- Carry drains only when carried ---
        float carryFactor = 0f;
        if (pickUp.IsCarried)
        {
            carryTimer += Time.deltaTime;
            float remainingCarry = Mathf.Clamp(maxCarryTime - carryTimer, 0f, maxCarryTime);
            carryFactor = 1f - (remainingCarry / maxCarryTime); // 0 - 1 only while carried

            // Smooth UI
            if (carryTimeSlider != null)
            {
                carryTimeSlider.value = Mathf.Lerp(
                    carryTimeSlider.value,
                    remainingCarry,
                    Time.deltaTime * sliderSmoothSpeed
                );
            }

            if (carryTimer >= maxCarryTime)
                ForceDrop();
        }

        // --- Update visuals ---
        UpdateOrbVisuals(lifeFactor, carryFactor, (float)currentDurability / maxDurability);

        // --- Burn out ---
        if (lifespanTimer >= maxLifespanTime)
            Shatter(transform.position);
    }


    private void HandlePickedUp(GameObject player)
    {
        bool isDifferentCarrier = (lastCarrier != null && player != lastCarrier);
        bool enoughTimePassed = (Time.time - lastDropTime) > pickupResetCooldown;

        if (isDifferentCarrier || enoughTimePassed)
        {
            carryTimer = 0f; // normal reset
        }

        lastCarrier = player;

        if (carryTimeSlider != null)
        {
            float remainingCarry = Mathf.Clamp(maxCarryTime - carryTimer, 0f, maxCarryTime);
            carryTimeSlider.value = remainingCarry;
            carryTimeSlider.gameObject.SetActive(true);
        }

        if (orbLight != null)
            orbLight.colorTemperature = startTemperature;
    }


    private void HandleDropped(GameObject player)
    {
        lastDropTime = Time.time;

        if (carryTimeSlider != null)
            carryTimeSlider.gameObject.SetActive(false); // hide on drop

        // Reset color temp when dropped
        if (orbLight != null)
            orbLight.colorTemperature = startTemperature;

        if (orbMaterial != null)
        {
            Color emissionColor = ColorFromTemperature(startTemperature) * Mathf.LinearToGammaSpace(orbLight.intensity);
            orbMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }

    private void ForceDrop()
    {
        carryTimer = 0f;
        pickUp.Drop();

        if (carryTimeSlider != null)
            carryTimeSlider.gameObject.SetActive(false);

        // Reset color temp and emission when force-dropped
        if (orbLight != null)
            orbLight.colorTemperature = startTemperature;

        if (orbMaterial != null)
        {
            Color emissionColor = ColorFromTemperature(startTemperature) * Mathf.LinearToGammaSpace(orbLight.intensity);
            orbMaterial.SetColor("_EmissionColor", emissionColor);
        }

        Debug.Log("Orb force-dropped!");
    }



    private void OnCollisionEnter(Collision collision)
    {
        if (shattered) return;

        //  If orb is not carried and it hit the ground, reset carry timer
        if (!pickUp.IsCarried)
        {
            ForceResetCarryTimer();
            Debug.Log("[Orb] Carry timer reset due to ground collision.");
        }

        // --- Existing damage logic ---
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

    // --- Durability loss ---
    currentDurability -= amount;

    // --- Lifespan penalty ---
    float penalty = lifespanDamagePenalty * amount;

    // Optional: scale penalty by impact speed for harder hits
    float speedFactor = Mathf.Clamp01(impactSpeed / 20f); // normalizes speed
    penalty *= (1f + speedFactor * 0.5f); // up to +50% extra

    lifespanTimer += penalty;

    Debug.Log(
        $"Orb took {amount} damage ({impactSpeed:F1} m/s). " +
        $"Durability left: {currentDurability}. " +
        $"Lifespan shortened by {penalty:F1}s (remaining: {Mathf.Max(0, maxLifespanTime - lifespanTimer):F1}s)."
    );

    if (currentDurability <= 0)
        Shatter(hitPoint);
}


    public void Recharge()
    {
        carryTimer = 0f;
        lifespanTimer = 0f; // RESET lifespan
        currentDurability = maxDurability;

        if (carryTimeSlider != null)
            carryTimeSlider.value = maxCarryTime;

        UpdateOrbVisuals(1f, 1f, 1f);

        Debug.Log("Orb recharged at station!");
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

    private void UpdateOrbVisuals(float lifeFactor, float carryFactor, float durabilityFactor)
    {
        if (orbLight == null) return;

        // Lifespan controls brightness
        float tLife = 1f - lifeFactor;
        float baseIntensity = Mathf.Lerp(startIntensity, endIntensity, tLife) * durabilityFactor;
        float range = Mathf.Lerp(startRange, endRange, tLife) * durabilityFactor;

        // Flicker if on last hit
        float finalIntensity = baseIntensity;
        float flickerFactor = 1f;

        if (currentDurability == 1)
        {
            // Use Perlin noise for chaotic flicker
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, 0f); // 0-1
            float jitter = 1f - (noise * flickerIntensity);
            flickerFactor = jitter;
            finalIntensity = baseIntensity * jitter;
        }


        orbLight.intensity = finalIntensity;
        orbLight.range = range;

        // Carry controls color temp
        float kelvin = Mathf.Lerp(startTemperature, endTemperature, carryFactor);
        orbLight.colorTemperature = kelvin;

        // Emission matches color and flicker
        if (orbMaterial != null)
        {
            Color emissionColor = ColorFromTemperature(kelvin)
                                  * Mathf.LinearToGammaSpace(finalIntensity * flickerFactor);
            orbMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }


    private Color ColorFromTemperature(float kelvin)
    {
        float temp = kelvin / 100f;
        float r, g, b;

        // Red
        if (temp <= 66f)
            r = 1f;
        else
            r = Mathf.Clamp01(329.698727446f * Mathf.Pow(temp - 60f, -0.1332047592f) / 255f);

        // Green
        if (temp <= 66f)
            g = Mathf.Clamp01((99.4708025861f * Mathf.Log(temp) - 161.1195681661f) / 255f);
        else
            g = Mathf.Clamp01(288.1221695283f * Mathf.Pow(temp - 60f, -0.0755148492f) / 255f);

        // Blue
        if (temp >= 66f)
            b = 1f;
        else if (temp <= 19f)
            b = 0f;
        else
            b = Mathf.Clamp01((138.5177312231f * Mathf.Log(temp - 10f) - 305.0447927307f) / 255f);

        return new Color(r, g, b, 1f);
    }

    public void RechargeStaged(float duration = 2f)
    {
        StopAllCoroutines();

        // Calculate missing durability ONCE
        int missingDurability = maxDurability - currentDurability;
        if (missingDurability <= 0)
        {
            Debug.Log("[Orb] Already full durability, no staged recharge needed.");
            return;
        }

        StartCoroutine(RechargeRoutine(duration, missingDurability, currentDurability));
    }

    public void ForceResetCarryTimer()
    {
        carryTimer = 0f;

        if (carryTimeSlider != null)
        {
            carryTimeSlider.value = maxCarryTime;
        }

        if (orbLight != null)
            orbLight.colorTemperature = startTemperature;

        Debug.Log("[Orb] Carry timer forcibly reset (zone, ground hit, or transfer).");
    }


    private IEnumerator RechargeRoutine(float duration, int missingDurability, int startingDurability)
    {
        Debug.Log($"[Orb] Starting staged recharge with {missingDurability} booms...");

        // Lifespan staged restore: fully reset by the last boom
        float startLifespanTimer = lifespanTimer;
        float targetLifespanTimer = 0f;

        if (carryTimeSlider != null)
            carryTimeSlider.value = maxCarryTime;

        float stepDelay = duration / missingDurability;

        for (int i = 1; i <= missingDurability; i++)
        {
            // --- Flicker before the boom ---
            float flickerTime = stepDelay * 0.3f; // 30% of step time reserved for flicker
            float elapsed = 0f;

            while (elapsed < flickerTime)
            {
                elapsed += Time.deltaTime;

                // Random light jitter
                float noise = Mathf.PerlinNoise(Time.time * 30f, 0f); // fast noise
                float jitter = Mathf.Lerp(0.8f, 1.2f, noise); // between 80–120%
                orbLight.intensity *= jitter;

                if (orbMaterial != null)
                {
                    Color emissionColor = ColorFromTemperature(startTemperature) *
                                          Mathf.LinearToGammaSpace(orbLight.intensity);
                    orbMaterial.SetColor("_EmissionColor", emissionColor);
                }

                yield return null;
            }

            // --- Wait remainder of step before boom ---
            yield return new WaitForSeconds(stepDelay - flickerTime);

            // --- BOOM: Durability restore ---
            currentDurability = startingDurability + i;

            // Lifespan staged restore
            float fraction = (float)i / missingDurability;
            lifespanTimer = Mathf.Lerp(startLifespanTimer, targetLifespanTimer, fraction);

            // Visuals based on durability fraction
            float durabilityFraction = (float)currentDurability / maxDurability;
            orbLight.intensity = Mathf.Lerp(endIntensity, startIntensity, durabilityFraction);
            orbLight.range = Mathf.Lerp(endRange, startRange, durabilityFraction);

            if (orbMaterial != null)
            {
                Color emissionColor = ColorFromTemperature(startTemperature) *
                                      Mathf.LinearToGammaSpace(orbLight.intensity);
                orbMaterial.SetColor("_EmissionColor", emissionColor);
            }

            Debug.Log($"[Orb] BOOM {i}/{missingDurability} ? Durability {currentDurability}/{maxDurability}, Lifespan restored {(1f - (lifespanTimer / maxLifespanTime)) * 100f:F0}%");
        }

        Debug.Log("[Orb] Recharge complete!");
    }


}
