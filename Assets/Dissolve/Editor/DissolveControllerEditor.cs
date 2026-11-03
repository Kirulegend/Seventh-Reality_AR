using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DissolveEffect.Editor
{
    [CustomEditor(typeof(DissolveController))]
    public class DissolveControllerEditor : UnityEditor.Editor
    {
        Material _materialToAssign, _uiMaterialToAssign;

        public override void OnInspectorGUI()
        {
            DissolveController controller = (DissolveController)target;
            DrawDefaultInspector();

            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("Dissolve Live Controls", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            float newAmount = EditorGUILayout.Slider("Dissolve Progress", controller.CurrentAmount, 0f, 1f);
            if ( EditorGUI.EndChangeCheck() )
            {
                Undo.RecordObject(controller, "Adjust Dissolve");
                controller.SetDissolve(newAmount);
                SceneView.RepaintAll();
            }

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.3f, 0.9f, 0.5f, 1f);
            if ( GUILayout.Button("Appear (Dissolve In)", GUILayout.Height(30)) )
            {
                if ( Application.isPlaying ) controller.DissolveIn();
                else controller.SetDissolve(0f);
            }
            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f, 1f);
            if ( GUILayout.Button("Disappear (Dissolve Out)", GUILayout.Height(30)) )
            {
                if ( Application.isPlaying ) controller.DissolveOut();
                else controller.SetDissolve(1f);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Color Palettes", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 1f, 1f);
            if ( GUILayout.Button("Electric Cyan Arc (Image 2)", GUILayout.Height(26)) )
            {
                Color cyanEdge = new Color(0f, 0.8f, 1f, 1f);
                Color cyanCore = new Color(2f, 2.5f, 3f, 1f);
                Color cyanAsh = new Color(0.04f, 0.06f, 0.09f, 1f);
                controller.SetEdgeColors(cyanEdge, cyanCore);
                controller.SetAshColor(cyanAsh);
            }
            GUI.backgroundColor = new Color(1f, 0.6f, 0.1f, 1f);
            if ( GUILayout.Button("Fiery Flame (Image 1)", GUILayout.Height(26)) )
            {
                Color fireEdge = new Color(1f, 0.42f, 0.04f, 1f);
                Color fireCore = new Color(2.8f, 2.3f, 1.4f, 1f);
                Color fireAsh = new Color(0.05f, 0.04f, 0.03f, 1f);
                controller.SetEdgeColors(fireEdge, fireCore);
                controller.SetAshColor(fireAsh);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Batch Assign 3D Mesh Material", EditorStyles.boldLabel);
            _materialToAssign = (Material)EditorGUILayout.ObjectField("3D Material", _materialToAssign, typeof(Material), false);
            if ( _materialToAssign && GUILayout.Button("Apply Material To 3D Renderers", GUILayout.Height(28)) )
            {
                var renderers = controller.GetComponentsInChildren<Renderer>(true);
                Undo.RecordObjects(renderers, "Assign Dissolve Material");
                foreach ( var r in renderers )
                {
                    if ( !r ) continue;
                    Material[] mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = _materialToAssign;
                    r.sharedMaterials = mats;
                    EditorUtility.SetDirty(r);
                }
                controller.CollectRenderers();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Batch Assign UI Dissolve Material", EditorStyles.boldLabel);
            _uiMaterialToAssign = (Material)EditorGUILayout.ObjectField("UI Material", _uiMaterialToAssign, typeof(Material), false);
            if ( _uiMaterialToAssign && GUILayout.Button("Apply Material To Child UI Images", GUILayout.Height(28)) )
            {
                var graphics = controller.GetComponentsInChildren<Graphic>(true);
                Undo.RecordObjects(graphics, "Assign UI Dissolve Material");
                foreach ( var g in graphics )
                {
                    if ( !g ) continue;
                    g.material = _uiMaterialToAssign;
                    EditorUtility.SetDirty(g);
                }
                controller.CollectRenderers();
            }

            EditorGUILayout.Space(6);
            if ( GUILayout.Button("Refresh Hierarchy Cache (Renderers & Canvases)", GUILayout.Height(24)) )
                controller.CollectRenderers();
        }
    }
}
