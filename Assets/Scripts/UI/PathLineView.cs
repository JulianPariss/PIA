using System.Collections.Generic;
using UnityEngine;
using PIA1.Navigation;

namespace PIA1.UI
{
    [RequireComponent(typeof(NavigationAgent))]
    public class PathLineView : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool showAStarPath = true;
        [SerializeField] private bool showDirectLine = false;
        [SerializeField] private float heightOffset = 0.25f;
        [SerializeField] private float lineWidth = 0.14f;
        [SerializeField] private Color aStarColor = new Color(0f, 0.9f, 1f, 0.85f);
        [SerializeField] private Color directColor = new Color(0.2f, 1f, 0.3f, 0.65f);

        private NavigationAgent _navAgent;
        private LineRenderer _lineRenderer;
        private static Material _sharedLineMaterial;

        public bool ShowAStarPath { get => showAStarPath; set => showAStarPath = value; }
        public bool ShowDirectLine { get => showDirectLine; set => showDirectLine = value; }

        private void Awake()
        {
            _navAgent = GetComponent<NavigationAgent>();
            SetupLineRenderer();
        }

        private void SetupLineRenderer()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            if (_lineRenderer == null)
            {
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            }

            _lineRenderer.useWorldSpace = true;
            _lineRenderer.startWidth = lineWidth;
            _lineRenderer.endWidth = lineWidth;
            _lineRenderer.numCapVertices = 3;
            _lineRenderer.numCornerVertices = 3;
            _lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.positionCount = 0;

            if (_lineRenderer.sharedMaterial == null)
            {
                if (_sharedLineMaterial == null)
                {
                    Shader shader = Shader.Find("Sprites/Default");
                    if (shader == null)
                    {
                        shader = Shader.Find("Universal Render Pipeline/Unlit");
                    }
                    if (shader != null)
                    {
                        _sharedLineMaterial = new Material(shader);
                    }
                }

                if (_sharedLineMaterial != null)
                {
                    _lineRenderer.sharedMaterial = _sharedLineMaterial;
                }
            }
        }

        private void LateUpdate()
        {
            if (_navAgent == null || _lineRenderer == null) return;

            Vector3 offset = Vector3.up * heightOffset;
            NavigationMode mode = _navAgent.CurrentMode;

            if (mode == NavigationMode.AStar && showAStarPath)
            {
                IReadOnlyList<Node> path = _navAgent.CurrentPath;
                int startIndex = _navAgent.CurrentPathIndex;

                if (path != null && path.Count > 0 && startIndex < path.Count)
                {
                    int remainingNodes = path.Count - startIndex;
                    int totalPoints = 1 + remainingNodes;

                    _lineRenderer.startColor = aStarColor;
                    _lineRenderer.endColor = aStarColor;
                    _lineRenderer.positionCount = totalPoints;
                    _lineRenderer.SetPosition(0, transform.position + offset);

                    int pointIndex = 1;
                    for (int i = startIndex; i < path.Count; i++)
                    {
                        Node n = path[i];
                        Vector3 pos = (n != null ? n.Position : transform.position) + offset;
                        _lineRenderer.SetPosition(pointIndex++, pos);
                    }
                    return;
                }
            }
            else if (mode == NavigationMode.Direct && showDirectLine)
            {
                _lineRenderer.startColor = directColor;
                _lineRenderer.endColor = directColor;
                _lineRenderer.positionCount = 2;
                _lineRenderer.SetPosition(0, transform.position + offset);
                _lineRenderer.SetPosition(1, _navAgent.CurrentDestination + offset);
                return;
            }

            if (_lineRenderer.positionCount != 0)
            {
                _lineRenderer.positionCount = 0;
            }
        }

        private void OnDisable()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
            }
        }
    }
}
