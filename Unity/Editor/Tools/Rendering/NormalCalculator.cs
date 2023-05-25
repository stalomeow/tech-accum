using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StaloTechAccum.Unity.Editor.Tools.Rendering
{
    public class NormalCalculator : EditorWindow
    {
        [MenuItem("Tools/Rendering/Smooth Normal")]
        public static void Open()
        {
            GetWindow<NormalCalculator>(true, "Smooth Normal");
        }

        [SerializeField] private SkinnedMeshRenderer m_Renderer;

        private void OnGUI()
        {
            m_Renderer = (SkinnedMeshRenderer)EditorGUILayout.ObjectField("Model", m_Renderer, typeof(SkinnedMeshRenderer), true);
            m_InGameModel = (GameObject)EditorGUILayout.ObjectField("In Game Model", m_InGameModel, typeof(GameObject), true);

            using (new EditorGUI.DisabledScope(m_Renderer == null))
            {
                if (GUILayout.Button("Calculate"))
                {
                    Execute();
                    Debug.Log("Done");
                }
            }
        }

        private void Execute()
        {
            Mesh mesh = m_Renderer.sharedMesh;
            Vector3[] normals = mesh.normals;
            Vector3[] vertices = mesh.vertices;
            Dictionary<Vector3, Vector3> v2nMap = new();

            for (int i = 0; i < vertices.Length; i++)
            {
                if (v2nMap.TryGetValue(vertices[i], out Vector3 normal))
                {
                    v2nMap[vertices[i]] = (normals[i] + normal).normalized;
                }
                else
                {
                    v2nMap[vertices[i]] = normals[i];
                }
            }

            Vector4[] tangents = new Vector4[vertices.Length];

            for (int i = 0; i < vertices.Length; i++)
            {
                tangents[i] = v2nMap[vertices[i]];
            }

            mesh.tangents = tangents;
            mesh.UploadMeshData(false);
        }
    }
}