using System;
using System.Collections.Generic;
using UnityEngine;

namespace RutherfordSim.Bio.Tracing
{
    [Serializable]
    public class DiagramPartDefinition
    {
        public string partId;
        public string displayName;
        [TextArea(2, 5)] public string description;
        public Color accentColor = new Color(0.2f, 0.7f, 1f, 1f);
        [Tooltip("Transparent PNG masks for areas the player may paint. Each mask must match the outline PNG's dimensions and alignment, and have Read/Write enabled.")]
        public List<Texture2D> fillMasks = new List<Texture2D>();
    }

    [Serializable]
    public class OutlinePathSegment
    {
        public string segmentName;
        public List<Vector2> points = new List<Vector2>();
        public float strokeWidth = 14f;
    }

    [CreateAssetMenu(fileName = "NewDiagramData", menuName = "RutherfordSim/Bio/Diagram Data")]
    public class DiagramDataSO : ScriptableObject
    {
        [Header("Diagram Information")]
        public string diagramTitle = "Mitochondria - Powerhouse of the Cell";
        [TextArea(3, 6)] public string diagramSummary = "Trace the membrane and cristae of the mitochondria.";

        [Header("Textures")]
        public Texture2D outlineTexture;
        public Texture2D fullColoredTexture;
        public DiagramStructureContainer structurePrefab;

        [Header("Snappy Tracing Paths")]
        public List<OutlinePathSegment> pathSegments = new List<OutlinePathSegment>();

        [Header("Structure Parts List")]
        public List<DiagramPartDefinition> parts = new List<DiagramPartDefinition>();

        [Header("Completion Settings")]
        [Range(0.5f, 1.0f)] public float completionThreshold = 0.9f;
        public float snapDistance = 14f;

        [Header("Tracing Appearance")]
        [Tooltip("Thickness of the colored tracing line in source-image pixels.")]
        [Range(1f, 32f)] public float tracingLineThickness = 10f;
    }
}

