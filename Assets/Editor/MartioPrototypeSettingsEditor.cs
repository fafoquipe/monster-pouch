using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MartioPrototypeSettings))]
public sealed class MartioPrototypeSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("Borrador de Martio · Whelp. No incorpora al personaje al mazo. Is Defined debe permanecer desactivado para valores aún pendientes. La velocidad de vuelo se edita aquí, pero no aparece en las habilidades.", MessageType.Info);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Draft"), true);
        serializedObject.ApplyModifiedProperties();
        var draft = ((MartioPrototypeSettings)target).Draft;
        if (draft == null) return;
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Textos de habilidades", EditorStyles.boldLabel);
        BlackText(MartioAbilityText.Basic(draft));
        BlackText(MartioAbilityText.Explosion(draft));
        BlackText(MartioAbilityText.Piercing);
        BlackText(MartioAbilityText.MoonUpgrade(draft));
        foreach (var pending in draft.PendingDecisions()) EditorGUILayout.LabelField("Pendiente: " + pending, EditorStyles.wordWrappedLabel);
    }

    static void BlackText(string text)
    {
        var style = new GUIStyle(EditorStyles.wordWrappedLabel) { richText = false };
        style.normal.textColor = Color.black;
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), style, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, Color.white);
        GUI.Label(rect, text, style);
    }
}
