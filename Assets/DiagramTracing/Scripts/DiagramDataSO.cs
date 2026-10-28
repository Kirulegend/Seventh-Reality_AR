using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace RutherfordSim.Bio.Tracing
{
    [Serializable]
    public class DiagramPartDefinition
    {
        [SerializeField] string _partId, _displayName;
        [SerializeField, TextArea(2, 5)] string _description;
        [SerializeField] Color _accentColor = new Color(0.2f, 0.7f, 1f, 1f);
        [SerializeField] List<Texture2D> _fillMasks = new List<Texture2D>();

        public string partId { get => _partId; set => _partId = value; }
        public string displayName { get => _displayName; set => _displayName = value; }
        public string description { get => _description; set => _description = value; }
        public Color accentColor { get => _accentColor; set => _accentColor = value; }
        public List<Texture2D> fillMasks { get => _fillMasks; set => _fillMasks = value; }
    }

    [Serializable]
    public class OutlinePathSegment
    {
        [SerializeField] string _segmentName;
        [SerializeField] List<Vector2> _points = new List<Vector2>();
        [SerializeField] float _strokeWidth = 14f;

        public string segmentName { get => _segmentName; set => _segmentName = value; }
        public List<Vector2> points { get => _points; set => _points = value; }
        public float strokeWidth { get => _strokeWidth; set => _strokeWidth = value; }
    }

    [CreateAssetMenu(fileName = "NewDiagramData", menuName = "RutherfordSim/Bio/Diagram Data")]
    public class DiagramDataSO : ScriptableObject
    {
        [SerializeField, FormerlySerializedAs("diagramTitle")] string _diagramTitle = "Mitochondria - Powerhouse of the Cell";
        [SerializeField, FormerlySerializedAs("diagramSummary"), TextArea(3, 6)] string _diagramSummary = "Trace the membrane and cristae of the mitochondria.";
        [SerializeField, FormerlySerializedAs("outlineTexture")] Texture2D _outlineTexture;
        [SerializeField, FormerlySerializedAs("fullColoredTexture")] Texture2D _fullColoredTexture;
        [SerializeField, FormerlySerializedAs("structurePrefab")] DiagramStructureContainer _structurePrefab;
        [SerializeField, FormerlySerializedAs("pathSegments")] List<OutlinePathSegment> _pathSegments = new List<OutlinePathSegment>();
        [SerializeField, FormerlySerializedAs("parts")] List<DiagramPartDefinition> _parts = new List<DiagramPartDefinition>();
        [SerializeField, FormerlySerializedAs("completionThreshold"), Range(0.5f, 1.0f)] float _completionThreshold = 0.9f;
        [SerializeField, FormerlySerializedAs("snapDistance")] float _snapDistance = 14f;
        [SerializeField, FormerlySerializedAs("tracingLineThickness"), Range(1f, 32f)] float _tracingLineThickness = 10f;

        public string diagramTitle { get => _diagramTitle; set => _diagramTitle = value; }
        public string diagramSummary { get => _diagramSummary; set => _diagramSummary = value; }
        public Texture2D outlineTexture { get => _outlineTexture; set => _outlineTexture = value; }
        public Texture2D fullColoredTexture { get => _fullColoredTexture; set => _fullColoredTexture = value; }
        public DiagramStructureContainer structurePrefab { get => _structurePrefab; set => _structurePrefab = value; }
        public List<OutlinePathSegment> pathSegments { get => _pathSegments; set => _pathSegments = value; }
        public List<DiagramPartDefinition> parts { get => _parts; set => _parts = value; }
        public float completionThreshold { get => _completionThreshold; set => _completionThreshold = value; }
        public float snapDistance { get => _snapDistance; set => _snapDistance = value; }
        public float tracingLineThickness { get => _tracingLineThickness; set => _tracingLineThickness = value; }

#if UNITY_EDITOR
        [ContextMenu("Extract Tracing Paths From Outline")]
        public void ExtractPathsFromOutline()
        {
            if ( !_outlineTexture ) return;
            try
            {
                _pathSegments = OutlinePathExtractor.Extract(_outlineTexture);
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
        }
#endif
    }
}
