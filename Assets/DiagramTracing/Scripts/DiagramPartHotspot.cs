using UnityEngine;

namespace RutherfordSim.Bio.Tracing
{
    public class DiagramPartHotspot : MonoBehaviour
    {
        public string partId;
        public Collider2D hitArea;
        public bool Contains(Vector3 worldPosition)
        {
            if (!isActiveAndEnabled) return false;
            if (hitArea == null) hitArea = GetComponent<Collider2D>();
            return hitArea != null && hitArea.enabled && hitArea.OverlapPoint(worldPosition);
        }
    }
}
