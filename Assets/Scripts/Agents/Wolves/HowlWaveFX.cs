using UnityEngine;

namespace PIA1.Agents.Wolves
{
    [RequireComponent(typeof(LineRenderer))]
    public class HowlWaveFX : MonoBehaviour
    {
        private LineRenderer _line;
        private Vector3 _center;
        private Color _baseColor;
        private float _maxRadius = 16f;
        private float _duration = 1.2f;
        private float _elapsed = 0f;
        private const int Segments = 36;
        private static Material _sharedFxMaterial;

        public bool IsPlaying => gameObject.activeSelf;

        private void Awake()
        {
            SetupLineRenderer();
        }

        public void Play(Vector3 origin, Color color, float maxRadius = 16f, float duration = 1.2f)
        {
            _center = origin;
            _baseColor = color;
            _maxRadius = maxRadius;
            _duration = duration;
            _elapsed = 0f;

            if (_line == null)
            {
                SetupLineRenderer();
            }

            gameObject.SetActive(true);
            UpdateCircle(0.5f, 0.85f);
        }

        private void SetupLineRenderer()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.startWidth = 0.25f;
            _line.endWidth = 0.25f;
            _line.loop = true;
            _line.positionCount = Segments;
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;

            if (_sharedFxMaterial == null)
            {
                Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
                if (shader != null)
                {
                    _sharedFxMaterial = new Material(shader);
                }
            }

            if (_sharedFxMaterial != null)
            {
                _line.sharedMaterial = _sharedFxMaterial;
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);

            float currentRadius = Mathf.Lerp(0.5f, _maxRadius, Mathf.Sqrt(t));
            float alpha = Mathf.Lerp(0.85f, 0f, t * t);

            UpdateCircle(currentRadius, alpha);

            if (_elapsed >= _duration)
            {
                gameObject.SetActive(false);
            }
        }

        private void UpdateCircle(float radius, float alpha)
        {
            if (_line == null) return;

            Color c = _baseColor;
            c.a = alpha;
            _line.startColor = c;
            _line.endColor = c;

            float angleStep = 360f / Segments;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                float x = _center.x + Mathf.Cos(angle) * radius;
                float z = _center.z + Mathf.Sin(angle) * radius;
                _line.SetPosition(i, new Vector3(x, _center.y + 0.12f, z));
            }
        }
    }
}
