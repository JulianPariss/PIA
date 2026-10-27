using System.Collections;
using UnityEngine;

/// <summary>
/// Componente de feedback visual para el NPC Cazador.
/// Administra la línea de fijación hacia el objetivo activo, el haz de disparo para ataques a distancia,
/// el impacto de escala para ataques cuerpo a cuerpo y la coloración vía MaterialPropertyBlock.
/// </summary>
[RequireComponent(typeof(HunterNPC))]
public class HunterVisuals : MonoBehaviour
{
    [Header("Renderers y Materiales")]
    [SerializeField] private LineRenderer targetLine;
    [SerializeField] private LineRenderer attackBeam;

    private HunterNPC hunter;
    private Vector3 originalScale;
    private Coroutine meleePunchCoroutine;
    private Coroutine rangedBeamCoroutine;

    private void Awake()
    {
        hunter = GetComponent<HunterNPC>();
        originalScale = transform.localScale;

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

    private void SetupLineRenderers()
    {
        if (targetLine == null)
        {
            GameObject targetLineObj = new GameObject("TargetLine");
            targetLineObj.transform.SetParent(transform, false);
            targetLine = targetLineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(targetLine, new Color(1f, 0.2f, 0.2f, 0.6f), 0.05f);
        }

        if (attackBeam == null)
        {
            GameObject attackBeamObj = new GameObject("AttackBeam");
            attackBeamObj.transform.SetParent(transform, false);
            attackBeam = attackBeamObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(attackBeam, new Color(1f, 0.9f, 0.1f, 0.95f), 0.12f);
            attackBeam.enabled = false;
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

        // Shader estándar unlit / sprite default
        Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (unlitShader != null)
        {
            lr.sharedMaterial = new Material(unlitShader);
        }
    }

    private void LateUpdate()
    {
        UpdateTargetLine();
    }

    private void UpdateTargetLine()
    {
        if (targetLine == null || hunter == null) return;

        Boid target = hunter.CurrentTarget;
        if (target != null && !target.isRespawning && hunter.IsInVision(target))
        {
            targetLine.enabled = true;
            Vector3 startPos = transform.position + Vector3.up * 0.8f;
            Vector3 endPos = target.transform.position + Vector3.up * 0.5f;

            targetLine.SetPosition(0, startPos);
            targetLine.SetPosition(1, endPos);

            Color lineColor = hunter.CurrentVisualMode switch
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
