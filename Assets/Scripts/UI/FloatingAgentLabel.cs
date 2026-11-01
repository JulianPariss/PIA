using UnityEngine;

namespace PIA1.UI
{
    public abstract class FloatingAgentLabel : MonoBehaviour
    {
        [SerializeField] protected float heightOffset = 2.0f;

        private GUIStyle labelStyle;
        private GUIStyle badgeStyle;
        private static Texture2D badgeTexture;

        private readonly GUIContent cachedContent = new GUIContent();
        private Vector2 cachedSize;
        private Color cachedColor = Color.white;
        private string lastText = string.Empty;

        protected abstract bool IsTargetActive();
        protected abstract void UpdateLabelContent(out string text, out Color color);

        private void Awake()
        {
            InitBadgeTexture();
        }

        private static void InitBadgeTexture()
        {
            if (badgeTexture == null)
            {
                badgeTexture = new Texture2D(1, 1);
                badgeTexture.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.08f, 0.85f));
                badgeTexture.Apply();
            }
        }

        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 11
                };
            }

            if (badgeStyle == null)
            {
                badgeStyle = new GUIStyle(GUI.skin.box);
                badgeStyle.normal.background = badgeTexture;
                badgeStyle.padding = new RectOffset(6, 6, 2, 2);
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            if (!gameObject.activeInHierarchy || !IsTargetActive()) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 screenPos = cam.WorldToScreenPoint(transform.position + Vector3.up * heightOffset);
            if (screenPos.z <= 0.5f) return;

            EnsureStyles();
            UpdateLabelContent(out string currentText, out Color currentColor);

            if (currentText != lastText || currentColor != cachedColor)
            {
                lastText = currentText;
                cachedColor = currentColor;
                cachedContent.text = currentText;
                cachedSize = labelStyle.CalcSize(cachedContent) + new Vector2(16f, 6f);
            }

            float x = screenPos.x - (cachedSize.x * 0.5f);
            float y = Screen.height - screenPos.y - cachedSize.y;
            Rect badgeRect = new Rect(x, y, cachedSize.x, cachedSize.y);

            GUI.Box(badgeRect, GUIContent.none, badgeStyle);
            labelStyle.normal.textColor = cachedColor;
            GUI.Label(badgeRect, cachedContent, labelStyle);
        }
    }
}
