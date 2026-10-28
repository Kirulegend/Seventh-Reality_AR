using System;
using System.Collections.Generic;
using UnityEngine;

namespace RutherfordSim.Bio.Tracing
{
    public static class OutlinePathExtractor
    {
        static readonly int[] DX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        static readonly int[] DY = { 1, 1, 0, -1, -1, -1, 0, 1 };

        public static List<OutlinePathSegment> Extract(Texture2D texture)
        {
            if (texture == null) throw new ArgumentNullException(nameof(texture));
            int w = texture.width, h = texture.height;
            if (Mathf.Max(w, h) > 2048)
                throw new ArgumentException("Import the outline at 2048 pixels or less for path generation.");
            Color32[] pixels;
            if (texture.isReadable) pixels = texture.GetPixels32();
            else
            {
                var previous = RenderTexture.active;
                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                try
                {
                    Graphics.Blit(texture, rt);
                    RenderTexture.active = rt;
                    copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    copy.Apply();
                    pixels = copy.GetPixels32();
                }
                finally
                {
                    RenderTexture.active = previous;
                    RenderTexture.ReleaseTemporary(rt);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(copy);
                    else UnityEngine.Object.DestroyImmediate(copy);
                }
            }
            return Extract(pixels, w, h);
        }

        public static List<OutlinePathSegment> Extract(Color32[] pixels, int w, int h)
        {
            if (pixels == null || pixels.Length != w * h || w < 3 || h < 3)
                throw new ArgumentException("Invalid outline dimensions.");
            int transparent = 0;
            double border = 0;
            int borderCount = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                var c = pixels[y * w + x];
                if (c.a < 32) transparent++;
                if (x == 0 || y == 0 || x == w - 1 || y == h - 1)
                { border += (c.r + c.g + c.b) / 765.0; borderCount++; }
            }
            bool alphaOutline = transparent > pixels.Length / 20;
            bool darkBackground = border / borderCount < .5;
            int width = w + 2, height = h + 2;
            var mask = new bool[width * height];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                Color32 c = pixels[y * w + x];
                float light = (c.r + c.g + c.b) / 765f;
                mask[(y + 1) * width + x + 1] = c.a > 64 &&
                    (alphaOutline || (darkBackground ? light > .55f : light < .65f));
            }
            Thin(mask, width, height);
            var adjacency = new Dictionary<int, List<int>>();
            for (int y = 1; y < height - 1; y++) for (int x = 1; x < width - 1; x++)
            {
                int p = y * width + x;
                if (!mask[p]) continue;
                var neighbors = new List<int>(8);
                for (int d = 0; d < 8; d++)
                {
                    int q = p + DY[d] * width + DX[d];
                    if (!mask[q]) continue;
                    if (DX[d] != 0 && DY[d] != 0 &&
                        (mask[p + DX[d]] || mask[p + DY[d] * width])) continue;
                    neighbors.Add(q);
                }
                adjacency[p] = neighbors;
            }
            var visited = new HashSet<ulong>();
            var paths = new List<OutlinePathSegment>();
            foreach (var entry in adjacency)
                if (entry.Value.Count != 2)
                    foreach (int next in entry.Value) Walk(entry.Key, next);
            foreach (var entry in adjacency)
                foreach (int next in entry.Value) Walk(entry.Key, next);
            return paths;

            void Walk(int first, int next)
            {
                if (visited.Contains(Edge(first, next))) return;
                var points = new List<Vector2> { Point(first) };
                int previous = first, current = next;
                while (true)
                {
                    if (!visited.Add(Edge(previous, current))) break;
                    points.Add(Point(current));
                    if (current == first || adjacency[current].Count != 2) break;
                    var neighbors = adjacency[current];
                    int following = neighbors[0] == previous ? neighbors[1] : neighbors[0];
                    previous = current; current = following;
                }
                if (points.Count < 3) return;
                bool closed = (points[0] - points[points.Count - 1]).sqrMagnitude < .001f;
                for (int pass = 0; pass < 3; pass++)
                {
                    var smooth = new List<Vector2>(points);
                    for (int i = 1; i < points.Count - 1; i++)
                        smooth[i] = .25f * points[i - 1] + .5f * points[i] + .25f * points[i + 1];
                    if (closed)
                    {
                        smooth[0] = .25f * points[points.Count - 2] + .5f * points[0] + .25f * points[1];
                        smooth[smooth.Count - 1] = smooth[0];
                    }
                    points = smooth;
                }
                for (int i = 0; i < points.Count; i++)
                    points[i] = new Vector2(points[i].x / w, points[i].y / h);
                paths.Add(new OutlinePathSegment { segmentName = "Outline " + (paths.Count + 1), points = points, strokeWidth = 5 });
            }
            Vector2 Point(int p) => new Vector2(p % width - .5f, p / width - .5f);
        }

        static ulong Edge(int a, int b) => ((ulong)(uint)System.Math.Min(a, b) << 32) | (uint)System.Math.Max(a, b);

        static void Thin(bool[] image, int w, int h)
        {
            var remove = new List<int>();
            bool changed;
            var n = new bool[8];
            do
            {
                changed = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    remove.Clear();
                    for (int y = 1; y < h - 1; y++) for (int x = 1; x < w - 1; x++)
                    {
                        int p = y * w + x;
                        if (!image[p]) continue;
                        int count = 0, transitions = 0;
                        for (int d = 0; d < 8; d++) { n[d] = image[p + DY[d] * w + DX[d]]; if (n[d]) count++; }
                        if (count < 2 || count > 6) continue;
                        for (int d = 0; d < 8; d++) if (!n[d] && n[(d + 1) % 8]) transitions++;
                        if (transitions != 1) continue;
                        if (pass == 0 ? (n[0] && n[2] && n[4]) || (n[2] && n[4] && n[6])
                                      : (n[0] && n[2] && n[6]) || (n[0] && n[4] && n[6])) continue;
                        remove.Add(p);
                    }
                    foreach (int p in remove) image[p] = false;
                    changed |= remove.Count > 0;
                }
            } while (changed);
        }
    }
}
