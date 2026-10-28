using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RutherfordSim.Bio.Tracing
{
    [Serializable]
    public class PartObjectMapping
    {
        public string partId;
        public GameObject partOverlayObject;
    }
    public class DiagramStructureContainer : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] List<PartObjectMapping> _partMappings = new List<PartObjectMapping>();
        readonly Dictionary<string, Color> _partColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        public event Action<string> OnPartSelected;
        public string SelectedPart { get; private set; }
        public bool Interactive { get; private set; }
        public void SetInteractive(bool enabled)
        {
            Interactive = enabled;
            var graphic = GetComponent<UnityEngine.UI.Graphic>();
            if (graphic != null) graphic.raycastTarget = enabled;
            if (!enabled) HideAllParts();
        }
        public void SelectPart(string partId)
        {
            SelectedPart = partId;
            foreach (var mapping in _partMappings)
                if (mapping != null && mapping.partOverlayObject != null)
                {
                    bool selected = !string.IsNullOrEmpty(partId) && string.Equals(mapping.partId, partId, StringComparison.OrdinalIgnoreCase);
                    if (selected && _partColors.TryGetValue(mapping.partId, out var color))
                        ApplyColor(mapping.partOverlayObject, color);
                    mapping.partOverlayObject.SetActive(selected);
                }
            OnPartSelected?.Invoke(partId);
        }
        public void ConfigureParts(IList<DiagramPartDefinition> parts)
        {
            _partColors.Clear();
            if (parts == null) return;
            foreach (var part in parts)
                if (part != null && !string.IsNullOrWhiteSpace(part.partId))
                    _partColors[part.partId] = part.accentColor;
        }
        static void ApplyColor(GameObject root, Color color)
        {
            foreach (var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            {
                // Keep themed label text and its background distinct and readable.
                if (graphic is TMPro.TextMeshProUGUI || graphic.gameObject.name == "LabelBackground") continue;
                graphic.color = color;
            }
            foreach (var sprite in root.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.color = color;
        }
        public void HideAllParts() => SelectPart(null);
        public void AddMapping(string partId, GameObject obj) => _partMappings.Add(new PartObjectMapping { partId = partId, partOverlayObject = obj });
        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactive || e.button != PointerEventData.InputButton.Left) return;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)transform, e.position, e.pressEventCamera, out var world)) return;
            Physics2D.SyncTransforms();
            foreach (var hotspot in GetComponentsInChildren<DiagramPartHotspot>())
                if (hotspot.Contains(world)) { SelectPart(hotspot.partId); return; }
        }
    }
}
