using System.Collections.Generic;
using UnityEngine;

namespace PIA1.World
{
    public enum ZoneType
    {
        InitialZone = 0,
        Coto = 1,
        SafeZone = 2,
        ExitZone = 3,
        Corridor = 4
    }

    [RequireComponent(typeof(BoxCollider))]
    public class ZoneArea : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [SerializeField] private ZoneType zoneType = ZoneType.SafeZone;
        [SerializeField] private string zoneName = "Zona";

        [Header("Visual Gizmos")]
        [SerializeField] private bool showGizmos = true;

        private BoxCollider _boxCollider;
        private Vector3 _center;
        private Vector3 _halfSize;

        private static readonly List<ZoneArea> ActiveZones = new List<ZoneArea>();
        private static readonly List<ZoneArea> SafeZones = new List<ZoneArea>();

        public ZoneType Type => zoneType;
        public string ZoneName => zoneName;
        public BoxCollider Collider => _boxCollider;

        public Bounds Bounds => _boxCollider != null
            ? _boxCollider.bounds
            : new Bounds(transform.position, Vector3.one * 5f);

        private void Awake()
        {
            CacheCollider();
            if (_boxCollider != null)
            {
                _boxCollider.isTrigger = true;
            }
        }

        private void CacheCollider()
        {
            if (_boxCollider == null)
            {
                _boxCollider = GetComponent<BoxCollider>();
            }
            if (_boxCollider != null)
            {
                _center = _boxCollider.center;
                _halfSize = _boxCollider.size * 0.5f;
            }
        }

        private void OnEnable()
        {
            CacheCollider();

            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }

            if (zoneType == ZoneType.SafeZone && !SafeZones.Contains(this))
            {
                SafeZones.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
            SafeZones.Remove(this);
        }

        public bool Contains(Vector3 worldPosition)
        {
            if (_boxCollider == null)
            {
                CacheCollider();
                if (_boxCollider == null)
                {
                    return false;
                }
            }

            Vector3 localPos = transform.InverseTransformPoint(worldPosition) - _center;

            return Mathf.Abs(localPos.x) <= _halfSize.x + 0.01f &&
                   Mathf.Abs(localPos.z) <= _halfSize.z + 0.01f;
        }

        public static IReadOnlyList<ZoneArea> AllZones => ActiveZones;

        public static IReadOnlyList<ZoneArea> SafeZonesList => SafeZones;

        public static ZoneArea GetZoneAt(Vector3 worldPosition)
        {
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                ZoneArea zone = ActiveZones[i];
                if (zone != null && zone.Contains(worldPosition))
                {
                    return zone;
                }
            }
            return null;
        }

        public static bool IsPositionInSafeZone(Vector3 worldPosition)
        {
            for (int i = 0; i < SafeZones.Count; i++)
            {
                ZoneArea zone = SafeZones[i];
                if (zone != null && zone.Contains(worldPosition))
                {
                    return true;
                }
            }
            return false;
        }

        public static ZoneArea GetZone(ZoneType type)
        {
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                ZoneArea zone = ActiveZones[i];
                if (zone != null && zone.Type == type)
                {
                    return zone;
                }
            }
            return null;
        }

        public static List<ZoneArea> GetSafeZones()
        {
            return SafeZones;
        }

        public static ZoneArea GetNearestSafeZone(Vector3 fromPosition)
        {
            ZoneArea nearest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < SafeZones.Count; i++)
            {
                ZoneArea zone = SafeZones[i];
                if (zone != null)
                {
                    float sqrDist = (zone.Bounds.center - fromPosition).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        nearest = zone;
                    }
                }
            }
            return nearest;
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos)
            {
                return;
            }

            Color c = GetDefaultColorForType(zoneType);
            c.a = 0.25f;
            Gizmos.color = c;

            BoxCollider col = GetComponent<BoxCollider>();
            Matrix4x4 oldMatrix = Gizmos.matrix;

            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(col.center, col.size);
                c.a = 0.9f;
                Gizmos.color = c;
                Gizmos.DrawWireCube(col.center, col.size);
            }
            else
            {
                Gizmos.DrawWireCube(transform.position, Vector3.one * 5f);
            }

            Gizmos.matrix = oldMatrix;
        }

        private Color GetDefaultColorForType(ZoneType type)
        {
            switch (type)
            {
                case ZoneType.InitialZone:
                    return new Color(0.2f, 0.6f, 1f);
                case ZoneType.Coto:
                    return new Color(0.2f, 0.9f, 0.3f);
                case ZoneType.SafeZone:
                    return new Color(1f, 0.85f, 0.1f);
                case ZoneType.ExitZone:
                    return new Color(0.8f, 0.2f, 0.9f);
                default:
                    return Color.white;
            }
        }
    }
}
