using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RutherfordSim.Bio.Tracing.Editor
{
    [InitializeOnLoad]
    public static class CreateARTracingBoardPrefab
    {
        private const string PrefabDirectory = "Assets/DiagramTracing/Prefabs";
        private const string PrefabPath = "Assets/DiagramTracing/Prefabs/ARTracingBoard.prefab";

        static CreateARTracingBoardPrefab()
        {
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(PrefabPath))
                {
                    GeneratePrefab();
                }
            };
        }

        [MenuItem("Tools/Diagram Tracing/Setup AR Diagram Tracing in Scene", false, 11)]
        public static void SetupInActiveScene()
        {
            if (!File.Exists(PrefabPath))
            {
                GeneratePrefab();
            }

            GameObject boardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (boardPrefab == null)
            {
                Debug.LogError("Could not load ARTracingBoard prefab at " + PrefabPath);
                return;
            }

            var spawnerObj = GameObject.Find("AR Diagram Spawner");
            if (spawnerObj == null)
            {
                spawnerObj = new GameObject("AR Diagram Spawner");
                Undo.RegisterCreatedObjectUndo(spawnerObj, "Create AR Diagram Spawner");
            }

            var spawner = spawnerObj.GetComponent<ARPlaneDiagramSpawner>();
            if (spawner == null)
            {
                spawner = Undo.AddComponent<ARPlaneDiagramSpawner>(spawnerObj);
            }

            var spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("_tracingBoardPrefab").objectReferenceValue = boardPrefab;
            spawnerSo.FindProperty("_spawnOnlyOnce").boolValue = true;
            spawnerSo.FindProperty("_heightOffset").floatValue = 0.005f;
            spawnerSo.ApplyModifiedProperties();

            // Also register with ObjectSpawner if present in the scene
            var objectSpawner = Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets.ObjectSpawner>();
            if (objectSpawner != null)
            {
                var osSo = new SerializedObject(objectSpawner);
                var prefabsProp = osSo.FindProperty("m_ObjectPrefabs");
                bool alreadyInList = false;
                for (int i = 0; i < prefabsProp.arraySize; i++)
                {
                    if (prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue == boardPrefab)
                    {
                        alreadyInList = true;
                        break;
                    }
                }
                if (!alreadyInList)
                {
                    prefabsProp.InsertArrayElementAtIndex(prefabsProp.arraySize);
                    prefabsProp.GetArrayElementAtIndex(prefabsProp.arraySize - 1).objectReferenceValue = boardPrefab;
                    osSo.ApplyModifiedProperties();
                    Debug.Log("[Setup] Added ARTracingBoard to ObjectSpawner prefabs list.");
                }
            }

            EditorUtility.SetDirty(spawnerObj);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(spawnerObj.scene);
            Debug.Log("[Setup] AR Diagram Tracing configured successfully in scene!");
        }

        [MenuItem("Tools/Diagram Tracing/Generate AR Tracing Board Prefab", false, 10)]
        public static void GeneratePrefab()
        {
            if (!Directory.Exists(PrefabDirectory))
            {
                Directory.CreateDirectory(PrefabDirectory);
                AssetDatabase.Refresh();
            }

            // Load default diagram data if available
            DiagramDataSO defaultData = AssetDatabase.LoadAssetAtPath<DiagramDataSO>(
                "Assets/DiagramTracing/Examples/MitochondriaSmoothTracingData.asset"
            );

            // 1. Root Board GameObject
            GameObject boardRoot = new GameObject("ARTracingBoard");

            // Add thin physical board backing (3D Quad / Cube) so it feels like a real physical drafting tablet/board
            GameObject backPlate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backPlate.name = "BoardBackingPlate";
            backPlate.transform.SetParent(boardRoot.transform, false);
            backPlate.transform.localPosition = new Vector3(0, -0.005f, 0); // slightly under the canvas
            backPlate.transform.localRotation = Quaternion.Euler(90f, 0, 0); // flat facing up
            backPlate.transform.localScale = new Vector3(0.54f, 0.46f, 0.01f); // 54cm x 46cm x 1cm

            // Create clean material for backing plate
            var plateRenderer = backPlate.GetComponent<Renderer>();
            if (plateRenderer != null)
            {
                Material plateMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                plateMat.color = new Color(0.12f, 0.15f, 0.2f, 1f); // Sleek modern dark slate backing
                plateMat.SetFloat("_Smoothness", 0.6f);
                string matPath = "Assets/DiagramTracing/Prefabs/M_BoardBacking.mat";
                AssetDatabase.CreateAsset(plateMat, matPath);
                plateRenderer.sharedMaterial = plateMat;
            }

            // 2. World Space Canvas
            GameObject canvasObj = new GameObject("WorldCanvas");
            canvasObj.transform.SetParent(boardRoot.transform, false);
            canvasObj.transform.localPosition = new Vector3(0, 0.002f, 0); // slightly above plate
            canvasObj.transform.localRotation = Quaternion.Euler(90f, 0, 0); // flat facing up along plane normal
            canvasObj.transform.localScale = new Vector3(0.0006f, 0.0006f, 0.0006f); // 1 canvas unit = 0.6mm

            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2;

            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(850, 720);

            // 3. Background Board Panel
            GameObject bgPanelObj = new GameObject("BackgroundPanel");
            bgPanelObj.transform.SetParent(canvasObj.transform, false);
            Image bgImage = bgPanelObj.AddComponent<Image>();
            bgImage.color = new Color(0.97f, 0.98f, 1.0f, 0.98f); // clean paper-white
            RectTransform bgRect = bgPanelObj.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Header Container
            GameObject headerObj = new GameObject("Header");
            headerObj.transform.SetParent(bgPanelObj.transform, false);
            RectTransform headerRect = headerObj.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.anchoredPosition = new Vector2(0, -20);
            headerRect.sizeDelta = new Vector2(-40, 70);

            // Title Text
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(headerObj.transform, false);
            TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = defaultData != null ? defaultData.diagramTitle : "Diagram Tracing";
            titleText.fontSize = 28;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Left;
            titleText.color = new Color(0.1f, 0.12f, 0.16f);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 0.5f);
            titleRect.anchorMax = new Vector2(0.7f, 1);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;

            // Subtitle Text
            GameObject subtitleObj = new GameObject("SubtitleText");
            subtitleObj.transform.SetParent(headerObj.transform, false);
            TextMeshProUGUI subtitleText = subtitleObj.AddComponent<TextMeshProUGUI>();
            subtitleText.text = defaultData != null ? defaultData.diagramSummary : "Trace with your finger along the outline.";
            subtitleText.fontSize = 17;
            subtitleText.alignment = TextAlignmentOptions.Left;
            subtitleText.color = new Color(0.4f, 0.45f, 0.52f);
            RectTransform subtitleRect = subtitleObj.GetComponent<RectTransform>();
            subtitleRect.anchorMin = new Vector2(0, 0);
            subtitleRect.anchorMax = new Vector2(0.7f, 0.5f);
            subtitleRect.offsetMin = Vector2.zero;
            subtitleRect.offsetMax = Vector2.zero;

            // 4. Diagram Tracing Area Container
            GameObject diagramAreaObj = new GameObject("DiagramArea");
            diagramAreaObj.transform.SetParent(bgPanelObj.transform, false);
            RectTransform diagramAreaRect = diagramAreaObj.AddComponent<RectTransform>();
            diagramAreaRect.anchorMin = new Vector2(0.04f, 0.18f);
            diagramAreaRect.anchorMax = new Vector2(0.96f, 0.86f);
            diagramAreaRect.offsetMin = Vector2.zero;
            diagramAreaRect.offsetMax = Vector2.zero;

            // Subtle border around diagram canvas
            Image diagramAreaBorder = diagramAreaObj.AddComponent<Image>();
            diagramAreaBorder.color = new Color(0.90f, 0.92f, 0.95f, 1f);

            // Inner Tracing Area
            GameObject innerAreaObj = new GameObject("TracingCanvas");
            innerAreaObj.transform.SetParent(diagramAreaObj.transform, false);
            RectTransform innerRect = innerAreaObj.AddComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(4, 4);
            innerRect.offsetMax = new Vector2(-4, -4);

            // Template RawImage (outline reference)
            GameObject templateObj = new GameObject("TemplateRawImage");
            templateObj.transform.SetParent(innerAreaObj.transform, false);
            RawImage templateImage = templateObj.AddComponent<RawImage>();
            templateImage.color = new Color(0.15f, 0.15f, 0.2f, 0.35f); // semi-transparent outline guide
            templateImage.raycastTarget = false;
            RectTransform templateRect = templateObj.GetComponent<RectTransform>();
            templateRect.anchorMin = Vector2.zero;
            templateRect.anchorMax = Vector2.one;
            templateRect.offsetMin = Vector2.zero;
            templateRect.offsetMax = Vector2.zero;

            // Drawing RawImage (runtime traced ink)
            GameObject drawingObj = new GameObject("DrawingRawImage");
            drawingObj.transform.SetParent(innerAreaObj.transform, false);
            RawImage drawingImage = drawingObj.AddComponent<RawImage>();
            drawingImage.color = Color.white;
            drawingImage.raycastTarget = true; // MUST receive pointer raycasts
            RectTransform drawingRect = drawingObj.GetComponent<RectTransform>();
            drawingRect.anchorMin = Vector2.zero;
            drawingRect.anchorMax = Vector2.one;
            drawingRect.offsetMin = Vector2.zero;
            drawingRect.offsetMax = Vector2.zero;

            // Add DiagramTracingCanvas component
            DiagramTracingCanvas tracingCanvas = innerAreaObj.AddComponent<DiagramTracingCanvas>();
            var tracingSo = new SerializedObject(tracingCanvas);
            tracingSo.FindProperty("_drawingRawImage").objectReferenceValue = drawingImage;
            tracingSo.FindProperty("_templateRawImage").objectReferenceValue = templateImage;
            tracingSo.FindProperty("_rectTransform").objectReferenceValue = innerRect;
            tracingSo.FindProperty("_snappedInkColor").colorValue = new Color(0.05f, 0.72f, 0.54f, 1f); // Vibrant emerald ink
            tracingSo.FindProperty("_useOverrideThreshold").boolValue = true;
            tracingSo.FindProperty("_overrideCompletionThreshold").floatValue = 0.95f; // 95% threshold requirement!
            tracingSo.ApplyModifiedPropertiesWithoutUndo();

            // 5. Bottom Toolbar & Progress Controls
            GameObject bottomBarObj = new GameObject("BottomBar");
            bottomBarObj.transform.SetParent(bgPanelObj.transform, false);
            RectTransform bottomBarRect = bottomBarObj.AddComponent<RectTransform>();
            bottomBarRect.anchorMin = new Vector2(0.04f, 0.03f);
            bottomBarRect.anchorMax = new Vector2(0.96f, 0.16f);
            bottomBarRect.offsetMin = Vector2.zero;
            bottomBarRect.offsetMax = Vector2.zero;

            // Progress Slider
            GameObject sliderObj = new GameObject("ProgressBar");
            sliderObj.transform.SetParent(bottomBarObj.transform, false);
            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;
            slider.interactable = false;
            RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0, 0.55f);
            sliderRect.anchorMax = new Vector2(0.58f, 0.95f);
            sliderRect.offsetMin = Vector2.zero;
            sliderRect.offsetMax = Vector2.zero;

            // Slider Background
            GameObject sliderBgObj = new GameObject("Background");
            sliderBgObj.transform.SetParent(sliderObj.transform, false);
            Image sliderBg = sliderBgObj.AddComponent<Image>();
            sliderBg.color = new Color(0.88f, 0.90f, 0.93f);
            RectTransform sliderBgRect = sliderBgObj.GetComponent<RectTransform>();
            sliderBgRect.anchorMin = Vector2.zero;
            sliderBgRect.anchorMax = Vector2.one;
            sliderBgRect.offsetMin = Vector2.zero;
            sliderBgRect.offsetMax = Vector2.zero;

            // Slider Fill Area
            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.05f, 0.72f, 0.54f); // Emerald fill
            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            slider.fillRect = fillRect;

            // Progress Percentage Text
            GameObject progressTextObj = new GameObject("ProgressText");
            progressTextObj.transform.SetParent(bottomBarObj.transform, false);
            TextMeshProUGUI progressText = progressTextObj.AddComponent<TextMeshProUGUI>();
            progressText.text = "0% / 95%";
            progressText.fontSize = 18;
            progressText.fontStyle = FontStyles.Bold;
            progressText.color = new Color(0.18f, 0.22f, 0.28f);
            progressText.alignment = TextAlignmentOptions.Left;
            RectTransform progressTextRect = progressTextObj.GetComponent<RectTransform>();
            progressTextRect.anchorMin = new Vector2(0, 0);
            progressTextRect.anchorMax = new Vector2(0.58f, 0.45f);
            progressTextRect.offsetMin = Vector2.zero;
            progressTextRect.offsetMax = Vector2.zero;

            // Eraser Toggle Button
            GameObject eraserBtnObj = CreateButton(bottomBarObj.transform, "EraserButton", "Tool: Pen",
                new Vector2(0.60f, 0.15f), new Vector2(0.78f, 0.85f), new Color(0.92f, 0.94f, 0.96f), new Color(0.2f, 0.25f, 0.3f));
            Button eraserButton = eraserBtnObj.GetComponent<Button>();
            TextMeshProUGUI eraserButtonText = eraserBtnObj.GetComponentInChildren<TextMeshProUGUI>();

            // Reset Button
            GameObject resetBtnObj = CreateButton(bottomBarObj.transform, "ResetButton", "Clear",
                new Vector2(0.80f, 0.15f), new Vector2(0.98f, 0.85f), new Color(0.95f, 0.92f, 0.92f), new Color(0.7f, 0.2f, 0.2f));
            Button resetButton = resetBtnObj.GetComponent<Button>();

            // 6. Submit Button (Appears at 95% Completion)
            GameObject submitContainer = new GameObject("SubmitButtonContainer");
            submitContainer.transform.SetParent(bgPanelObj.transform, false);
            RectTransform submitContainerRect = submitContainer.AddComponent<RectTransform>();
            submitContainerRect.anchorMin = new Vector2(0.72f, 0.89f);
            submitContainerRect.anchorMax = new Vector2(0.96f, 0.97f);
            submitContainerRect.offsetMin = Vector2.zero;
            submitContainerRect.offsetMax = Vector2.zero;

            GameObject submitBtnObj = CreateButton(submitContainer.transform, "SubmitButton", "Submit Diagram",
                Vector2.zero, Vector2.one, new Color(0.10f, 0.65f, 0.45f), Color.white);
            Button submitButton = submitBtnObj.GetComponent<Button>();
            var submitText = submitBtnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (submitText != null)
            {
                submitText.fontSize = 20;
                submitText.fontStyle = FontStyles.Bold;
            }

            // Initially deactivate the submit button root
            submitContainer.SetActive(false);

            // 7. Attach and wire up ARTracingBoard component
            ARTracingBoard board = boardRoot.AddComponent<ARTracingBoard>();
            var boardSo = new SerializedObject(board);
            boardSo.FindProperty("_diagramData").objectReferenceValue = defaultData;
            boardSo.FindProperty("_completionThreshold").floatValue = 0.95f;
            boardSo.FindProperty("_tracingCanvas").objectReferenceValue = tracingCanvas;
            boardSo.FindProperty("_worldCanvas").objectReferenceValue = canvas;
            boardSo.FindProperty("_titleText").objectReferenceValue = titleText;
            boardSo.FindProperty("_subtitleText").objectReferenceValue = subtitleText;
            boardSo.FindProperty("_progressSlider").objectReferenceValue = slider;
            boardSo.FindProperty("_progressPercentageText").objectReferenceValue = progressText;
            boardSo.FindProperty("_resetButton").objectReferenceValue = resetButton;
            boardSo.FindProperty("_eraserToggleButton").objectReferenceValue = eraserButton;
            boardSo.FindProperty("_eraserButtonText").objectReferenceValue = eraserButtonText;
            boardSo.FindProperty("_submitButtonRoot").objectReferenceValue = submitContainer;
            boardSo.FindProperty("_submitButton").objectReferenceValue = submitButton;
            boardSo.ApplyModifiedPropertiesWithoutUndo();

            // Save as Prefab
            PrefabUtility.SaveAsPrefabAsset(boardRoot, PrefabPath);
            GameObject.DestroyImmediate(boardRoot);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CreateARTracingBoardPrefab] Successfully generated ARTracingBoard prefab at: {PrefabPath}");
        }

        private static GameObject CreateButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color bgColor, Color textColor)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            Image img = btnObj.AddComponent<Image>();
            img.color = bgColor;

            Button btn = btnObj.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
            colors.pressedColor = Color.Lerp(bgColor, Color.black, 0.15f);
            btn.colors = colors;

            RectTransform rt = btnObj.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(btnObj.transform, false);
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 17;
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
    }
}
