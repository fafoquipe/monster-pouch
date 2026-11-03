using MonsterPouch.Gameplay.Presentation;
using UnityEditor;
using UnityEngine;

/// <summary>Embedded in the existing basic/ability inspector; no standalone editor window.</summary>
public static class MonsterPouchProjectileVisualControls
{
    public static void Draw(SerializedObject owner)
    {
        var slots = owner.FindProperty("ProjectileVisuals");
        if (slots == null || slots.arraySize == 0) return;
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField(BalanceText.L("Projectile appearance", "Aspecto del proyectil"), EditorStyles.boldLabel);
        for (int i = 0; i < slots.arraySize; i++) DrawSlot(slots.GetArrayElementAtIndex(i));
    }

    static void DrawSlot(SerializedProperty slot)
    {
        string label = slot.FindPropertyRelative(BalanceText.Spanish ? "NameSpanish" : "NameEnglish").stringValue;
        var enabled = slot.FindPropertyRelative("OverrideSize");
        var value = slot.FindPropertyRelative("SizePercent");
        string tooltip = BalanceText.L("Visual size only. 0 hides the flying projectile; its effects still occur. 50 is one tile; 100 sets the largest visible dimension across the animation to two tiles, preserving proportions. Includes bright rings and glow, excluding nearly transparent specks.",
            "Solo tamaño visual. 0 oculta el proyectil en vuelo; sus efectos siguen ocurriendo. 50 es una casilla; 100 ajusta la dimensión visible máxima de la animación a dos casillas y conserva las proporciones. Incluye anillos y resplandor brillantes; excluye motas casi transparentes.");
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            enabled.boolValue = EditorGUILayout.Toggle(new GUIContent(BalanceText.L("Use custom visual size", "Usar tamaño visual personalizado"), tooltip), enabled.boolValue);
            if (enabled.boolValue)
            {
                EditorGUI.BeginChangeCheck();
                float next = EditorGUILayout.Slider(new GUIContent(BalanceText.L("Visual size (0–100)", "Tamaño visual (0–100)"), tooltip),
                    ProjectileVisualSlot.Clamp(value.floatValue), 0, 100);
                if (EditorGUI.EndChangeCheck()) value.floatValue = ProjectileVisualSlot.Clamp(next);
            }
            EditorGUILayout.LabelField(enabled.boolValue ? BalanceText.L("Active · custom size", "Activo · tamaño personalizado") :
                BalanceText.L("Active · original appearance", "Activo · aspecto original"), EditorStyles.miniLabel);
            string note = slot.FindPropertyRelative(BalanceText.Spanish ? "LegacyNoteSpanish" : "LegacyNoteEnglish").stringValue;
            if (!enabled.boolValue)
            {
                EditorGUILayout.LabelField(BalanceText.L("Current size", "Tamaño actual"), BalanceText.L("original appearance", "aspecto original"));
                if (!string.IsNullOrEmpty(note)) EditorGUILayout.HelpBox(note, MessageType.None);
            }
            MonsterPouchProjectileFieldPreview.Draw(slot);
            if (GUILayout.Button(BalanceText.L("Restore original appearance", "Restaurar aspecto original"))) enabled.boolValue = false;
            EditorGUILayout.HelpBox(tooltip + " " + BalanceText.L("Save, then restart Play to refresh active projectiles.",
                "Guarda y vuelve a iniciar Play para actualizar los proyectiles activos."), MessageType.None);
        }
    }

}
