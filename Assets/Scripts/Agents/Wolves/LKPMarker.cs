using UnityEngine;

namespace PIA1.Agents.Wolves
{
    public class LKPMarker : MonoBehaviour
    {
        [Header("Visualization")]
        [SerializeField] private Color markerColor = new Color(1f, 0.85f, 0.05f, 0.9f);
        [SerializeField] private float markerRadius = 1.0f;
        [SerializeField] private float pulseSpeed = 4.0f;
        [SerializeField] private bool showGameViewLabel = true;

        private GameObject _visualDisc;
        private Material _discMaterial;
        private bool _isVisible = false;
        private Vector3 _currentPosition;

        private GUIStyle _labelStyle;
        private GUIStyle _badgeStyle;
        private static Texture2D _badgeTexture;
        private static readonly GUIContent CachedLabelContent = new GUIContent("LKP (ÚLTIMA POSICIÓN CONOCIDA)");

        public static LKPMarker Instance { get; private set; }

        public bool IsVisible => _isVisible;
        public Vector3 Position => _currentPosition;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            CreateVisualDisc();
            Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void CreateVisualDisc()
        {
            _visualDisc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _visualDisc.name = "DiscMesh";
            _visualDisc.transform.SetParent(transform, false);
            _visualDisc.transform.localScale = new Vector3(markerRadius * 2f, 0.04f, markerRadius * 2f);

            Collider col = _visualDisc.GetComponent<Collider>();
            if (col != null) Destroy(col);

            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                _discMaterial = new Material(shader) { color = markerColor };
                Renderer r = _visualDisc.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = _discMaterial;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                }
            }
        }

        public void ShowAt(Vector3 position)
        {
            _currentPosition = position;
            transform.position = position + Vector3.up * 0.05f;
            _isVisible = true;

            if (_visualDisc != null)
            {
                _visualDisc.SetActive(true);
            }
        }

        public void Hide()
        {
            _isVisible = false;
            if (_visualDisc != null)
            {
                _visualDisc.SetActive(false);
            }
        }

        private void Update()
        {
            if (!_isVisible || _visualDisc == null) return;

            float pulse = 1f + 0.15f * Mathf.Sin(Time.time * pulseSpeed);
            _visualDisc.transform.localScale = new Vector3(markerRadius * 2f * pulse, 0.04f, markerRadius * 2f * pulse);
        }

        private void EnsureGUIStyles()
        {
            if (_badgeTexture == null)
            {
                _badgeTexture = new Texture2D(1, 1);
                _badgeTexture.SetPixel(0, 0, new Color(0.1f, 0.08f, 0.02f, 0.88f));
                _badgeTexture.Apply();
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 11
                };
            }

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(GUI.skin.box);
                _badgeStyle.normal.background = _badgeTexture;
                _badgeStyle.padding = new RectOffset(6, 6, 2, 2);
            }
        }

        private void OnGUI()
        {
            if (!showGameViewLabel || !_isVisible || Event.current.type != EventType.Repaint) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            Vector3 worldPos = _currentPosition + Vector3.up * 1.5f;
            Vector3 screenPos = cam.WorldToScreenPoint(worldPos);
            if (screenPos.z <= 0.5f) return;

            EnsureGUIStyles();

            Vector2 size = _labelStyle.CalcSize(CachedLabelContent) + new Vector2(16, 6);
            float x = screenPos.x - size.x * 0.5f;
            float y = Screen.height - screenPos.y - size.y;

            Rect rect = new Rect(x, y, size.x, size.y);
            GUI.Box(rect, GUIContent.none, _badgeStyle);

            _labelStyle.normal.textColor = markerColor;
            GUI.Label(rect, CachedLabelContent, _labelStyle);
        }

        private void OnDrawGizmos()
        {
            if (!_isVisible) return;

            Gizmos.color = markerColor;
            Gizmos.DrawWireSphere(_currentPosition + Vector3.up * 0.2f, markerRadius);
            Gizmos.DrawLine(_currentPosition, _currentPosition + Vector3.up * 2.0f);
            Gizmos.DrawWireCube(_currentPosition + Vector3.up * 2.0f, Vector3.one * 0.25f);
        }
    }
}
