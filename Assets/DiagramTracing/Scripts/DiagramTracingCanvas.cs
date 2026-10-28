using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RutherfordSim.Bio.Tracing
{
    public class DiagramTracingCanvas : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] UnityEngine.UI.RawImage _drawingRawImage;
        [SerializeField] UnityEngine.UI.RawImage _templateRawImage;
        [SerializeField] RectTransform _rectTransform;
        [SerializeField] Color _snappedInkColor = new Color(.08f, .72f, .56f, 1);
        [SerializeField, Range(1f, 32f)] float _brushRadius = 10f;
        [SerializeField] bool _useOverrideThreshold;
        [SerializeField, Range(.5f, 1)] float _overrideCompletionThreshold = .9f;
        public float TargetThreshold => Mathf.Clamp(_useOverrideThreshold ? _overrideCompletionThreshold : _data != null ? _data.completionThreshold : .9f, .5f, 1);
        public float Progress => _totalLength > 0 ? Mathf.Clamp01(_hitLength / _totalLength) : 0;
        public event Action<float> OnProgressChanged;
        public event Action OnThresholdReached;
        public event Action OnStrokeStarted;
        private bool _hasDrawnAnyStroke;
        public bool HasDrawnContent => _hitLength > 0.0001f || _hasDrawnAnyStroke;

        struct Edge { public Vector2 a, b; public int path, index; public float length; }
        readonly List<Edge> _edges = new List<Edge>();
        readonly Dictionary<Vector2Int, List<int>> _grid = new Dictionary<Vector2Int, List<int>>();
        readonly List<List<int>> _pathEdges = new List<List<int>>();
        DiagramDataSO _data;
        Texture2D _texture;
        Color32[] _pixels;
        bool[] _hits;
        Color32[] _edgeColors;
        float _totalLength, _hitLength, _cellSize;
        bool _interactive, _fired, _dirty;
        bool _eraser;
        int _pointerId = int.MinValue, _previousEdge = -1;
        Vector2? _previousPointer;

        public void Initialize(DiagramDataSO data)
        {
            ReleaseTexture();
            _data = data;
            _rectTransform = _rectTransform != null ? _rectTransform : GetComponent<RectTransform>();
            _edges.Clear(); _grid.Clear(); _pathEdges.Clear(); _totalLength = 0;
            if (data == null || data.outlineTexture == null) { _interactive = false; return; }
            if (data.pathSegments == null || data.pathSegments.Count == 0)
            {
                try { data.pathSegments = OutlinePathExtractor.Extract(data.outlineTexture); }
                catch (Exception e) { Debug.LogError("Unable to generate outline paths: " + e.Message, this); }
            }
            int w = data.outlineTexture.width, h = data.outlineTexture.height;
            _texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Tracing Ink" };
            _pixels = new Color32[w * h];
            _cellSize = Mathf.Max(4, data.snapDistance);
            if (data.pathSegments != null) foreach (var path in data.pathSegments)
            {
                var indices = new List<int>();
                int pathId = _pathEdges.Count;
                _pathEdges.Add(indices);
                if (path == null || path.points == null) continue;
                for (int i = 1; i < path.points.Count; i++)
                {
                    Vector2 a = Vector2.Scale(path.points[i - 1], new Vector2(w, h));
                    Vector2 b = Vector2.Scale(path.points[i], new Vector2(w, h));
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / 2f));
                    for (int j = 0; j < steps; j++)
                    {
                        Vector2 start = Vector2.Lerp(a, b, j / (float)steps), end = Vector2.Lerp(a, b, (j + 1f) / steps);
                        float length = Vector2.Distance(start, end);
                        if (length < .0001f) continue;
                        int id = _edges.Count;
                        _edges.Add(new Edge { a = start, b = end, length = length, path = pathId, index = indices.Count });
                        indices.Add(id); _totalLength += length;
                        Vector2Int lo = Cell(Vector2.Min(start, end)), hi = Cell(Vector2.Max(start, end));
                        for (int y = lo.y; y <= hi.y; y++) for (int x = lo.x; x <= hi.x; x++)
                        {
                            var key = new Vector2Int(x, y);
                            if (!_grid.TryGetValue(key, out var bucket)) _grid[key] = bucket = new List<int>();
                            bucket.Add(id);
                        }
                    }
                }
            }
            _hits = new bool[_edges.Count];
            _edgeColors = new Color32[_edges.Count];
            if (_templateRawImage != null) { _templateRawImage.texture = data.outlineTexture; _templateRawImage.color = Color.white; _templateRawImage.raycastTarget = false; }
            if (_drawingRawImage != null) { _drawingRawImage.texture = _texture; _drawingRawImage.color = Color.white; _drawingRawImage.raycastTarget = true; }
            _interactive = _edges.Count > 0;
            ClearCanvas();
            if (!_interactive) Debug.LogError("No tracing lines found. Use a clear outline PNG on a transparent or plain background.", this);
        }

        public void ClearCanvas()
        {
            if (_pixels != null) Array.Clear(_pixels, 0, _pixels.Length);
            if (_hits != null) Array.Clear(_hits, 0, _hits.Length);
            _hitLength = 0; _fired = false; _dirty = true;
            _hasDrawnAnyStroke = false;
            EndStroke(); OnProgressChanged?.Invoke(0); Flush();
        }
        public void ShowFullColoredTexture()
        {
            if (_data != null && _data.fullColoredTexture != null)
            {
                if (_templateRawImage != null)
                {
                    _templateRawImage.texture = _data.fullColoredTexture;
                    _templateRawImage.color = Color.white;
                }
                if (_drawingRawImage != null)
                {
                    _drawingRawImage.color = new Color(1f, 1f, 1f, 0f);
                }
                SetInteractive(false);
            }
        }

        public void ResetToOutline()
        {
            SetTemplateToOutline();
            ClearCanvas();
            SetInteractive(true);
        }

        public void SetTemplateToOutline()
        {
            if (_data != null && _data.outlineTexture != null && _templateRawImage != null)
            {
                _templateRawImage.texture = _data.outlineTexture;
                _templateRawImage.color = Color.white;
            }
            if (_drawingRawImage != null)
            {
                _drawingRawImage.color = Color.white;
            }
        }

        public void CaptureCanvasState(DiagramTracingState state)
        {
            if (state == null) return;
            state.hasDrawnContent = HasDrawnContent;
            if (_pixels != null)
            {
                state.pixels = new Color32[_pixels.Length];
                Array.Copy(_pixels, state.pixels, _pixels.Length);
            }
            if (_hits != null)
            {
                state.hits = new bool[_hits.Length];
                Array.Copy(_hits, state.hits, _hits.Length);
            }
            if (_edgeColors != null)
            {
                state.edgeColors = new Color32[_edgeColors.Length];
                Array.Copy(_edgeColors, state.edgeColors, _edgeColors.Length);
            }
            state.hitLength = _hitLength;
            state.thresholdFired = _fired;
        }

        public void RestoreCanvasState(DiagramTracingState state)
        {
            if (state == null) return;

            if (state.isSubmitted)
            {
                ShowFullColoredTexture();
            }
            else
            {
                _hasDrawnAnyStroke = state.hasDrawnContent;
                SetTemplateToOutline();
                if (state.pixels != null && _pixels != null && state.pixels.Length == _pixels.Length)
                {
                    Array.Copy(state.pixels, _pixels, _pixels.Length);
                    _dirty = true;
                    Flush();
                }
                if (state.hits != null && _hits != null && state.hits.Length == _hits.Length)
                {
                    Array.Copy(state.hits, _hits, _hits.Length);
                }
                if (state.edgeColors != null && _edgeColors != null && state.edgeColors.Length == _edgeColors.Length)
                {
                    Array.Copy(state.edgeColors, _edgeColors, _edgeColors.Length);
                }
                _hitLength = state.hitLength;
                _fired = state.thresholdFired;
                SetInteractive(true);
            }
        }

        public void SetInteractive(bool value)
        {
            bool nextState = value && _edges.Count > 0;
            if (_interactive == nextState) return;
            _interactive = nextState;
            if (_drawingRawImage != null) _drawingRawImage.raycastTarget = _interactive;
            if (!_interactive) EndStroke();
        }
        public void ResumeEditing()
        {
            _fired = false;
            SetInteractive(true);
        }
        public void SetTool(Color color, bool eraser)
        {
            _snappedInkColor = color;
            _eraser = eraser;
            EndStroke();
        }
        public void SetCanvasVisible(bool value)
        {
            if (_drawingRawImage != null) _drawingRawImage.gameObject.SetActive(value);
            if (_templateRawImage != null) _templateRawImage.gameObject.SetActive(value);
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (!_interactive || e.button != PointerEventData.InputButton.Left || _pointerId != int.MinValue) return;
            _hasDrawnAnyStroke = true;
            _pointerId = e.pointerId; _previousPointer = null; _previousEdge = -1; ReadPointer(e);
            OnStrokeStarted?.Invoke();
        }
        public void OnDrag(PointerEventData e) { if (_interactive && e.pointerId == _pointerId) ReadPointer(e); }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == _pointerId) { if (_interactive) ReadPointer(e); EndStroke(); } }
        void EndStroke() { _pointerId = int.MinValue; _previousPointer = null; _previousEdge = -1; }
        void OnDisable() => EndStroke();
        void OnDestroy() => ReleaseTexture();
        void LateUpdate() => Flush();
        void ReleaseTexture() { if (_texture != null) { if (Application.isPlaying) Destroy(_texture); else DestroyImmediate(_texture); } _texture = null; }
        void Flush() { if (!_dirty || _texture == null) return; _texture.SetPixels32(_pixels); _texture.Apply(false); _dirty = false; }
        Vector2Int Cell(Vector2 p) => new Vector2Int(Mathf.FloorToInt(p.x / _cellSize), Mathf.FloorToInt(p.y / _cellSize));
        public void SetTargetThreshold(float threshold)
        {
            _useOverrideThreshold = true;
            _overrideCompletionThreshold = Mathf.Clamp(threshold, 0.05f, 1f);
        }

        void ReadPointer(PointerEventData e)
        {
            var cam = e.pressEventCamera;
            if (cam == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null && canvas.worldCamera != null) cam = canvas.worldCamera;
                else cam = Camera.main;
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, e.position, cam, out var p)) return;
            Rect r = _rectTransform.rect;
            if (!r.Contains(p) || r.width <= 0 || r.height <= 0) { _previousPointer = null; _previousEdge = -1; return; }
            TracePixel(new Vector2((p.x - r.xMin) / r.width * _texture.width, (p.y - r.yMin) / r.height * _texture.height));
        }

        public void TracePixel(Vector2 point)
        {
            if (!_interactive || _fired) return;
            Vector2 start = _previousPointer ?? point;
            float distance = Vector2.Distance(start, point);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 2));
            float before = _hitLength;
            for (int i = 1; i <= steps; i++)
            {
                Vector2 sample = Vector2.Lerp(start, point, i / (float)steps);
                if (_eraser) { EraseNear(sample); _previousEdge = -1; continue; }
                int nearest = FindEdge(sample);
                if (nearest < 0) { _previousEdge = -1; continue; }
                if (_previousEdge >= 0 && _edges[nearest].path == _edges[_previousEdge].path)
                {
                    var path = _pathEdges[_edges[nearest].path];
                    int from = _edges[_previousEdge].index, to = _edges[nearest].index;
                    float arc = 0;
                    for (int j = Mathf.Min(from, to); j <= Mathf.Max(from, to) && arc <= 12; j++) arc += _edges[path[j]].length;
                    if (arc <= 12) for (int j = Mathf.Min(from, to); j <= Mathf.Max(from, to); j++) PaintEdge(path[j]);
                    else PaintEdge(nearest);
                }
                else PaintEdge(nearest);
                _previousEdge = nearest;
            }
            _previousPointer = point;
            if (_eraser && _hitLength < before) OnProgressChanged?.Invoke(Progress);
            if (_hitLength > before)
            {
                OnProgressChanged?.Invoke(Progress);
                if (!_fired && Progress + .00001f >= TargetThreshold)
                { _fired = true; Flush(); OnThresholdReached?.Invoke(); }
            }
        }
        int FindEdge(Vector2 point)
        {
            var cell = Cell(point);
            float limit = Mathf.Max(1, _data.snapDistance), best = limit * limit;
            int result = -1;
            for (int y = -1; y <= 1; y++) for (int x = -1; x <= 1; x++)
                if (_grid.TryGetValue(cell + new Vector2Int(x, y), out var bucket)) foreach (int id in bucket)
                {
                    var edge = _edges[id];
                    float d = (point - Closest(point, edge.a, edge.b)).sqrMagnitude;
                    if (_previousEdge >= 0 && edge.path != _edges[_previousEdge].path) d += 4;
                    if (d <= best) { best = d; result = id; }
                }
            return result;
        }
        void PaintEdge(int id)
        {
            if (_hits[id]) return;
            _hits[id] = true;
            _edgeColors[id] = _snappedInkColor;
            _hitLength += _edges[id].length;
            DrawEdge(id, _edgeColors[id]);
        }
        void EraseNear(Vector2 point)
        {
            float inkRadius = Mathf.Max(1f, (_data != null ? _data.tracingLineThickness : _brushRadius) * .5f);
            float radius = Mathf.Max(8f, inkRadius * 2.4f);
            float edgeRadius = Mathf.Max(2f, radius - inkRadius - 1f);
            int reach = Mathf.CeilToInt(edgeRadius / _cellSize) + 1;
            Vector2Int cell = Cell(point);
            for (int y = -reach; y <= reach; y++)
                for (int x = -reach; x <= reach; x++)
                    if (_grid.TryGetValue(cell + new Vector2Int(x, y), out var bucket))
                        foreach (int id in bucket)
                        {
                            if (!_hits[id]) continue;
                            var edge = _edges[id];
                            if (Vector2.Distance(point, Closest(point, edge.a, edge.b)) > edgeRadius) continue;
                            _hits[id] = false;
                            _hitLength = Mathf.Max(0f, _hitLength - edge.length);
                        }
            int minX = Mathf.Max(0, Mathf.FloorToInt(point.x - radius));
            int maxX = Mathf.Min(_texture.width - 1, Mathf.CeilToInt(point.x + radius));
            int minY = Mathf.Max(0, Mathf.FloorToInt(point.y - radius));
            int maxY = Mathf.Min(_texture.height - 1, Mathf.CeilToInt(point.y + radius));
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                {
                    int index = y * _texture.width + x;
                    if (_pixels[index].a == 0) continue;
                    float coverage = Mathf.Clamp01(radius + .5f - Vector2.Distance(new Vector2(x + .5f, y + .5f), point));
                    if (coverage <= 0f) continue;
                    var pixel = _pixels[index];
                    pixel.a = (byte)Mathf.RoundToInt(Mathf.Max(0f, pixel.a - coverage * 255f));
                    _pixels[index] = pixel.a == 0 ? new Color32(0, 0, 0, 0) : pixel;
                    _dirty = true;
                }
        }
        void DrawEdge(int id, Color32 ink)
        {
            var edge = _edges[id];
            float radius = Mathf.Max(1f, _data != null && _data.tracingLineThickness > 0f
                ? _data.tracingLineThickness * .5f
                : _brushRadius * .5f);
            int w = _texture.width, h = _texture.height;
            Vector2 lo = Vector2.Min(edge.a, edge.b) - Vector2.one * (radius + 1), hi = Vector2.Max(edge.a, edge.b) + Vector2.one * (radius + 1);
            for (int y = Mathf.Max(0, Mathf.FloorToInt(lo.y)); y <= Mathf.Min(h - 1, Mathf.CeilToInt(hi.y)); y++)
                for (int x = Mathf.Max(0, Mathf.FloorToInt(lo.x)); x <= Mathf.Min(w - 1, Mathf.CeilToInt(hi.x)); x++)
                {
                    Vector2 p = new Vector2(x + .5f, y + .5f);
                    float alpha = Mathf.Clamp01(radius + .5f - Vector2.Distance(p, Closest(p, edge.a, edge.b)));
                    int index = y * w + x;
                    if (alpha * 255 <= _pixels[index].a) continue;
                    Color c = ink; c.a = alpha * (ink.a / 255f); _pixels[index] = c;
                }
            _dirty = true;
        }
        static Vector2 Closest(Vector2 p, Vector2 a, Vector2 b)
        { Vector2 d = b - a; return a + d * Mathf.Clamp01(Vector2.Dot(p - a, d) / Mathf.Max(.000001f, d.sqrMagnitude)); }
    }
}
