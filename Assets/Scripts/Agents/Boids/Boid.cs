/// <summary>
/// Agente autónomo Boid. Aplica Flocking sensorial local, prioriza Evade ante el Cazador, selecciona POIs estrictamente por menor distancia geométrica y gestiona su ciclo de vida y reaparición diferida.
/// </summary>
using System.Collections.Generic;
using UnityEngine;

public class Boid : SteeringAgent
{
    [Header("Salud y Estado")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public bool isDead = false;

    [Header("Radios de Percepción")]
    public float viewRadius = 8f;
    public float separationRadius = 2.5f;
    public float poiSlowingRadius = 3.5f;
    public float poiPerceptionRadius = 10f;

    [Header("Interacción POI")]
    public float poiInteractionRadius = 1.5f;
    public float poiDamageAmount = 15f;
    public float poiDamageInterval = 1f;
    private float lastPOIDamageTime = 0f;

    [Header("Pesos de Comportamientos")]
    public float alignmentWeight = 1f;
    public float cohesionWeight = 1f;
    public float separationWeight = 1.8f;
    public float poiWeight = 2f;
    public float evadeWeight = 2.5f;
    public float boundaryWeight = 1.5f;

    [Header("Configuración de Tags y Capas")]
    public string hunterTag = "Hunter";
    public string poiTag = "POI";
    public string boidLayerName = "Boid";
    public LayerMask detectionLayers = ~0;

    [Header("Respawn y Contención")]
    public float respawnDelay = 5f;
    public Vector3 spawnAreaCenter = Vector3.zero;
    public Vector3 spawnAreaSize = new Vector3(25f, 0f, 25f);

    private readonly List<SteeringAgent> flockNeighbors = new List<SteeringAgent>();
    private readonly List<SteeringAgent> separationNeighbors = new List<SteeringAgent>();

    private Renderer[] meshRenderers;
    private Collider[] colliders;
    private Color[] originalColors;
    private int boidLayerIndex = -1;

    public bool isRespawning { get; private set; } = false;
    private float respawnTimer = 0f;

    protected override void Awake()
    {
        base.Awake();
        lockRotationToY = true;
        lockMovementToXZ = true;

        currentHealth = maxHealth;
        boidLayerIndex = LayerMask.NameToLayer(boidLayerName);
        meshRenderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();

        if (meshRenderers != null && meshRenderers.Length > 0)
        {
            originalColors = new Color[meshRenderers.Length];
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i].material.HasProperty("_Color"))
                {
                    originalColors[i] = meshRenderers[i].material.color;
                }
                else
                {
                    originalColors[i] = Color.white;
                }
            }
        }
    }

    private void OnValidate()
    {
        if (separationRadius > viewRadius)
        {
            separationRadius = viewRadius;
        }
    }

    protected override void Update()
    {
        if (isRespawning)
        {
            velocity = Vector3.zero;
            acceleration = Vector3.zero;
            respawnTimer += Time.deltaTime;
            if (respawnTimer >= respawnDelay)
            {
                CompleteRespawn();
            }
            return;
        }

        if (isDead)
        {
            velocity = Vector3.zero;
            acceleration = Vector3.zero;
            return;
        }

        Collider[] hits = Physics.OverlapSphere(transform.position, viewRadius, detectionLayers);
        Collider hunterCollider = null;
        float minHunterDistSqr = float.MaxValue;

        flockNeighbors.Clear();
        separationNeighbors.Clear();
        float sqrSeparationRadius = separationRadius * separationRadius;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit.gameObject == gameObject) continue;

            if (hit.CompareTag(hunterTag))
            {
                float distSqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (distSqr < minHunterDistSqr)
                {
                    minHunterDistSqr = distSqr;
                    hunterCollider = hit;
                }
                continue;
            }

            bool isBoidLayer = (boidLayerIndex != -1 && hit.gameObject.layer == boidLayerIndex);
            SteeringAgent neighborAgent = hit.GetComponent<SteeringAgent>() ?? hit.GetComponentInParent<SteeringAgent>();

            if (neighborAgent != null && neighborAgent != this && (isBoidLayer || hit.CompareTag(gameObject.tag)))
            {
                if (neighborAgent is Boid neighborBoid && (neighborBoid.isDead || neighborBoid.isRespawning))
                {
                    continue;
                }

                flockNeighbors.Add(neighborAgent);

                float distSqr = (neighborAgent.transform.position - transform.position).sqrMagnitude;
                if (distSqr <= sqrSeparationRadius)
                {
                    separationNeighbors.Add(neighborAgent);
                }
            }
        }

        if (hunterCollider != null)
        {
            Vector3 hunterVelocity = Vector3.zero;
            SteeringAgent hunterAgent = hunterCollider.GetComponent<SteeringAgent>() ?? hunterCollider.GetComponentInParent<SteeringAgent>();

            if (hunterAgent != null)
            {
                hunterVelocity = hunterAgent.velocity;
            }
            else if (hunterCollider.attachedRigidbody != null)
            {
                #if UNITY_6000_0_OR_NEWER
                hunterVelocity = hunterCollider.attachedRigidbody.linearVelocity;
                #else
                hunterVelocity = hunterCollider.attachedRigidbody.velocity;
                #endif
            }

            Vector3 hunterTargetPos = hunterCollider.transform.position;
            hunterTargetPos.y = transform.position.y;
            hunterVelocity.y = 0f;

            Vector3 evadeForce = SteeringBehaviors.Evade(this, hunterTargetPos, hunterVelocity);
            evadeForce.y = 0f;
            ApplyForce(evadeForce * evadeWeight);
            base.Update();
            return;
        }

        PointOfInterest targetPOI = PrioritizePOIByProximity();
        if (targetPOI != null)
        {
            float distToPOI = Vector3.Distance(transform.position, targetPOI.transform.position);
            if (distToPOI <= poiInteractionRadius)
            {
                velocity = Vector3.zero;
                acceleration = Vector3.zero;
                Vector3 toPOI = targetPOI.transform.position - transform.position;
                toPOI.y = 0f;
                if (toPOI.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPOI.normalized), rotationSpeed * Time.deltaTime);
                }
                InteractWithPOI(targetPOI);
                return;
            }

            Vector3 arriveForce = SteeringBehaviors.Arrive(this, targetPOI.transform.position, poiSlowingRadius);
            arriveForce.y = 0f;

            Vector3 separationForce = CalculateSeparation(separationNeighbors) * separationWeight;

            Vector3 poiMoveForce = (arriveForce * poiWeight) + separationForce;
            ApplyForce(poiMoveForce);
            base.Update();
            return;
        }

        Vector3 alignmentForce = CalculateAlignment(flockNeighbors) * alignmentWeight;
        Vector3 cohesionForce = CalculateCohesion(flockNeighbors) * cohesionWeight;
        Vector3 flockSeparationForce = CalculateSeparation(separationNeighbors) * separationWeight;

        Vector3 totalForce = alignmentForce + cohesionForce + flockSeparationForce;

        Vector3 toCenter = spawnAreaCenter - transform.position;
        toCenter.y = 0f;
        float maxAllowedDist = Mathf.Max(spawnAreaSize.x, spawnAreaSize.z) * 0.5f;

        if (toCenter.magnitude > maxAllowedDist)
        {
            Vector3 returnForce = SteeringBehaviors.Seek(this, spawnAreaCenter);
            returnForce.y = 0f;
            totalForce += returnForce * boundaryWeight;
        }
        else if (flockNeighbors.Count == 0)
        {
            velocity = Vector3.MoveTowards(velocity, velocity.normalized * (maxSpeed * 0.5f), 2f * Time.deltaTime);
            Vector3 gentleReturn = SteeringBehaviors.Seek(this, spawnAreaCenter);
            gentleReturn.y = 0f;
            totalForce += gentleReturn * (boundaryWeight * 0.3f);
        }

        ApplyForce(totalForce);
        base.Update();
    }

    public PointOfInterest PrioritizePOIByProximity()
    {
        PointOfInterest nearestPOI = null;
        float shortestDistance = float.MaxValue;
        Vector3 currentPosition = transform.position;

        for (int i = 0; i < PointOfInterest.ActivePOIs.Count; i++)
        {
            PointOfInterest poi = PointOfInterest.ActivePOIs[i];
            if (poi == null || poi.currentHealth <= 0f) continue;

            float distance = Vector3.Distance(currentPosition, poi.transform.position);

            if (distance <= poiPerceptionRadius && distance < shortestDistance)
            {
                shortestDistance = distance;
                nearestPOI = poi;
            }
        }

        return nearestPOI;
    }

    private void InteractWithPOI(PointOfInterest targetPOI)
    {
        if (targetPOI == null) return;

        if (Time.time >= lastPOIDamageTime + poiDamageInterval)
        {
            targetPOI.TakeDamage(poiDamageAmount);
            lastPOIDamageTime = Time.time;
        }
    }

    public void TakeDamage(float amount)
    {
        if (isDead || isRespawning) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    public void Die()
    {
        if (isDead) return;

        isDead = true;
        currentHealth = 0f;
        velocity = Vector3.zero;
        acceleration = Vector3.zero;

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            if (!rb.isKinematic)
            {
                #if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                #else
                rb.velocity = Vector3.zero;
                #endif
                rb.angularVelocity = Vector3.zero;
            }
            rb.isKinematic = true;
        }

        if (meshRenderers != null)
        {
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null && meshRenderers[i].material != null)
                {
                    meshRenderers[i].material.color = Color.gray;
                }
            }
        }
    }

    public void StartDelayedRespawn()
    {
        isRespawning = true;
        respawnTimer = 0f;
        SetVisibilityAndPhysics(false);
    }

    private void CompleteRespawn()
    {
        isRespawning = false;
        currentHealth = maxHealth;
        isDead = false;
        velocity = Vector3.zero;
        acceleration = Vector3.zero;

        if (TryGetComponent<Rigidbody>(out var rb))
        {
            rb.isKinematic = false;
            #if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
            #else
            rb.velocity = Vector3.zero;
            #endif
            rb.angularVelocity = Vector3.zero;
        }

        Vector3 randomOffset = new Vector3(
            Random.Range(-spawnAreaSize.x * 0.5f, spawnAreaSize.x * 0.5f),
            0f,
            Random.Range(-spawnAreaSize.z * 0.5f, spawnAreaSize.z * 0.5f)
        );
        Vector3 newPos = spawnAreaCenter + randomOffset;
        newPos.y = initialY;
        transform.position = newPos;

        if (meshRenderers != null && originalColors != null)
        {
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i] != null && meshRenderers[i].material != null && i < originalColors.Length)
                {
                    meshRenderers[i].material.color = originalColors[i];
                }
            }
        }

        SetVisibilityAndPhysics(true);
    }

    private void SetVisibilityAndPhysics(bool state)
    {
        if (meshRenderers != null)
        {
            foreach (var r in meshRenderers)
            {
                if (r != null) r.enabled = state;
            }
        }
        if (colliders != null)
        {
            foreach (var c in colliders)
            {
                if (c != null) c.enabled = state;
            }
        }
    }

    private Vector3 CalculateAlignment(List<SteeringAgent> neighbors)
    {
        if (neighbors.Count == 0) return Vector3.zero;
        Vector3 averageVelocity = Vector3.zero;
        for (int i = 0; i < neighbors.Count; i++)
        {
            averageVelocity += neighbors[i].velocity;
        }
        averageVelocity /= neighbors.Count;
        averageVelocity.y = 0f;
        if (averageVelocity.sqrMagnitude < 0.001f) return Vector3.zero;
        Vector3 desiredVelocity = averageVelocity.normalized * maxSpeed;
        Vector3 steeringForce = desiredVelocity - velocity;
        steeringForce.y = 0f;
        return Vector3.ClampMagnitude(steeringForce, maxForce);
    }

    private Vector3 CalculateCohesion(List<SteeringAgent> neighbors)
    {
        if (neighbors.Count == 0) return Vector3.zero;
        Vector3 centerOfMass = Vector3.zero;
        for (int i = 0; i < neighbors.Count; i++)
        {
            centerOfMass += neighbors[i].transform.position;
        }
        centerOfMass /= neighbors.Count;
        Vector3 toCenter = centerOfMass - transform.position;
        toCenter.y = 0f;
        if (toCenter.sqrMagnitude < 0.001f) return Vector3.zero;
        Vector3 desiredVelocity = toCenter.normalized * maxSpeed;
        Vector3 steeringForce = desiredVelocity - velocity;
        steeringForce.y = 0f;
        return Vector3.ClampMagnitude(steeringForce, maxForce);
    }

    private Vector3 CalculateSeparation(List<SteeringAgent> neighbors)
    {
        if (neighbors.Count == 0) return Vector3.zero;
        Vector3 repulsionVector = Vector3.zero;
        int count = 0;
        for (int i = 0; i < neighbors.Count; i++)
        {
            Vector3 diff = transform.position - neighbors[i].transform.position;
            diff.y = 0f;
            float distance = diff.magnitude;
            if (distance > 0.0001f)
            {
                repulsionVector += (diff.normalized / distance);
                count++;
            }
        }
        if (count == 0 || repulsionVector.sqrMagnitude < 0.001f) return Vector3.zero;
        repulsionVector /= count;
        repulsionVector.y = 0f;
        Vector3 desiredVelocity = repulsionVector.normalized * maxSpeed;
        Vector3 steeringForce = desiredVelocity - velocity;
        steeringForce.y = 0f;
        return Vector3.ClampMagnitude(steeringForce, maxForce);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, viewRadius);
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, separationRadius);
        Gizmos.color = new Color(0.2f, 1f, 0.2f, 0.3f);
        Gizmos.DrawWireCube(spawnAreaCenter, spawnAreaSize);
    }
}
