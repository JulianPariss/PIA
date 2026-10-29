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

    /// <summary>
    /// Componente que delimita e identifica visualmente una de las zonas obligatorias
    /// de la consigna (Zona Inicial, Coto de Caza, Zona Segura, Salida).
    /// Mantiene un registro estático para consultas globales sin GC.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class ZoneArea : MonoBehaviour
    {
        [Header("Zone Configuration")]
        [SerializeField] private ZoneType zoneType = ZoneType.SafeZone;
        [SerializeField] private string zoneName = "Zona";

        [Header("Visual Gizmos")]
        [SerializeField] private bool showGizmos = true;

        private BoxCollider _boxCollider;
        private static readonly List<ZoneArea> ActiveZones = new List<ZoneArea>();

        public ZoneType Type => zoneType;
        public string ZoneName => zoneName;
        public BoxCollider Collider => _boxCollider;

        public Bounds Bounds => _boxCollider != null
            ? _boxCollider.bounds
            : new Bounds(transform.position, Vector3.one * 5f);

        private void Awake()
        {
            _boxCollider = GetComponent<BoxCollider>();
            if (_boxCollider != null)
            {
                _boxCollider.isTrigger = true;
            }
        }

        private void OnEnable()
        {
            if (!ActiveZones.Contains(this))
            {
                ActiveZones.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveZones.Remove(this);
        }

        /// <summary>
        /// Comprueba si un punto en espacio de mundo está contenido dentro de los límites de la zona.
        /// Evalúa en el plano horizontal del volumen para tolerar diferencias de altura Y entre piso/nodos/agentes.
        /// </summary>
        public bool Contains(Vector3 worldPosition)
        {
            if (_boxCollider == null)
            {
                _boxCollider = GetComponent<BoxCollider>();
            }

            if (_boxCollider == null) return false;

            // Convertimos el punto al espacio local del transform y comprobamos contra center y size/2 en XZ
            Vector3 localPos = transform.InverseTransformPoint(worldPosition) - _boxCollider.center;
            Vector3 halfSize = _boxCollider.size * 0.5f;

            return Mathf.Abs(localPos.x) <= halfSize.x + 0.01f &&
                   Mathf.Abs(localPos.z) <= halfSize.z + 0.01f;
        }

        #region Static Queries

        public static IReadOnlyList<ZoneArea> AllZones
        {
            get
            {
                EnsureZonesPopulated();
                return ActiveZones;
            }
        }

        private static void EnsureZonesPopulated()
        {
            if (!Application.isPlaying || ActiveZones.Count == 0)
            {
                ActiveZones.Clear();
                ZoneArea[] found = Object.FindObjectsByType<ZoneArea>(FindObjectsSortMode.None);
                for (int i = 0; i < found.Length; i++)
                {
                    if (found[i] != null && !ActiveZones.Contains(found[i]))
                    {
                        ActiveZones.Add(found[i]);
                    }
                }
            }
        }

        public static ZoneArea GetZoneAt(Vector3 worldPosition)
        {
            EnsureZonesPopulated();
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                if (ActiveZones[i] != null && ActiveZones[i].Contains(worldPosition))
                {
                    return ActiveZones[i];
                }
            }
            return null;
        }

        public static bool IsPositionInSafeZone(Vector3 worldPosition)
        {
            EnsureZonesPopulated();
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                ZoneArea zone = ActiveZones[i];
                if (zone != null && zone.Type == ZoneType.SafeZone && zone.Contains(worldPosition))
                {
                    return true;
                }
            }
            return false;
        }

        public static ZoneArea GetZone(ZoneType type)
        {
            EnsureZonesPopulated();
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                if (ActiveZones[i] != null && ActiveZones[i].Type == type)
                {
                    return ActiveZones[i];
                }
            }
            return null;
        }

        public static List<ZoneArea> GetSafeZones()
        {
            EnsureZonesPopulated();
            var result = new List<ZoneArea>();
            for (int i = 0; i < ActiveZones.Count; i++)
            {
                if (ActiveZones[i] != null && ActiveZones[i].Type == ZoneType.SafeZone)
                {
                    result.Add(ActiveZones[i]);
                }
            }
            return result;
        }

        public static ZoneArea GetNearestSafeZone(Vector3 fromPosition)
        {
            EnsureZonesPopulated();
            ZoneArea nearest = null;
            float minSqrDist = float.MaxValue;

            for (int i = 0; i < ActiveZones.Count; i++)
            {
                ZoneArea zone = ActiveZones[i];
                if (zone != null && zone.Type == ZoneType.SafeZone)
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

        #endregion

        #region Gizmos

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;

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
                    return new Color(0.2f, 0.6f, 1f); // Azul cielo
                case ZoneType.Coto:
                    return new Color(0.2f, 0.9f, 0.3f); // Verde
                case ZoneType.SafeZone:
                    return new Color(1f, 0.85f, 0.1f); // Amarillo / Dorado
                case ZoneType.ExitZone:
                    return new Color(0.8f, 0.2f, 0.9f); // Magenta / Violeta
                default:
                    return Color.white;
            }
        }

        #endregion
    }
}
