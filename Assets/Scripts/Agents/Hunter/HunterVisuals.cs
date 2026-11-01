using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PIA1.Agents.Wolves;

[RequireComponent(typeof(HunterNPC))]
public class HunterVisuals : MonoBehaviour
{
    [SerializeField] private LineRenderer targetLine;
    [SerializeField] private LineRenderer attackBeam;
    [SerializeField] private LineRenderer wolfAlertLine;

    private HunterNPC hunter;
    private Vector3 originalScale;
    private Coroutine meleePunchCoroutine;
    private Coroutine rangedBeamCoroutine;

    private readonly List<Renderer> _bodyRenderers = new List<Renderer>(8);
    private MaterialPropertyBlock _propBlock;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public HunterVisualMode CurrentVisualMode { get; private set; } = HunterVisualMode.Search;

    private void Awake()
    {
        hunter = GetComponent<HunterNPC>();
        originalScale = transform.localScale;
        _propBlock = new MaterialPropertyBlock();

        CacheBodyRenderers();
        SetupLineRenderers();

        if (hunter != null)
        {
            hunter.OnActionExecuted += HandleActionExecuted;
        }
    }

    private void OnDestroy()
    {
        if (hunter != null)
        {
            hunter.OnActionExecuted -= HandleActionExecuted;
        }
    }

    private void CacheBodyRenderers()
    {
        _bodyRenderers.Clear();
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Renderer r = all[i];
            if (r == null || r is LineRenderer) continue;
            _bodyRenderers.Add(r);
        }
    }

    public void SetRenderersEnabled(bool isEnabled)
    {
        for (int i = 0; i < _bodyRenderers.Count; i++)
        {
            if (_bodyRenderers[i] != null)
            {
                _bodyRenderers[i].enabled = isEnabled;
            }
        }
    }

    public void SetVisualMode(HunterVisualMode mode)
    {
        CurrentVisualMode = mode;
        if (_bodyRenderers.Count == 0) return;

        Color targetColor = mode switch
        {
            HunterVisualMode.Search => new Color(0.25f, 0.9f, 0.35f),
            HunterVisualMode.Pursuit => new Color(0.1f, 0.85f, 0.75f),
            HunterVisualMode.Attack => new Color(1f, 0.2f, 0.2f),
            HunterVisualMode.Gather => new Color(1f, 0.88f, 0.1f),
            HunterVisualMode.Flee => new Color(1f, 0.5f, 0.1f),
            HunterVisualMode.Exit => new Color(0.75f, 0.3f, 1f),
            HunterVisualMode.Dead => new Color(0.35f, 0.35f, 0.35f),
            _ => Color.white
        };

        if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

        for (int i = 0; i < _bodyRenderers.Count; i++)
        {
            Renderer r = _bodyRenderers[i];
            if (r != null && r.sharedMaterial != null)
            {
                r.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(BaseColorId, targetColor);
                _propBlock.SetColor(ColorId, targetColor);
                r.SetPropertyBlock(_propBlock);
            }
        }
    }

    private void SetupLineRenderers()
    {
        if (targetLine == null)
        {
            GameObject targetLineObj = new GameObject("TargetLine");
            targetLineObj.transform.SetParent(transform, false);
            targetLine = targetLineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(targetLine, new Color(1f, 0.2f, 0.2f, 0.6f), 0.05f);
            targetLine.enabled = false;
        }

        if (attackBeam == null)
        {
            GameObject attackBeamObj = new GameObject("AttackBeam");
            attackBeamObj.transform.SetParent(transform, false);
            attackBeam = attackBeamObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(attackBeam, new Color(1f, 0.9f, 0.1f, 0.95f), 0.12f);
            attackBeam.enabled = false;
        }

        if (wolfAlertLine == null)
        {
            GameObject wolfAlertObj = new GameObject("WolfAlertLine");
            wolfAlertObj.transform.SetParent(transform, false);
            wolfAlertLine = wolfAlertObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(wolfAlertLine, new Color(1f, 0.45f, 0.05f, 0.85f), 0.06f);
            wolfAlertLine.enabled = false;
        }
    }

    private void ConfigureLineRenderer(LineRenderer lr, Color color, float width)
    {
        lr.startWidth = width;
        lr.endWidth = width * 0.6f;
        lr.positionCount = 2;
        lr.useWorldSpace = true;
        lr.startColor = color;
        lr.endColor = color;

        Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (unlitShader != null)
        {
            lr.sharedMaterial = new Material(unlitShader);
        }
    }

    private void LateUpdate()
    {
        if (hunter == null || hunter.IsDead)
        {
            if (targetLine != null) targetLine.enabled = false;
            if (attackBeam != null) attackBeam.enabled = false;
            if (wolfAlertLine != null) wolfAlertLine.enabled = false;
            return;
        }

        UpdateTargetLine();
        UpdateWolfAlertLine();
    }

    private void UpdateTargetLine()
    {
        if (targetLine == null) return;

        Boid target = hunter.CurrentTarget;
        if (target != null && !target.isRespawning && hunter.IsInVision(target))
        {
            targetLine.enabled = true;
            Vector3 startPos = transform.position + Vector3.up * 0.8f;
            Vector3 endPos = target.transform.position + Vector3.up * 0.5f;

            targetLine.SetPosition(0, startPos);
            targetLine.SetPosition(1, endPos);

            Color lineColor = CurrentVisualMode switch
            {
                HunterVisualMode.Attack => Color.red,
                HunterVisualMode.Gather => new Color(1f, 0.88f, 0.1f, 0.85f),
                _ => new Color(0.1f, 0.85f, 0.75f, 0.7f)
            };

            targetLine.startColor = lineColor;
            targetLine.endColor = lineColor;
        }
        else
        {
            targetLine.enabled = false;
        }
    }

    private void UpdateWolfAlertLine()
    {
        if (wolfAlertLine == null) return;

        Wolf wolf = hunter.DetectedWolf;
        if (wolf != null && wolf.gameObject.activeInHierarchy && !hunter.IsInSafeZone)
        {
            wolfAlertLine.enabled = true;
            wolfAlertLine.SetPosition(0, transform.position + Vector3.up * hunter.EyeHeight);
            wolfAlertLine.SetPosition(1, wolf.transform.position + Vector3.up * hunter.EyeHeight);
        }
        else
        {
            wolfAlertLine.enabled = false;
        }
    }

    private void HandleActionExecuted(string actionName, Boid target)
    {
        if (actionName == "Melee")
        {
            if (meleePunchCoroutine != null) StopCoroutine(meleePunchCoroutine);
            meleePunchCoroutine = StartCoroutine(MeleePunchRoutine());
        }
        else if (actionName == "Ranged" && target != null)
        {
            if (rangedBeamCoroutine != null) StopCoroutine(rangedBeamCoroutine);
            rangedBeamCoroutine = StartCoroutine(RangedBeamRoutine(target.transform.position + Vector3.up * 0.5f));
        }
    }

    private IEnumerator MeleePunchRoutine()
    {
        float duration = 0.18f;
        float elapsed = 0f;
        Vector3 punchScale = originalScale * 1.35f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            transform.localScale = (t < 0.5f)
                ? Vector3.Lerp(originalScale, punchScale, t * 2f)
                : Vector3.Lerp(punchScale, originalScale, (t - 0.5f) * 2f);
            yield return null;
        }

        transform.localScale = originalScale;
        meleePunchCoroutine = null;
    }

    private IEnumerator RangedBeamRoutine(Vector3 targetPos)
    {
        if (attackBeam == null) yield break;

        attackBeam.enabled = true;
        float duration = 0.28f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 startPos = transform.position + Vector3.up * 0.8f;
            attackBeam.SetPosition(0, startPos);
            attackBeam.SetPosition(1, targetPos);
            yield return null;
        }

        attackBeam.enabled = false;
        rangedBeamCoroutine = null;
    }
}
