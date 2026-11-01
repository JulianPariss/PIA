using System.Collections.Generic;
using UnityEngine;
using PIA1.Agents.Wolves;
using PIA1.World;

namespace PIA1.Sensors
{
    [RequireComponent(typeof(HunterNPC))]
    public class HunterSensor : MonoBehaviour
    {
        [SerializeField] private AgentSensor agentSensor;
        [SerializeField] private LayerMask obstacleMask;
        [SerializeField] private float eyeHeight = 0.8f;
        [SerializeField] private float targetEyeHeight = 0.5f;

        private HunterNPC _hunter;
        public Wolf DetectedWolf { get; private set; }

        public AgentSensor Sensor => agentSensor;

        private void Awake()
        {
            _hunter = GetComponent<HunterNPC>();
            if (agentSensor == null)
            {
                agentSensor = GetComponentInChildren<AgentSensor>();
            }
        }

        public void Configure(AgentSensor sensor, float radius, float eyeH, LayerMask mask)
        {
            agentSensor = sensor;
            eyeHeight = eyeH;
            obstacleMask = mask;
            if (agentSensor != null)
            {
                agentSensor.SetRadius(radius);
            }
        }

        public Wolf DetectVisibleWolf()
        {
            Wolf closestWolf = null;
            float visionRadius = _hunter != null ? _hunter.VisionRadius : 14f;
            float minSqrDist = visionRadius * visionRadius;
            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;

            IReadOnlyList<Wolf> wolves = (agentSensor != null && agentSensor.Wolves.Count > 0)
                ? agentSensor.Wolves
                : (WolfPack.Instance != null ? WolfPack.Instance.Wolves : null);

            if (wolves == null)
            {
                DetectedWolf = null;
                return null;
            }

            for (int i = 0; i < wolves.Count; i++)
            {
                Wolf w = wolves[i];
                if (w == null || !w.gameObject.activeInHierarchy) continue;

                Vector3 diff = w.transform.position - transform.position;
                diff.y = 0f;
                float sqrDist = diff.sqrMagnitude;
                if (sqrDist > minSqrDist) continue;

                float wolfEyeH = w.FOV != null ? w.FOV.EyeHeight : eyeHeight;
                Vector3 wolfEyePos = w.transform.position + Vector3.up * wolfEyeH;

                if (LineOfSight.HasLineOfSight(eyePos, wolfEyePos, obstacleMask))
                {
                    minSqrDist = sqrDist;
                    closestWolf = w;
                }
            }

            DetectedWolf = closestWolf;
            return closestWolf;
        }

        public bool ShouldFleeFromWolf()
        {
            if (_hunter != null && _hunter.IsInSafeZone)
            {
                DetectedWolf = null;
                return false;
            }

            return DetectVisibleWolf() != null;
        }

        public Boid SelectAttackTarget(Boid lastDamagedTarget)
        {
            if (lastDamagedTarget != null && lastDamagedTarget.IsAlive && !lastDamagedTarget.isRespawning && IsInVision(lastDamagedTarget))
            {
                return lastDamagedTarget;
            }

            if (agentSensor == null) return null;

            Boid closest = null;
            float minSqr = float.MaxValue;
            float visionRadius = _hunter != null ? _hunter.VisionRadius : 14f;
            float maxSqrDist = visionRadius * visionRadius;
            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            var boids = agentSensor.Boids;

            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || !b.IsAlive || b.isRespawning) continue;

                Vector3 diff = b.transform.position - transform.position;
                diff.y = 0f;
                float sqr = diff.sqrMagnitude;
                if (sqr > maxSqrDist) continue;

                if (sqr < minSqr)
                {
                    Vector3 targetCenter = b.transform.position + Vector3.up * targetEyeHeight;
                    if (LineOfSight.HasLineOfSight(eyePos, targetCenter, obstacleMask))
                    {
                        minSqr = sqr;
                        closest = b;
                    }
                }
            }

            return closest;
        }

        public Boid FindCollectableBoid()
        {
            if (agentSensor == null) return null;

            Boid closest = null;
            float minSqr = float.MaxValue;
            float visionRadius = _hunter != null ? _hunter.VisionRadius : 14f;
            float maxSqrDist = visionRadius * visionRadius;
            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            var boids = agentSensor.Boids;

            for (int i = 0; i < boids.Count; i++)
            {
                Boid b = boids[i];
                if (b == null || !b.isDead || b.isRespawning) continue;

                Vector3 diff = b.transform.position - transform.position;
                diff.y = 0f;
                float sqr = diff.sqrMagnitude;
                if (sqr > maxSqrDist) continue;

                if (sqr < minSqr)
                {
                    Vector3 targetCenter = b.transform.position + Vector3.up * targetEyeHeight;
                    if (LineOfSight.HasLineOfSight(eyePos, targetCenter, obstacleMask))
                    {
                        minSqr = sqr;
                        closest = b;
                    }
                }
            }

            return closest;
        }

        public bool IsInVision(Boid boid)
        {
            if (boid == null || !boid.gameObject.activeInHierarchy || boid.isRespawning) return false;

            float visionRadius = _hunter != null ? _hunter.VisionRadius : 14f;
            Vector3 diff = boid.transform.position - transform.position;
            diff.y = 0f;
            if (diff.sqrMagnitude > visionRadius * visionRadius) return false;

            if (agentSensor != null)
            {
                var list = agentSensor.Boids;
                bool found = false;
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] == boid)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            Vector3 eyePos = transform.position + Vector3.up * eyeHeight;
            Vector3 targetCenter = boid.transform.position + Vector3.up * targetEyeHeight;
            return LineOfSight.HasLineOfSight(eyePos, targetCenter, obstacleMask);
        }

        public void Clear()
        {
            DetectedWolf = null;
            if (agentSensor != null)
            {
                agentSensor.Clear();
            }
        }
    }
}
