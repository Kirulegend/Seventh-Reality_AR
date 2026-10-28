using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;

namespace RutherfordSim.Bio.Tracing.Editor
{
    [InitializeOnLoad]
    public static class CleanARDiagramTracingScene
    {
        private const string ScenePath = "Assets/Scenes/ARDiagramTracing.unity";
        private const string BookPrefabPath = "Assets/DiagramTracing/Prefabs/ARTracingBook.prefab";
        private const string BoardPrefabPath = "Assets/DiagramTracing/Prefabs/ARTracingBoard.prefab";

        [MenuItem("Tools/Diagram Tracing/Clean AR Diagram Tracing Scene", false, 20)]
        public static void CleanSceneMenu()
        {
            CleanScene(true);
        }

        public static void CleanScene(bool forceOpen)
        {
            Scene activeScene = SceneManager.GetActiveScene();
            bool isCurrentScene = activeScene.path == ScenePath;

            if (!isCurrentScene)
            {
                if (forceOpen)
                {
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                        return;

                    activeScene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }
                else
                {
                    // Don't disturb active scene if not force opened
                    return;
                }
            }

            Debug.Log("[CleanScene] Cleaning unwanted template objects from ARDiagramTracing.unity...");

            // 1. Load target prefab (prefer ARTracingBook, fallback to ARTracingBoard)
            GameObject targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BookPrefabPath);
            if (targetPrefab == null)
            {
                if (!File.Exists(BoardPrefabPath))
                {
                    CreateARTracingBoardPrefab.GeneratePrefab();
                }
                targetPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BoardPrefabPath);
            }

            if (targetPrefab == null)
            {
                Debug.LogError("[CleanScene] Target prefab not found at " + BoardPrefabPath);
                return;
            }

            // 2. Remove unwanted GameObjects
            string[] unwantedRoots = new string[]
            {
                "UI",
                "Object Spawner",
                "Greeting Prompt",
                "Coaching UI",
                "Delete Button",
                "Object Menu"
            };

            foreach (var rootGo in activeScene.GetRootGameObjects())
            {
                foreach (var unwantedName in unwantedRoots)
                {
                    if (rootGo.name == unwantedName)
                    {
                        Debug.Log($"[CleanScene] Removing unwanted object: {rootGo.name}");
                        Object.DestroyImmediate(rootGo);
                        break;
                    }
                }
            }

            // Also check for any leftover loose tutorial or template objects
            foreach (var go in activeScene.GetRootGameObjects())
            {
                if (go.name.Contains("Prompt_") || go.name.Contains("GreetingCTA") || go.name.Contains("Button (Cube") ||
                    go.name.Contains("Options Modal") || go.name.Contains("Debug Menu"))
                {
                    Debug.Log($"[CleanScene] Removing loose template object: {go.name}");
                    Object.DestroyImmediate(go);
                }
            }

            // 3. Find Core AR Components
            Camera arCamera = Camera.main;
            ARRaycastManager raycastManager = Object.FindFirstObjectByType<ARRaycastManager>();
            ARPlaneManager planeManager = Object.FindFirstObjectByType<ARPlaneManager>();

            // 4. Create Clean Minimalist AR HUD for instructions & repositioning
            GameObject hudGo = GameObject.Find("AR Diagram HUD");
            if (hudGo != null)
                Object.DestroyImmediate(hudGo);

            hudGo = new GameObject("AR Diagram HUD");
            Canvas hudCanvas = hudGo.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 50;

            CanvasScaler hudScaler = hudGo.AddComponent<CanvasScaler>();
            hudScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            hudScaler.referenceResolution = new Vector2(1080, 1920);
            hudScaler.matchWidthOrHeight = 0.5f;

            hudGo.AddComponent<GraphicRaycaster>();

            // Top instruction pill
            GameObject instructionPill = new GameObject("InstructionPill");
            instructionPill.transform.SetParent(hudGo.transform, false);

            Image pillBg = instructionPill.AddComponent<Image>();
            pillBg.color = new Color(0.08f, 0.1f, 0.14f, 0.88f); // dark translucent pill

            RectTransform pillRt = instructionPill.GetComponent<RectTransform>();
            pillRt.anchorMin = new Vector2(0.5f, 1f);
            pillRt.anchorMax = new Vector2(0.5f, 1f);
            pillRt.pivot = new Vector2(0.5f, 1f);
            pillRt.anchoredPosition = new Vector2(0, -90);
            pillRt.sizeDelta = new Vector2(860, 100);

            GameObject pillTextGo = new GameObject("Text");
            pillTextGo.transform.SetParent(instructionPill.transform, false);
            TextMeshProUGUI pillText = pillTextGo.AddComponent<TextMeshProUGUI>();
            pillText.text = "Point camera at a flat surface and tap to place the diagram.";
            pillText.fontSize = 28;
            pillText.fontStyle = FontStyles.Bold;
            pillText.color = Color.white;
            pillText.alignment = TextAlignmentOptions.Center;

            RectTransform pillTextRt = pillTextGo.GetComponent<RectTransform>();
            pillTextRt.anchorMin = Vector2.zero;
            pillTextRt.anchorMax = Vector2.one;
            pillTextRt.offsetMin = new Vector2(20, 10);
            pillTextRt.offsetMax = new Vector2(-20, -10);

            // 5. Create / Configure "AR Diagram Spawner"
            GameObject spawnerGo = GameObject.Find("AR Diagram Spawner");
            if (spawnerGo == null)
            {
                spawnerGo = new GameObject("AR Diagram Spawner");
            }

            var spawner = spawnerGo.GetComponent<ARPlaneDiagramSpawner>();
            if (spawner == null)
            {
                spawner = spawnerGo.AddComponent<ARPlaneDiagramSpawner>();
            }

            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_tracingBoardPrefab").objectReferenceValue = targetPrefab;
            spawnerSo.FindProperty("_raycastManager").objectReferenceValue = raycastManager;
            spawnerSo.FindProperty("_planeManager").objectReferenceValue = planeManager;
            spawnerSo.FindProperty("_arCamera").objectReferenceValue = arCamera;
            spawnerSo.FindProperty("_spawnOnlyOnce").boolValue = true;
            spawnerSo.FindProperty("_heightOffset").floatValue = 0.005f;
            spawnerSo.FindProperty("_hidePlanesAfterSpawn").boolValue = false;
            spawnerSo.FindProperty("_placementInstructionUI").objectReferenceValue = instructionPill;
            spawnerSo.ApplyModifiedProperties();

            // 6. Save Scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            Debug.Log("[CleanScene] Successfully cleaned ARDiagramTracing.unity! Only essential AR and Diagram Tracing objects remain.");
        }
    }
}
