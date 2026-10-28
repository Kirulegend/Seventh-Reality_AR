using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor.SceneManagement;

namespace RutherfordSim.Bio.Tracing.Editor
{
    [InitializeOnLoad]
    public static class GenerateBookDiagramsAndPrefab
    {
        private const string DiagramDir = "Assets/DiagramTracing/Examples/BookDiagrams";
        private const string PrefabDir = "Assets/DiagramTracing/Prefabs";
        private const string BookPrefabPath = "Assets/DiagramTracing/Prefabs/ARTracingBook.prefab";
        private const string BoardPrefabPath = "Assets/DiagramTracing/Prefabs/ARTracingBoard.prefab";

        static GenerateBookDiagramsAndPrefab()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(BookPrefabPath) || 
                    !File.Exists($"{DiagramDir}/Diagram_02_Chloroplast_Colored.png") ||
                    (File.Exists(BookPrefabPath) && File.ReadAllText(BookPrefabPath).Contains("\\u25C4")))
                {
                    GenerateAll();
                }
            };
        }

        [MenuItem("Tools/Diagram Tracing/Generate 4-Page AR Book & Diagrams", false, 5)]
        public static void GenerateAll()
        {
            if (!Directory.Exists(DiagramDir))
                Directory.CreateDirectory(DiagramDir);

            if (!Directory.Exists(PrefabDir))
                Directory.CreateDirectory(PrefabDir);

            AssetDatabase.Refresh();

            // 1. Generate / Load the 8 DiagramDataSO assets
            var diagrams = CreateOrLoadDiagramAssets();

            // 2. Ensure base ARTracingBoard prefab exists
            if (!File.Exists(BoardPrefabPath))
            {
                CreateARTracingBoardPrefab.GeneratePrefab();
            }

            // 3. Build the 3D Book Prefab
            BuildBookPrefab(diagrams);

            // 4. Update the current scene to spawn the book
            UpdateSceneSpawner();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[GenerateBook] 4-Page AR Tracing Book successfully created and configured in ARDiagramTracing.unity!");
        }

        private static List<DiagramDataSO> CreateOrLoadDiagramAssets()
        {
            var list = new List<DiagramDataSO>();

            // Diagram 1: Mitochondria (reuse existing high-res outline & colored)
            list.Add(GetOrCreateDiagram(
                "Diagram_01_Mitochondria",
                "Mitochondria Organelle",
                "Trace the outer capsule membrane and inner folded cristae.",
                "Assets/DiagramTracing/Examples/MitochondriaSmooth_Outline.png",
                "Assets/DiagramTracing/Examples/MitochondriaSmooth_Colored.png",
                DrawMitochondriaBackup,
                null
            ));

            // Diagram 2: Chloroplast
            list.Add(GetOrCreateDiagram(
                "Diagram_02_Chloroplast",
                "Plant Chloroplast",
                "Trace the double outer membrane and stacked thylakoid grana disks.",
                null,
                null,
                DrawChloroplast,
                DrawChloroplastColored
            ));

            // Diagram 3: Animal Cell
            list.Add(GetOrCreateDiagram(
                "Diagram_03_AnimalCell",
                "Animal Cell Anatomy",
                "Trace the cellular membrane, central nucleus, and endoplasmic reticulum.",
                null,
                null,
                DrawAnimalCell,
                DrawAnimalCellColored
            ));

            // Diagram 4: Plant Cell
            list.Add(GetOrCreateDiagram(
                "Diagram_04_PlantCell",
                "Plant Cell Structure",
                "Trace the rigid outer cell wall, cell membrane, and large central vacuole.",
                null,
                null,
                DrawPlantCell,
                DrawPlantCellColored
            ));

            // Diagram 5: Human Heart
            list.Add(GetOrCreateDiagram(
                "Diagram_05_HumanHeart",
                "Human Heart Anatomy",
                "Trace the cardiac ventricles, atria chambers, and ascending aorta arch.",
                null,
                null,
                DrawHumanHeart,
                DrawHumanHeartColored
            ));

            // Diagram 6: Neuron
            list.Add(GetOrCreateDiagram(
                "Diagram_06_Neuron",
                "Neuron Nerve Cell",
                "Trace the soma cell body, dendrites, long axon, and terminal buttons.",
                null,
                null,
                DrawNeuron,
                DrawNeuronColored
            ));

            // Diagram 7: DNA Double Helix
            list.Add(GetOrCreateDiagram(
                "Diagram_07_DNAHelix",
                "DNA Double Helix",
                "Trace the interlaced helical sugar-phosphate backbones and connecting base pairs.",
                null,
                null,
                DrawDNAHelix,
                DrawDNAHelixColored
            ));

            // Diagram 8: Rutherford Atom
            list.Add(GetOrCreateDiagram(
                "Diagram_08_RutherfordAtom",
                "Atomic Model (Rutherford)",
                "Trace the central atomic nucleus and elliptical electron orbital shells.",
                null,
                null,
                DrawRutherfordAtom,
                DrawRutherfordAtomColored
            ));

            return list;
        }

        private static DiagramDataSO GetOrCreateDiagram(
            string assetName,
            string title,
            string summary,
            string existingPngPath,
            string existingColoredPngPath,
            Action<Texture2D> proceduralDrawer,
            Action<Texture2D> proceduralColoredDrawer)
        {
            string soPath = $"{DiagramDir}/{assetName}.asset";
            DiagramDataSO existingSo = AssetDatabase.LoadAssetAtPath<DiagramDataSO>(soPath);

            string pngPath = $"{DiagramDir}/{assetName}_Outline.png";
            Texture2D outlineTex = null;

            if (!string.IsNullOrEmpty(existingPngPath) && File.Exists(existingPngPath))
            {
                outlineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(existingPngPath);
            }

            if (outlineTex == null)
            {
                if (!File.Exists(pngPath))
                {
                    Texture2D newTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    Color32 clear = new Color32(0, 0, 0, 0);
                    Color32[] clears = new Color32[512 * 512];
                    for (int i = 0; i < clears.Length; i++) clears[i] = clear;
                    newTex.SetPixels32(clears);

                    proceduralDrawer?.Invoke(newTex);
                    newTex.Apply();

                    byte[] pngBytes = ImageConversion.EncodeToPNG(newTex);
                    File.WriteAllBytes(pngPath, pngBytes);
                    UnityEngine.Object.DestroyImmediate(newTex);
                    AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);

                    var importer = AssetImporter.GetAtPath(pngPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.isReadable = true;
                        importer.textureType = TextureImporterType.Default;
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                    }
                }
                outlineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
            }

            // Colored Texture
            string coloredPngPath = $"{DiagramDir}/{assetName}_Colored.png";
            Texture2D coloredTex = null;

            if (!string.IsNullOrEmpty(existingColoredPngPath) && File.Exists(existingColoredPngPath))
            {
                coloredTex = AssetDatabase.LoadAssetAtPath<Texture2D>(existingColoredPngPath);
            }

            if (coloredTex == null)
            {
                if (!File.Exists(coloredPngPath) && proceduralColoredDrawer != null)
                {
                    Texture2D newTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    Color32 clear = new Color32(0, 0, 0, 0);
                    Color32[] clears = new Color32[512 * 512];
                    for (int i = 0; i < clears.Length; i++) clears[i] = clear;
                    newTex.SetPixels32(clears);

                    proceduralColoredDrawer?.Invoke(newTex);
                    newTex.Apply();

                    byte[] pngBytes = ImageConversion.EncodeToPNG(newTex);
                    File.WriteAllBytes(coloredPngPath, pngBytes);
                    UnityEngine.Object.DestroyImmediate(newTex);
                    AssetDatabase.ImportAsset(coloredPngPath, ImportAssetOptions.ForceUpdate);

                    var importer = AssetImporter.GetAtPath(coloredPngPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.isReadable = true;
                        importer.textureType = TextureImporterType.Default;
                        importer.alphaIsTransparency = true;
                        importer.SaveAndReimport();
                    }
                }
                coloredTex = AssetDatabase.LoadAssetAtPath<Texture2D>(coloredPngPath);
            }

            if (existingSo == null)
            {
                existingSo = ScriptableObject.CreateInstance<DiagramDataSO>();
                existingSo.diagramTitle = title;
                existingSo.diagramSummary = summary;
                existingSo.outlineTexture = outlineTex;
                existingSo.fullColoredTexture = coloredTex;
                existingSo.completionThreshold = 0.95f;
                existingSo.snapDistance = 16f;
                existingSo.tracingLineThickness = 12f;

                if (outlineTex != null)
                {
                    try
                    {
                        existingSo.pathSegments = OutlinePathExtractor.Extract(outlineTex);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[GenerateDiagram] Path extraction warning for {assetName}: {ex.Message}");
                    }
                }

                AssetDatabase.CreateAsset(existingSo, soPath);
            }
            else
            {
                existingSo.diagramTitle = title;
                existingSo.diagramSummary = summary;
                existingSo.outlineTexture = outlineTex;
                existingSo.fullColoredTexture = coloredTex;
                existingSo.completionThreshold = 0.95f;
                if (existingSo.pathSegments == null || existingSo.pathSegments.Count == 0)
                {
                    if (outlineTex != null)
                        existingSo.pathSegments = OutlinePathExtractor.Extract(outlineTex);
                }
                EditorUtility.SetDirty(existingSo);
            }

            return existingSo;
        }

        #region Procedural Drawing Helpers

        private static readonly Color LineColor = new Color(0.12f, 0.14f, 0.18f, 1f);

        private static void DrawMitochondriaBackup(Texture2D t)
        {
            DrawEllipse(t, new Vector2(256, 256), 200, 120, 0f, LineColor, 8);
            DrawEllipse(t, new Vector2(256, 256), 170, 95, 0f, LineColor, 6);
            for (int x = 140; x <= 370; x += 36)
            {
                DrawLine(t, new Vector2(x, 190), new Vector2(x, 320), LineColor, 6);
            }
        }

        private static void DrawChloroplast(Texture2D t)
        {
            // Outer double membrane
            DrawEllipse(t, new Vector2(256, 256), 210, 130, 0f, LineColor, 8);
            DrawEllipse(t, new Vector2(256, 256), 185, 110, 0f, LineColor, 6);

            // 4 Thylakoid Grana stacks
            int[] stackX = { 135, 215, 295, 375 };
            foreach (int x in stackX)
            {
                for (int y = 205; y <= 305; y += 25)
                {
                    DrawEllipse(t, new Vector2(x, y), 24, 7, 0f, LineColor, 5);
                }
            }

            // Connecting stromal lamellae lines
            DrawLine(t, new Vector2(135, 255), new Vector2(295, 255), LineColor, 5);
            DrawLine(t, new Vector2(215, 280), new Vector2(375, 280), LineColor, 5);
        }

        private static void DrawAnimalCell(Texture2D t)
        {
            // Cell membrane (smooth organic circle)
            DrawCircle(t, new Vector2(256, 256), 195, LineColor, 8);

            // Nucleus & Nucleolus
            DrawCircle(t, new Vector2(256, 256), 70, LineColor, 6);
            DrawCircle(t, new Vector2(245, 260), 25, LineColor, 5);

            // Endoplasmic Reticulum (concentric arches)
            DrawArc(t, new Vector2(256, 256), 95, 20f, 160f, LineColor, 5);
            DrawArc(t, new Vector2(256, 256), 115, 30f, 150f, LineColor, 5);
            DrawArc(t, new Vector2(256, 256), 135, 40f, 140f, LineColor, 5);

            // Mitochondria & Vacuoles (small organelles)
            DrawEllipse(t, new Vector2(150, 170), 28, 14, 35f, LineColor, 5);
            DrawEllipse(t, new Vector2(360, 180), 30, 15, -25f, LineColor, 5);
            DrawCircle(t, new Vector2(160, 340), 16, LineColor, 5);
            DrawCircle(t, new Vector2(350, 340), 18, LineColor, 5);
        }

        private static void DrawPlantCell(Texture2D t)
        {
            // Rigid rectangular Cell Wall
            DrawRoundedRect(t, new Rect(60, 60, 392, 392), 40, LineColor, 9);
            // Inner Cell Membrane
            DrawRoundedRect(t, new Rect(80, 80, 352, 352), 30, LineColor, 6);

            // Large Central Vacuole
            DrawRoundedRect(t, new Rect(130, 130, 190, 230), 35, LineColor, 6);

            // Nucleus pushed to bottom-right
            DrawCircle(t, new Vector2(365, 210), 38, LineColor, 6);
            DrawCircle(t, new Vector2(365, 210), 14, LineColor, 4);

            // Chloroplasts
            DrawEllipse(t, new Vector2(120, 385), 25, 12, 15f, LineColor, 5);
            DrawEllipse(t, new Vector2(370, 360), 25, 12, -20f, LineColor, 5);
            DrawEllipse(t, new Vector2(370, 110), 25, 12, 10f, LineColor, 5);
        }

        private static void DrawHumanHeart(Texture2D t)
        {
            // Heart outer ventricular apex and walls
            Vector2 apex = new Vector2(256, 90);
            Vector2 leftShoulder = new Vector2(130, 310);
            Vector2 rightShoulder = new Vector2(382, 310);

            DrawBezier(t, apex, new Vector2(110, 180), new Vector2(100, 270), leftShoulder, LineColor, 8);
            DrawBezier(t, leftShoulder, new Vector2(150, 380), new Vector2(230, 360), new Vector2(256, 320), LineColor, 8);

            DrawBezier(t, apex, new Vector2(400, 180), new Vector2(410, 270), rightShoulder, LineColor, 8);
            DrawBezier(t, rightShoulder, new Vector2(360, 380), new Vector2(280, 360), new Vector2(256, 320), LineColor, 8);

            // Central septum line
            DrawLine(t, new Vector2(256, 110), new Vector2(256, 310), LineColor, 6);

            // Aorta arch on top
            DrawArc(t, new Vector2(256, 320), 65, 30f, 150f, LineColor, 7);
            DrawLine(t, new Vector2(220, 375), new Vector2(220, 420), LineColor, 6);
            DrawLine(t, new Vector2(256, 385), new Vector2(256, 430), LineColor, 6);
            DrawLine(t, new Vector2(290, 375), new Vector2(290, 420), LineColor, 6);
        }

        private static void DrawNeuron(Texture2D t)
        {
            Vector2 soma = new Vector2(140, 256);

            // Star-shaped soma with 5 dendrites
            DrawCircle(t, soma, 38, LineColor, 7);
            DrawCircle(t, soma, 15, LineColor, 5);

            DrawLine(t, soma + new Vector2(-25, 25), soma + new Vector2(-75, 70), LineColor, 6);
            DrawLine(t, soma + new Vector2(-35, 0), soma + new Vector2(-90, 0), LineColor, 6);
            DrawLine(t, soma + new Vector2(-25, -25), soma + new Vector2(-75, -70), LineColor, 6);
            DrawLine(t, soma + new Vector2(0, 35), soma + new Vector2(0, 95), LineColor, 6);
            DrawLine(t, soma + new Vector2(0, -35), soma + new Vector2(0, -95), LineColor, 6);

            // Long Axon running to the right
            Vector2 axonStart = soma + new Vector2(38, 0);
            Vector2 axonEnd = new Vector2(430, 256);
            DrawLine(t, axonStart, axonEnd, LineColor, 7);

            // 4 Myelin Sheaths (rounded pills along the axon)
            int[] myelinX = { 210, 270, 330, 390 };
            foreach (int mx in myelinX)
            {
                DrawEllipse(t, new Vector2(mx, 256), 22, 14, 0f, LineColor, 6);
            }

            // Axon terminal buttons
            DrawLine(t, axonEnd, axonEnd + new Vector2(45, 45), LineColor, 5);
            DrawLine(t, axonEnd, axonEnd + new Vector2(50, 0), LineColor, 5);
            DrawLine(t, axonEnd, axonEnd + new Vector2(45, -45), LineColor, 5);
            DrawCircle(t, axonEnd + new Vector2(48, 48), 6, LineColor, 4);
            DrawCircle(t, axonEnd + new Vector2(54, 0), 6, LineColor, 4);
            DrawCircle(t, axonEnd + new Vector2(48, -48), 6, LineColor, 4);
        }

        private static void DrawDNAHelix(Texture2D t)
        {
            // Dual intertwined sine waves
            int points = 80;
            Vector2 prev1 = Vector2.zero, prev2 = Vector2.zero;

            for (int i = 0; i <= points; i++)
            {
                float y = Mathf.Lerp(60f, 452f, i / (float)points);
                float angle = (y / 452f) * Mathf.PI * 4f; // 2 complete twists

                float x1 = 256f + Mathf.Sin(angle) * 110f;
                float x2 = 256f - Mathf.Sin(angle) * 110f;

                Vector2 curr1 = new Vector2(x1, y);
                Vector2 curr2 = new Vector2(x2, y);

                if (i > 0)
                {
                    DrawLine(t, prev1, curr1, LineColor, 7);
                    DrawLine(t, prev2, curr2, LineColor, 7);
                }

                // Horizontal rungs (base pairs) every few steps
                if (i % 6 == 0)
                {
                    DrawLine(t, curr1, curr2, LineColor, 5);
                }

                prev1 = curr1;
                prev2 = curr2;
            }
        }

        private static void DrawRutherfordAtom(Texture2D t)
        {
            Vector2 center = new Vector2(256, 256);

            // 3 Elliptical electron orbit shells at 0, 60, and 120 degrees
            DrawEllipse(t, center, 195, 65, 0f, LineColor, 6);
            DrawEllipse(t, center, 195, 65, 60f, LineColor, 6);
            DrawEllipse(t, center, 195, 65, 120f, LineColor, 6);

            // Central nucleus cluster
            DrawCircle(t, center, 28, LineColor, 7);
            DrawCircle(t, center + new Vector2(-7, 5), 8, LineColor, 5);
            DrawCircle(t, center + new Vector2(7, 5), 8, LineColor, 5);
            DrawCircle(t, center + new Vector2(0, -8), 8, LineColor, 5);

            // Orbiting electrons (dots)
            DrawCircle(t, center + new Vector2(195, 0), 7, LineColor, 5);
            DrawCircle(t, center + new Vector2(-195 * 0.5f, 195 * 0.866f), 7, LineColor, 5);
            DrawCircle(t, center + new Vector2(-195 * 0.5f, -195 * 0.866f), 7, LineColor, 5);
        }

        private static void DrawLine(Texture2D t, Vector2 a, Vector2 b, Color c, int thickness)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 pt = Vector2.Lerp(a, b, i / (float)steps);
                FillCirclePixels(t, pt, thickness * 0.5f, c);
            }
        }

        private static void DrawCircle(Texture2D t, Vector2 center, float radius, Color c, int thickness)
        {
            int steps = Mathf.Max(16, Mathf.CeilToInt(radius * Mathf.PI * 2f));
            for (int i = 0; i <= steps; i++)
            {
                float angle = (i / (float)steps) * Mathf.PI * 2f;
                Vector2 pt = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                FillCirclePixels(t, pt, thickness * 0.5f, c);
            }
        }

        private static void DrawArc(Texture2D t, Vector2 center, float radius, float startDeg, float endDeg, Color c, int thickness)
        {
            int steps = Mathf.Max(12, Mathf.CeilToInt(radius * (Mathf.Abs(endDeg - startDeg) / 180f) * Mathf.PI));
            for (int i = 0; i <= steps; i++)
            {
                float angle = Mathf.Lerp(startDeg, endDeg, i / (float)steps) * Mathf.Deg2Rad;
                Vector2 pt = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                FillCirclePixels(t, pt, thickness * 0.5f, c);
            }
        }

        private static void DrawEllipse(Texture2D t, Vector2 center, float rx, float ry, float angleDeg, Color c, int thickness)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
            int steps = Mathf.Max(32, Mathf.CeilToInt(Mathf.Max(rx, ry) * Mathf.PI * 2f));

            for (int i = 0; i <= steps; i++)
            {
                float theta = (i / (float)steps) * Mathf.PI * 2f;
                float lx = Mathf.Cos(theta) * rx;
                float ly = Mathf.Sin(theta) * ry;

                Vector2 pt = center + new Vector2(lx * cos - ly * sin, lx * sin + ly * cos);
                FillCirclePixels(t, pt, thickness * 0.5f, c);
            }
        }

        private static void DrawRoundedRect(Texture2D t, Rect rect, float radius, Color c, int thickness)
        {
            DrawLine(t, new Vector2(rect.xMin + radius, rect.yMin), new Vector2(rect.xMax - radius, rect.yMin), c, thickness);
            DrawLine(t, new Vector2(rect.xMin + radius, rect.yMax), new Vector2(rect.xMax - radius, rect.yMax), c, thickness);
            DrawLine(t, new Vector2(rect.xMin, rect.yMin + radius), new Vector2(rect.xMin, rect.yMax - radius), c, thickness);
            DrawLine(t, new Vector2(rect.xMax, rect.yMin + radius), new Vector2(rect.xMax, rect.yMax - radius), c, thickness);

            DrawArc(t, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, c, thickness);
            DrawArc(t, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, c, thickness);
            DrawArc(t, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, c, thickness);
            DrawArc(t, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, c, thickness);
        }

        private static void DrawBezier(Texture2D t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, Color c, int thickness)
        {
            int steps = 60;
            Vector2 prev = p0;
            for (int i = 1; i <= steps; i++)
            {
                float u = i / (float)steps;
                float uu = u * u, uuu = uu * u;
                float v = 1f - u, vv = v * v, vvv = vv * v;

                Vector2 curr = vvv * p0 + 3f * vv * u * p1 + 3f * v * uu * p2 + uuu * p3;
                DrawLine(t, prev, curr, c, thickness);
                prev = curr;
            }
        }

        private static void FillCirclePixels(Texture2D t, Vector2 pt, float radius, Color c)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(pt.x - radius), 0, t.width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(pt.x + radius), 0, t.width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(pt.y - radius), 0, t.height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(pt.y + radius), 0, t.height - 1);
            float rSqr = radius * radius;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dSqr = (x - pt.x) * (x - pt.x) + (y - pt.y) * (y - pt.y);
                    if (dSqr <= rSqr)
                    {
                        t.SetPixel(x, y, c);
                    }
                }
            }
        }

        private static void FillCircle(Texture2D t, Vector2 center, float radius, Color c)
        {
            FillCirclePixels(t, center, radius, c);
        }

        private static void FillEllipse(Texture2D t, Vector2 center, float rx, float ry, float angleDeg, Color c)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cos = Mathf.Cos(-rad), sin = Mathf.Sin(-rad);
            float maxR = Mathf.Max(rx, ry);
            int minX = Mathf.Clamp(Mathf.FloorToInt(center.x - maxR), 0, t.width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(center.x + maxR), 0, t.width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(center.y - maxR), 0, t.height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(center.y + maxR), 0, t.height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float lx = dx * cos - dy * sin;
                    float ly = dx * sin + dy * cos;
                    if ((lx * lx) / (rx * rx) + (ly * ly) / (ry * ry) <= 1f)
                    {
                        t.SetPixel(x, y, c);
                    }
                }
            }
        }

        private static void FillRoundedRect(Texture2D t, Rect rect, float radius, Color c)
        {
            int minX = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, t.width - 1);
            int maxX = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 0, t.width - 1);
            int minY = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, t.height - 1);
            int maxY = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 0, t.height - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float cx = Mathf.Clamp(x, rect.xMin + radius, rect.xMax - radius);
                    float cy = Mathf.Clamp(y, rect.yMin + radius, rect.yMax - radius);
                    float dx = x - cx;
                    float dy = y - cy;
                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        t.SetPixel(x, y, c);
                    }
                }
            }
        }

        private static void DrawChloroplastColored(Texture2D t)
        {
            FillEllipse(t, new Vector2(256, 256), 205, 125, 0f, new Color(0.72f, 0.94f, 0.65f, 1f));
            int[] stackX = { 135, 215, 295, 375 };
            foreach (int x in stackX)
            {
                for (int y = 205; y <= 305; y += 25)
                {
                    FillEllipse(t, new Vector2(x, y), 24, 7, 0f, new Color(0.18f, 0.58f, 0.28f, 1f));
                }
            }
            DrawChloroplast(t);
        }

        private static void DrawAnimalCellColored(Texture2D t)
        {
            FillCircle(t, new Vector2(256, 256), 193, new Color(0.85f, 0.93f, 0.98f, 1f));
            FillCircle(t, new Vector2(256, 256), 68, new Color(0.75f, 0.55f, 0.88f, 1f));
            FillCircle(t, new Vector2(245, 260), 24, new Color(0.48f, 0.22f, 0.65f, 1f));
            FillEllipse(t, new Vector2(150, 170), 26, 12, 35f, new Color(0.95f, 0.50f, 0.35f, 1f));
            FillEllipse(t, new Vector2(360, 180), 28, 13, -25f, new Color(0.95f, 0.50f, 0.35f, 1f));
            FillCircle(t, new Vector2(160, 340), 14, new Color(0.55f, 0.82f, 0.95f, 1f));
            FillCircle(t, new Vector2(350, 340), 16, new Color(0.55f, 0.82f, 0.95f, 1f));
            DrawAnimalCell(t);
        }

        private static void DrawPlantCellColored(Texture2D t)
        {
            FillRoundedRect(t, new Rect(62, 62, 388, 388), 38, new Color(0.45f, 0.78f, 0.40f, 1f));
            FillRoundedRect(t, new Rect(82, 82, 348, 348), 28, new Color(0.88f, 0.96f, 0.82f, 1f));
            FillRoundedRect(t, new Rect(132, 132, 186, 226), 33, new Color(0.65f, 0.88f, 0.95f, 1f));
            FillCircle(t, new Vector2(365, 210), 36, new Color(0.78f, 0.45f, 0.65f, 1f));
            FillCircle(t, new Vector2(365, 210), 13, new Color(0.48f, 0.20f, 0.42f, 1f));
            FillEllipse(t, new Vector2(140, 395), 20, 10, 15f, new Color(0.20f, 0.65f, 0.30f, 1f));
            FillEllipse(t, new Vector2(385, 330), 20, 10, -35f, new Color(0.20f, 0.65f, 0.30f, 1f));
            DrawPlantCell(t);
        }

        private static void DrawHumanHeartColored(Texture2D t)
        {
            FillCircle(t, new Vector2(210, 290), 48, new Color(0.25f, 0.45f, 0.82f, 1f));
            FillCircle(t, new Vector2(305, 285), 48, new Color(0.85f, 0.20f, 0.25f, 1f));
            FillRoundedRect(t, new Rect(170, 140, 170, 150), 30, new Color(0.80f, 0.18f, 0.24f, 1f));
            FillEllipse(t, new Vector2(256, 360), 55, 30, 0f, new Color(0.92f, 0.22f, 0.22f, 1f));
            DrawHumanHeart(t);
        }

        private static void DrawNeuronColored(Texture2D t)
        {
            FillCircle(t, new Vector2(170, 256), 46, new Color(0.96f, 0.78f, 0.35f, 1f));
            FillCircle(t, new Vector2(170, 256), 18, new Color(0.82f, 0.48f, 0.20f, 1f));
            FillRoundedRect(t, new Rect(240, 244, 30, 24), 8, new Color(0.25f, 0.78f, 0.85f, 1f));
            FillRoundedRect(t, new Rect(285, 244, 30, 24), 8, new Color(0.25f, 0.78f, 0.85f, 1f));
            FillRoundedRect(t, new Rect(330, 244, 30, 24), 8, new Color(0.25f, 0.78f, 0.85f, 1f));
            FillRoundedRect(t, new Rect(375, 244, 30, 24), 8, new Color(0.25f, 0.78f, 0.85f, 1f));
            DrawNeuron(t);
        }

        private static void DrawDNAHelixColored(Texture2D t)
        {
            Color[] baseColors = {
                new Color(0.90f, 0.25f, 0.25f, 1f),
                new Color(0.25f, 0.75f, 0.35f, 1f),
                new Color(0.25f, 0.45f, 0.90f, 1f),
                new Color(0.95f, 0.80f, 0.20f, 1f)
            };
            int colorIdx = 0;
            for (int y = 90; y <= 420; y += 36)
            {
                float angle = ((y - 90f) / 330f) * Mathf.PI * 3f;
                float x1 = 256f + Mathf.Sin(angle) * 110f;
                float x2 = 256f - Mathf.Sin(angle) * 110f;
                DrawLine(t, new Vector2(x1, y), new Vector2(x2, y), baseColors[(colorIdx++) % baseColors.Length], 8);
            }
            DrawDNAHelix(t);
        }

        private static void DrawRutherfordAtomColored(Texture2D t)
        {
            Vector2 center = new Vector2(256, 256);
            DrawEllipse(t, center, 195, 65, 0f, new Color(0.15f, 0.65f, 0.95f, 1f), 6);
            DrawEllipse(t, center, 195, 65, 60f, new Color(0.15f, 0.65f, 0.95f, 1f), 6);
            DrawEllipse(t, center, 195, 65, 120f, new Color(0.15f, 0.65f, 0.95f, 1f), 6);

            FillCircle(t, center, 28, new Color(0.95f, 0.50f, 0.15f, 1f));
            FillCircle(t, center + new Vector2(-7, 5), 8, new Color(0.95f, 0.25f, 0.25f, 1f));
            FillCircle(t, center + new Vector2(7, 5), 8, new Color(0.98f, 0.75f, 0.15f, 1f));
            FillCircle(t, center + new Vector2(0, -8), 8, new Color(0.95f, 0.25f, 0.25f, 1f));

            FillCircle(t, center + new Vector2(195, 0), 7, new Color(0.20f, 0.85f, 1f, 1f));
            FillCircle(t, center + new Vector2(-195 * 0.5f, 195 * 0.866f), 7, new Color(0.20f, 0.85f, 1f, 1f));
            FillCircle(t, center + new Vector2(-195 * 0.5f, -195 * 0.866f), 7, new Color(0.20f, 0.85f, 1f, 1f));
            DrawRutherfordAtom(t);
        }

        #endregion

        #region Book Prefab Construction

        private static void BuildBookPrefab(List<DiagramDataSO> diagrams)
        {
            GameObject baseBoardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefabPath);
            if (baseBoardPrefab == null)
            {
                Debug.LogError("[BuildBook] ARTracingBoard.prefab not found!");
                return;
            }

            GameObject bookRoot = new GameObject("ARTracingBook");

            // 1. Hardcover Base Plate
            GameObject coverObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            coverObj.name = "BookCover_Hardback";
            coverObj.transform.SetParent(bookRoot.transform, false);
            coverObj.transform.localPosition = new Vector3(0, -0.008f, 0);
            coverObj.transform.localRotation = Quaternion.identity;
            coverObj.transform.localScale = new Vector3(0.96f, 0.016f, 0.58f); // 96cm x 58cm hardcover

            Material coverMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            coverMat.color = new Color(0.06f, 0.10f, 0.16f); // Deep navy leather
            coverMat.SetFloat("_Smoothness", 0.55f);
            AssetDatabase.CreateAsset(coverMat, $"{PrefabDir}/M_BookCover.mat");
            coverObj.GetComponent<Renderer>().sharedMaterial = coverMat;

            // 2. Book Spine (Center Divider)
            GameObject spineObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            spineObj.name = "BookSpine";
            spineObj.transform.SetParent(bookRoot.transform, false);
            spineObj.transform.localPosition = new Vector3(0, 0.003f, 0);
            spineObj.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            spineObj.transform.localScale = new Vector3(0.035f, 0.29f, 0.035f);

            Material spineMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            spineMat.color = new Color(0.85f, 0.68f, 0.28f); // Gold / Brass spine
            spineMat.SetFloat("_Smoothness", 0.8f);
            AssetDatabase.CreateAsset(spineMat, $"{PrefabDir}/M_BookSpine.mat");
            spineObj.GetComponent<Renderer>().sharedMaterial = spineMat;

            // 3. Paper Pages Bed Material
            Material pageMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            pageMat.color = new Color(0.97f, 0.98f, 0.99f); // Paper white
            pageMat.SetFloat("_Smoothness", 0.2f);
            AssetDatabase.CreateAsset(pageMat, $"{PrefabDir}/M_BookPage.mat");

            // Left Page Bed
            GameObject leftBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftBed.name = "LeftPage_Bed";
            leftBed.transform.SetParent(bookRoot.transform, false);
            leftBed.transform.localPosition = new Vector3(-0.235f, 0.005f, 0);
            leftBed.transform.localScale = new Vector3(0.44f, 0.012f, 0.54f);
            leftBed.GetComponent<Renderer>().sharedMaterial = pageMat;

            // Right Page Bed
            GameObject rightBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightBed.name = "RightPage_Bed";
            rightBed.transform.SetParent(bookRoot.transform, false);
            rightBed.transform.localPosition = new Vector3(0.235f, 0.005f, 0);
            rightBed.transform.localScale = new Vector3(0.44f, 0.012f, 0.54f);
            rightBed.GetComponent<Renderer>().sharedMaterial = pageMat;

            // 4. Turning Page Leaf (Animated flap)
            GameObject leafPivot = new GameObject("TurningPageLeaf");
            leafPivot.transform.SetParent(bookRoot.transform, false);
            leafPivot.transform.localPosition = new Vector3(0, 0.013f, 0);
            leafPivot.transform.localRotation = Quaternion.identity;

            GameObject leafMesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leafMesh.name = "LeafMesh";
            leafMesh.transform.SetParent(leafPivot.transform, false);
            leafMesh.transform.localPosition = new Vector3(0.215f, 0, 0); // Offset to pivot around spine
            leafMesh.transform.localScale = new Vector3(0.43f, 0.002f, 0.53f);
            leafMesh.GetComponent<Renderer>().sharedMaterial = pageMat;

            leafPivot.SetActive(false); // Resting state is hidden

            // 5. Pooled Left Board Instance
            GameObject leftBoardObj = PrefabUtility.InstantiatePrefab(baseBoardPrefab) as GameObject;
            leftBoardObj.name = "Left_TracingBoard";
            leftBoardObj.transform.SetParent(bookRoot.transform, false);
            leftBoardObj.transform.localPosition = new Vector3(-0.235f, 0.012f, 0);
            leftBoardObj.transform.localRotation = Quaternion.Euler(0, 0, 0); // Flat on book
            leftBoardObj.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f); // Scaled to fit page
            var leftBoard = leftBoardObj.GetComponent<ARTracingBoard>();
            leftBoard.SetBackingPlateVisible(false);

            // 6. Pooled Right Board Instance
            GameObject rightBoardObj = PrefabUtility.InstantiatePrefab(baseBoardPrefab) as GameObject;
            rightBoardObj.name = "Right_TracingBoard";
            rightBoardObj.transform.SetParent(bookRoot.transform, false);
            rightBoardObj.transform.localPosition = new Vector3(0.235f, 0.012f, 0);
            rightBoardObj.transform.localRotation = Quaternion.Euler(0, 0, 0); // Flat on book
            rightBoardObj.transform.localScale = new Vector3(0.48f, 0.48f, 0.48f); // Scaled to fit page
            var rightBoard = rightBoardObj.GetComponent<ARTracingBoard>();
            rightBoard.SetBackingPlateVisible(false);

            // 7. World Space Book Navigation Canvas
            GameObject navCanvasObj = new GameObject("BookNavigationCanvas");
            navCanvasObj.transform.SetParent(bookRoot.transform, false);
            navCanvasObj.transform.localPosition = new Vector3(0, 0.015f, 0);
            navCanvasObj.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            navCanvasObj.transform.localScale = new Vector3(0.0006f, 0.0006f, 0.0006f);

            Canvas navCanvas = navCanvasObj.AddComponent<Canvas>();
            navCanvas.renderMode = RenderMode.WorldSpace;
            navCanvas.worldCamera = Camera.main;

            navCanvasObj.AddComponent<CanvasScaler>();
            navCanvasObj.AddComponent<GraphicRaycaster>();

            RectTransform navRect = navCanvasObj.GetComponent<RectTransform>();
            navRect.sizeDelta = new Vector2(1600, 960);

            // Previous Button (<) on Bottom-Left page corner
            GameObject prevBtnObj = CreateNavButton(navCanvasObj.transform, "PrevPageButton", "<  Prev Page",
                new Vector2(-650, -420), new Vector2(240, 70), new Color(0.12f, 0.16f, 0.22f), Color.white);
            Button prevButton = prevBtnObj.GetComponent<Button>();

            // Next Button (>) on Bottom-Right page corner
            GameObject nextBtnObj = CreateNavButton(navCanvasObj.transform, "NextPageButton", "Next Page  >",
                new Vector2(650, -420), new Vector2(240, 70), new Color(0.08f, 0.65f, 0.45f), Color.white);
            Button nextButton = nextBtnObj.GetComponent<Button>();

            // Page Number Badge (Center Bottom)
            GameObject pageBadgeObj = new GameObject("PageNumberBadge");
            pageBadgeObj.transform.SetParent(navCanvasObj.transform, false);
            Image badgeImg = pageBadgeObj.AddComponent<Image>();
            badgeImg.color = new Color(0.10f, 0.13f, 0.18f, 0.90f);
            RectTransform badgeRt = pageBadgeObj.GetComponent<RectTransform>();
            badgeRt.anchoredPosition = new Vector2(0, -420);
            badgeRt.sizeDelta = new Vector2(300, 56);

            GameObject pageNumTextObj = new GameObject("Text");
            pageNumTextObj.transform.SetParent(pageBadgeObj.transform, false);
            TextMeshProUGUI pageNumText = pageNumTextObj.AddComponent<TextMeshProUGUI>();
            pageNumText.text = "Pages 1-2 of 8";
            pageNumText.fontSize = 24;
            pageNumText.fontStyle = FontStyles.Bold;
            pageNumText.color = Color.white;
            pageNumText.alignment = TextAlignmentOptions.Center;
            RectTransform pageNumRt = pageNumTextObj.GetComponent<RectTransform>();
            pageNumRt.anchorMin = Vector2.zero;
            pageNumRt.anchorMax = Vector2.one;
            pageNumRt.offsetMin = Vector2.zero;
            pageNumRt.offsetMax = Vector2.zero;

            // Book Title Badge (Center Top)
            GameObject titleBadgeObj = new GameObject("BookTitleBadge");
            titleBadgeObj.transform.SetParent(navCanvasObj.transform, false);
            Image titleBadgeImg = titleBadgeObj.AddComponent<Image>();
            titleBadgeImg.color = new Color(0.10f, 0.13f, 0.18f, 0.90f);
            RectTransform titleBadgeRt = titleBadgeObj.GetComponent<RectTransform>();
            titleBadgeRt.anchoredPosition = new Vector2(0, 430);
            titleBadgeRt.sizeDelta = new Vector2(460, 54);

            GameObject titleTextObj = new GameObject("Text");
            titleTextObj.transform.SetParent(titleBadgeObj.transform, false);
            TextMeshProUGUI bookTitleText = titleTextObj.AddComponent<TextMeshProUGUI>();
            bookTitleText.text = "Interactive AR Biology Book";
            bookTitleText.fontSize = 22;
            bookTitleText.fontStyle = FontStyles.Bold;
            bookTitleText.color = new Color(0.90f, 0.93f, 0.98f);
            bookTitleText.alignment = TextAlignmentOptions.Center;
            RectTransform titleRt = titleTextObj.GetComponent<RectTransform>();
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;

            // 8. Attach ARTracingBook Component & Configure Spreads
            ARTracingBook bookComp = bookRoot.AddComponent<ARTracingBook>();
            var bookSo = new SerializedObject(bookComp);

            bookSo.FindProperty("_leftBoard").objectReferenceValue = leftBoard;
            bookSo.FindProperty("_rightBoard").objectReferenceValue = rightBoard;
            bookSo.FindProperty("_prevPageButton").objectReferenceValue = prevButton;
            bookSo.FindProperty("_nextPageButton").objectReferenceValue = nextButton;
            bookSo.FindProperty("_pageNumberText").objectReferenceValue = pageNumText;
            bookSo.FindProperty("_bookTitleText").objectReferenceValue = bookTitleText;
            bookSo.FindProperty("_turningPageLeaf").objectReferenceValue = leafPivot.transform;
            bookSo.FindProperty("_completionThreshold").floatValue = 0.95f;

            // Configure the 4 Page Spreads (8 diagrams total)
            var pagesProp = bookSo.FindProperty("_pages");
            pagesProp.ClearArray();

            string[] titles = {
                "Spread 1: Organelles (Mitochondria & Chloroplast)",
                "Spread 2: Cellular Biology (Animal & Plant Cells)",
                "Spread 3: Human Anatomy & Neurology (Heart & Neuron)",
                "Spread 4: Molecular & Atomic Structures (DNA & Atom)"
            };

            for (int p = 0; p < 4; p++)
            {
                pagesProp.InsertArrayElementAtIndex(p);
                var elem = pagesProp.GetArrayElementAtIndex(p);
                elem.FindPropertyRelative("spreadTitle").stringValue = titles[p];

                int leftIdx = p * 2;
                int rightIdx = leftIdx + 1;

                if (leftIdx < diagrams.Count)
                    elem.FindPropertyRelative("leftDiagram").objectReferenceValue = diagrams[leftIdx];

                if (rightIdx < diagrams.Count)
                    elem.FindPropertyRelative("rightDiagram").objectReferenceValue = diagrams[rightIdx];
            }

            bookSo.ApplyModifiedPropertiesWithoutUndo();

            // Save Prefab
            PrefabUtility.SaveAsPrefabAsset(bookRoot, BookPrefabPath);
            UnityEngine.Object.DestroyImmediate(bookRoot);

            Debug.Log($"[BuildBook] Saved 4-Page ARTracingBook prefab to {BookPrefabPath}");
        }

        private static GameObject CreateNavButton(Transform parent, string name, string label, Vector2 position, Vector2 size, Color bgColor, Color textColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.2f);
            colors.disabledColor = new Color(0.4f, 0.45f, 0.5f, 0.5f);
            btn.colors = colors;

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchoredPosition = position;
            rt.sizeDelta = size;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = textColor;
            tmp.alignment = TextAlignmentOptions.Center;

            RectTransform textRt = textObj.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            return btnObj;
        }

        #endregion

        #region Scene Spawner Update

        private static void UpdateSceneSpawner()
        {
            GameObject bookPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefabPath);
            if (bookPrefab == null) return;

            string scenePath = "Assets/Scenes/ARDiagramTracing.unity";
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            var spawner = UnityEngine.Object.FindFirstObjectByType<ARPlaneDiagramSpawner>();
            if (spawner != null)
            {
                var spawnerSo = new SerializedObject(spawner);
                spawnerSo.FindProperty("_tracingBoardPrefab").objectReferenceValue = bookPrefab;
                spawnerSo.ApplyModifiedProperties();
                EditorUtility.SetDirty(spawner);
                EditorSceneManager.MarkSceneDirty(spawner.gameObject.scene);
                EditorSceneManager.SaveScene(spawner.gameObject.scene);
                Debug.Log("[UpdateScene] Set ARPlaneDiagramSpawner prefab to ARTracingBook in ARDiagramTracing.unity!");
            }
        }

        #endregion
    }
}
