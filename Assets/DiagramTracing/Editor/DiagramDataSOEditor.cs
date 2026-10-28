using System;
using UnityEngine;
using UnityEditor;

namespace RutherfordSim.Bio.Tracing.Editor
{
    [CustomEditor(typeof(DiagramDataSO))]
    public class DiagramDataSOEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DiagramDataSO data = (DiagramDataSO)target;
            DrawDefaultInspector();

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Tracing Path Generator", EditorStyles.boldLabel);

            if ( !data.outlineTexture )
            {
                EditorGUILayout.HelpBox("Assign an Outline Texture above to extract tracing data paths.", MessageType.Warning);
                return;
            }

            int segmentCount = data.pathSegments != null ? data.pathSegments.Count : 0;
            int totalPoints = 0;
            if ( data.pathSegments != null )
            {
                foreach ( var seg in data.pathSegments )
                    if ( seg != null && seg.points != null ) totalPoints += seg.points.Count;
            }

            if ( segmentCount > 0 ) EditorGUILayout.HelpBox($"Tracing data ready: {segmentCount} path segments ({totalPoints} total points).", MessageType.Info);
            else EditorGUILayout.HelpBox("No tracing paths generated yet. Click 'Extract Tracing Paths' below to generate them from the outline PNG.", MessageType.Warning);

            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f, 1f);
            if ( GUILayout.Button("Extract Tracing Paths", GUILayout.Height(32)) ) ExtractPaths(data);
            GUI.backgroundColor = Color.white;

            if ( segmentCount > 0 && GUILayout.Button("Clear Paths", GUILayout.Height(32), GUILayout.Width(90)) )
            {
                Undo.RecordObject(data, "Clear Tracing Paths");
                data.pathSegments.Clear();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            EditorGUILayout.EndHorizontal();
        }

        static void ExtractPaths(DiagramDataSO data)
        {
            if ( !data || !data.outlineTexture ) return;
            try
            {
                string texPath = AssetDatabase.GetAssetPath(data.outlineTexture);
                if ( !string.IsNullOrEmpty(texPath) )
                {
                    var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if ( importer && !importer.isReadable )
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                    }
                }

                Undo.RecordObject(data, "Extract Tracing Paths");
                data.pathSegments = OutlinePathExtractor.Extract(data.outlineTexture);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                Debug.LogError(ex.Message);
            }
        }
    }
}
