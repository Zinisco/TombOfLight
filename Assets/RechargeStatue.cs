using UnityEngine;

public class RechargeStatue : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform chargeZone;   // child with BoxCollider (IsTrigger)
    [SerializeField] private float floatSpeed = 2f;
    [SerializeField] private float floatHeight = 0.1f;
    [SerializeField] private float catchSpeed = 3f;   // how fast orb moves to center
    [SerializeField] private float snapDistance = 0.05f; // threshold to "lock" into place

    private Orb orbInZone;
    private bool isCharging = false;
    private bool isCatching = false;

    private BoxCollider zoneCollider;
    private Vector3 zoneLocalCenter;

    void Awake()
    {
        if (chargeZone != null)
        {
            zoneCollider = chargeZone.GetComponent<BoxCollider>();
            if (zoneCollider == null)
                Debug.LogError($"[RechargeStatue] {name}: ChargeZone needs a BoxCollider set to IsTrigger!");
            else
            {
                zoneLocalCenter = zoneCollider.center;
                Debug.Log($"[RechargeStatue] {name}: Found BoxCollider on {chargeZone.name}, isTrigger={zoneCollider.isTrigger}");
            }
        }
    }

    private void HandleOrbDropped(GameObject player)
    {
        if (orbInZone != null && !isCharging && !isCatching)
        {
            Debug.Log("[RechargeStatue] Orb dropped inside zone, catching now...");
            StartCatching(orbInZone);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        Orb orb = other.GetComponent<Orb>();
        if (orb != null)
        {
            orbInZone = orb;
            Debug.Log($"[RechargeStatue] Orb {orb.name} entered recharge zone");

            var pickUp = orb.GetComponent<PickUpItem>();
            if (pickUp != null)
            {
                // Subscribe to drop so we can auto-catch if dropped while inside
                pickUp.OnDropped += HandleOrbDropped;
            }

            // If it’s already free, catch immediately
            if (pickUp == null || !pickUp.IsCarried)
            {
                Debug.Log("[RechargeStatue] Orb is free, starting auto-charge...");
                StartCatching(orb);
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (orbInZone != null && other.gameObject == orbInZone.gameObject)
        {
            // Unsubscribe to avoid leaks
            var pickUp = orbInZone.GetComponent<PickUpItem>();
            if (pickUp != null)
                pickUp.OnDropped -= HandleOrbDropped;

            if (isCharging) ReleaseOrb();
            orbInZone = null;
            Debug.Log("[RechargeStatue] Orb left recharge zone");
        }
    }

    void Update()
    {
        if (isCatching && orbInZone != null)
        {
            CatchOrb();
        }
        else if (isCharging && orbInZone != null)
        {
            FloatOrb();
        }
    }

    private void StartCatching(Orb orb)
    {
        if (orb == null) return;

        var pickUp = orb.GetComponent<PickUpItem>();
        if (pickUp != null && pickUp.IsCarried)
            pickUp.Drop();

        Rigidbody rb = orb.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        orb.transform.SetParent(chargeZone, true);
        isCatching = true;
    }

    private void CatchOrb()
    {
        if (orbInZone == null) return;

        Vector3 targetLocal = zoneLocalCenter;
        orbInZone.transform.localPosition =
            Vector3.Lerp(orbInZone.transform.localPosition, targetLocal, Time.deltaTime * catchSpeed);

        if (Vector3.Distance(orbInZone.transform.localPosition, targetLocal) < snapDistance)
        {
            orbInZone.transform.localPosition = targetLocal;
            orbInZone.transform.localRotation = Quaternion.identity;

            // Force reset before staged recharge
            orbInZone.ForceResetCarryTimer();

            orbInZone.RechargeStaged(2.5f);
            isCatching = false;
            isCharging = true;

            Debug.Log("[RechargeStatue] Orb locked into center, timer reset, and charging started");
        }
    }


    private void FloatOrb()
    {
        if (orbInZone == null) return;

        Vector3 floatPos = zoneLocalCenter;
        floatPos.y += Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        orbInZone.transform.localPosition = floatPos;
    }

    private void ReleaseOrb()
    {
        if (orbInZone == null) return;

        orbInZone.transform.SetParent(null);

        Rigidbody rb = orbInZone.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        isCharging = false;
    }
}
