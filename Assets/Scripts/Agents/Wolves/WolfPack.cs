using System;
using System.Collections.Generic;
using UnityEngine;
using PIA1.World;

namespace PIA1.Agents.Wolves
{
    public class WolfPack : MonoBehaviour
    {
        public static WolfPack Instance { get; private set; }

        [Header("Pack Configuration")]
        [Tooltip("Si es true, el aullido alerta a todos los lobos del mapa. Si es false, solo dentro del radio de audición.")]
        [SerializeField] private bool globalHowlRange = true;
        [Tooltip("Radio de audición del aullido en metros si globalHowlRange es false.")]
        [SerializeField] private float howlAuditionRadius = 25f;

        [Header("Target & References")]
        [SerializeField] private HunterNPC targetHunter;
        [SerializeField] private LKPMarker lkpMarker;

        [Header("Visual & Effects (0-GC Pool)")]
        [SerializeField] private bool enableHowlWaveFX = true;
        [SerializeField] private Color howlWaveColor = new Color(1f, 0.9f, 0.2f, 0.85f);
        [SerializeField] private int initialWavePoolSize = 4;

        private readonly List<Wolf> _wolves = new List<Wolf>(8);
        private readonly List<HowlWaveFX> _wavePool = new List<HowlWaveFX>(6);
        private Vector3 _currentLKP;
        private bool _hasActiveLKP = false;
        private Wolf _lastHowler;
        private float _lastHowlTimestamp = -999f;

        public event Action<Wolf, Vector3> OnHowlAlert;
        public event Action OnLKPDiscarded;

        public IReadOnlyList<Wolf> Wolves => _wolves;
        public bool GlobalHowlRange => globalHowlRange;
        public float HowlAuditionRadius => howlAuditionRadius;
        public bool HasActiveLKP => _hasActiveLKP;
        public Vector3 CurrentLKP => _currentLKP;
        public Wolf LastHowler => _lastHowler;
        public float LastHowlTimestamp => _lastHowlTimestamp;

        public void RegisterWolf(Wolf wolf)
        {
            if (wolf != null && !_wolves.Contains(wolf))
            {
                _wolves.Add(wolf);
            }
        }

        public void UnregisterWolf(Wolf wolf)
        {
            if (wolf != null)
            {
                _wolves.Remove(wolf);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureLKPMarker();
            PrewarmWavePool();
        }

        private void Start()
        {
            if (targetHunter == null)
            {
                targetHunter = HunterNPC.Instance;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            OnHowlAlert = null;
            OnLKPDiscarded = null;
        }

        private void Update()
        {
            if (!_hasActiveLKP) return;

            if (targetHunter == null)
            {
                targetHunter = HunterNPC.Instance;
            }

            if (targetHunter != null && targetHunter.IsDead)
            {
                DiscardLKP("Cazador capturado");
                return;
            }

            if (targetHunter != null && ZoneArea.IsPositionInSafeZone(targetHunter.transform.position))
            {
                DiscardLKP("Cazador ingresó a Zona Segura");
                return;
            }

            if (Time.time - _lastHowlTimestamp > 0.2f && !AnyWolfUsingLKP())
            {
                DiscardLKP("Manada finalizó búsqueda y retomó patrullaje");
            }
        }

        private bool AnyWolfUsingLKP()
        {
            for (int i = 0; i < _wolves.Count; i++)
            {
                Wolf w = _wolves[i];
                if (w == null || !w.gameObject.activeInHierarchy) continue;

                if (w.HasLastKnownPosition || w.HasPendingAlert ||
                    (w.FSM != null && w.FSM.CurrentKey != WolfStateId.Patrol))
                {
                    return true;
                }
            }
            return false;
        }

        public void HandleWolfHowled(Wolf howler, Vector3 lkp)
        {
            if (howler == null) return;

            if (ZoneArea.IsPositionInSafeZone(lkp))
            {
                return;
            }

            _currentLKP = lkp;
            _hasActiveLKP = true;
            _lastHowler = howler;
            _lastHowlTimestamp = Time.time;

            EnsureLKPMarker();
            if (lkpMarker != null)
            {
                lkpMarker.ShowAt(lkp);
            }

            if (enableHowlWaveFX)
            {
                PlayPooledHowlWave(howler.transform.position);
            }

            OnHowlAlert?.Invoke(howler, lkp);
        }

        public void NotifyWolfHowl(Wolf howler, Vector3 lkp)
        {
            HandleWolfHowled(howler, lkp);
        }

        public void DiscardLKP(string reason = "")
        {
            if (!_hasActiveLKP) return;

            _hasActiveLKP = false;

            if (lkpMarker != null)
            {
                lkpMarker.Hide();
            }

            for (int i = 0; i < _wolves.Count; i++)
            {
                if (_wolves[i] != null)
                {
                    _wolves[i].ClearMemoryAndAlerts();
                }
            }

            OnLKPDiscarded?.Invoke();
        }

        private void EnsureLKPMarker()
        {
            if (lkpMarker == null)
            {
                lkpMarker = LKPMarker.Instance;
                if (lkpMarker == null)
                {
                    GameObject go = new GameObject("[LKP_Marker]");
                    go.transform.SetParent(transform, false);
                    lkpMarker = go.AddComponent<LKPMarker>();
                }
            }
        }

        private void PrewarmWavePool()
        {
            int count = Mathf.Max(2, initialWavePoolSize);
            for (int i = _wavePool.Count; i < count; i++)
            {
                CreatePooledWave();
            }
        }

        private HowlWaveFX CreatePooledWave()
        {
            GameObject fxGO = new GameObject($"[FX_HowlWave_{_wavePool.Count}]");
            fxGO.transform.SetParent(transform, false);
            HowlWaveFX wave = fxGO.AddComponent<HowlWaveFX>();
            fxGO.SetActive(false);
            _wavePool.Add(wave);
            return wave;
        }

        private void PlayPooledHowlWave(Vector3 origin)
        {
            HowlWaveFX selected = null;
            for (int i = 0; i < _wavePool.Count; i++)
            {
                if (_wavePool[i] != null && !_wavePool[i].IsPlaying)
                {
                    selected = _wavePool[i];
                    break;
                }
            }

            if (selected == null)
            {
                selected = CreatePooledWave();
            }

            selected.Play(origin, howlWaveColor, maxRadius: globalHowlRange ? 16f : howlAuditionRadius);
        }
    }
}
