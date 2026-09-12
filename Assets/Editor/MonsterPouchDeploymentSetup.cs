using System;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

/// <summary>Migrates only deployment masks on the existing match asset to the two complete 6x5 halves.</summary>
public static class MonsterPouchDeploymentSetup
{
    private const string ConfigPath = "Assets/Resources/MonsterPouch/MatchConfig.asset";

    [MenuItem("Monster Pouch/Rules/Use five deployment rows per side")]
    public static void Setup()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Sal del modo Play antes de actualizar el despliegue.");
        MatchConfig config = AssetDatabase.LoadAssetAtPath<MatchConfig>(ConfigPath);
        if (config == null)
            throw new InvalidOperationException("No se encontró MatchConfig.asset. Prepara primero el juego local.");

        MatchConfig defaults = MatchConfig.CreateDefault();
        try
        {
            var serialized = new SerializedObject(config);
            serialized.Update();
            SetMask(serialized.FindProperty(nameof(MatchConfig.BlueDeployment)), defaults.BlueDeployment);
            SetMask(serialized.FindProperty(nameof(MatchConfig.RedDeployment)), defaults.RedDeployment);
            if (serialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(defaults); }
        Debug.Log("Monster Pouch: despliegue de 6 columnas × 5 filas por lado. Blue 5–9; Red 0–4. Solo se actualizaron las dos máscaras.");
    }

    private static void SetMask(SerializedProperty property, Vector2Int[] cells)
    {
        if (property == null) throw new InvalidOperationException("Falta una propiedad de máscara de despliegue.");
        property.arraySize = cells.Length;
        for (int i = 0; i < cells.Length; i++) property.GetArrayElementAtIndex(i).vector2IntValue = cells[i];
    }
}
