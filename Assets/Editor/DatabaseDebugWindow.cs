using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public class DatabaseDebugWindow : EditorWindow
{
    private Vector2 _scroll;

    [MenuItem("Tools/Database/Debug")]
    public static void OpenWindow()
    {
        var window = GetWindow<DatabaseDebugWindow>("Database Debug");
        window.minSize = new Vector2(420f, 320f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("Database Debug", EditorStyles.largeLabel);
        if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
            Repaint();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Les données sont visibles en direct dans l’éditeur, et la fenêtre se rafraîchit pendant le Play Mode.", MessageType.Info);
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawDefinitions();
        DrawRuntimeInstances();

        EditorGUILayout.EndScrollView();
    }

    private void DrawDefinitions()
    {
        GUILayout.Label("Definitions", EditorStyles.boldLabel);

        if (Database.Definitions == null || Database.Definitions.Count == 0)
        {
            EditorGUILayout.HelpBox("Aucune définition enregistrée.", MessageType.None);
            return;
        }

        foreach (var pair in Database.Definitions)
        {
            var type = pair.Key;
            var defs = pair.Value;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(type.ToString(), EditorStyles.miniBoldLabel);

            foreach (var definition in defs)
            {
                var def = definition.Value;
                if (def == null)
                    continue;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Id : " + def.Id);
                EditorGUILayout.LabelField("Material : " + (def.Material != null ? def.Material.name : "null"));
                EditorGUILayout.LabelField("Mesh : " + (def.Mesh != null ? def.Mesh.name : "null"));
                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
    }

    private void DrawRuntimeInstances()
    {
        GUILayout.Label("Runtime Instances", EditorStyles.boldLabel);

        if (Database.RuntimeInstances == null || Database.RuntimeInstances.Count == 0)
        {
            EditorGUILayout.HelpBox("Aucune instance runtime disponible.", MessageType.None);
            return;
        }

        foreach (var pair in Database.RuntimeInstances)
        {
            var type = pair.Key;
            var byId = pair.Value;

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField(type.ToString(), EditorStyles.miniBoldLabel);

            foreach (var idPair in byId)
            {
                var id = idPair.Key;
                var byNumber = idPair.Value;

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Id : " + id);

                foreach (var numberPair in byNumber)
                {
                    var number = numberPair.Key;
                    var entity = numberPair.Value;

                    var entityName = "null";
                    var em = World.DefaultGameObjectInjectionWorld != null ? World.DefaultGameObjectInjectionWorld.EntityManager : default;
                    if (em != default && em.Exists(entity))
                    {
                        entityName = em.GetName(entity);
                    }

                    EditorGUILayout.LabelField("- " + number + " => " + entity + " | " + entityName);
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
    }
}
