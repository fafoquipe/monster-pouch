using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Profiles;
using MonsterPouch.Gameplay.Match;
using UnityEditor;
using UnityEngine;

public sealed class RoadmapProfileWindow : EditorWindow
{
    EconomyDevelopmentConfig settings;
    LocalQAProfileStore store;
    LocalEconomyProfile profile;
    MatchConfig catalogue;
    string message;
    string selectedCharacterId;
    Vector2 scrollPosition;

    [MenuItem("Tools/Monster Pouch/Roadmap/Perfil de pruebas")]
    static void Open() => GetWindow<RoadmapProfileWindow>("Perfil QA");

    void OnEnable()
    {
        settings = CreateInstance<EconomyDevelopmentConfig>();
        settings.hideFlags = HideFlags.HideAndDontSave;
        store = new LocalQAProfileStore(Path.Combine(Application.persistentDataPath, "RoadmapQA"));
        var asset=Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
        if(asset!=null){catalogue=asset.CreateRuntimeConfig();catalogue.hideFlags=HideFlags.HideAndDontSave;}
    }
    void OnDisable()
    {
        if(settings!=null)DestroyImmediate(settings);
        if(catalogue!=null)DestroyImmediate(catalogue);
    }
    string CharacterName(string id)=>catalogue?.Get(id)?.DisplayName??"Personaje no disponible ("+id+")";
    void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.HelpBox("Perfil interno de pruebas. No son monedas de jugador ni compras. El progreso existente se copia sin borrarlo.", MessageType.Info);
        settings.EnableLocalQA = EditorGUILayout.Toggle("Habilitar economía QA", settings.EnableLocalQA);
        settings.starterSaturncoins = EditorGUILayout.IntField("Saturncoins iniciales (provisional)", settings.StarterAmount);
        if (GUILayout.Button("Abrir o crear perfil QA"))
            Run(() => { profile = store.OpenOrCreate(PlayerPrefs.GetString("monster-pouch.progress.v1", "")); message = "Perfil cargado."; });
        using (new EditorGUI.DisabledScope(profile == null || !settings.EnableLocalQA || store.NeedsRecovery))
        {
            if (GUILayout.Button("Conceder bienvenida una sola vez"))
                Run(() => { message = store.GrantWelcome(settings).ToString(); profile = store.OpenOrCreate(); });
            EditorGUILayout.LabelField("Monster inicial",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(profile?.Starter!=null||catalogue==null))
            foreach(var id in StarterRoster.Monsters)
                if(GUILayout.Button("Elegir "+CharacterName(id)))
                    Run(() => {
                        message=store.ChooseStarter(settings,catalogue,id).ToString();profile=store.OpenOrCreate();
                    });
        }
        if (profile != null)
        {
            EditorGUILayout.SelectableLabel("ID: " + profile.ProfileId, GUILayout.Height(20));
            EditorGUILayout.LabelField("Revisión", profile.Revision.ToString());
            foreach (PermanentCurrency currency in Enum.GetValues(typeof(PermanentCurrency)))
                EditorGUILayout.LabelField(currency + " QA", profile.Balance(currency, CreditOrigin.InternalQA).ToString());
            EditorGUILayout.LabelField("Operaciones registradas", profile.Transactions.Count.ToString());
            if(profile.Starter!=null)
            {
                EditorGUILayout.LabelField("Elección guardada",CharacterName(profile.Starter.Deck.Monster));
                EditorGUILayout.LabelField("Whelps",string.Join(", ",profile.Starter.Deck.Whelps.Select(CharacterName)),EditorStyles.wordWrappedLabel);
            }
            DrawInventory();
        }
        if (store.NeedsRecovery)
            EditorGUILayout.HelpBox("Se pudo leer el respaldo, pero el archivo principal necesita recuperación. Se bloquean concesiones y gastos para no repetir operaciones perdidas.", MessageType.Error);
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.None);
        EditorGUILayout.EndScrollView();
    }
    void DrawInventory()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Inventario interno QA", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Personajes", profile.OwnedCharacterIds.Count.ToString());
        EditorGUILayout.LabelField("Habilidades", profile.OwnedAbilities.Count.ToString());
        EditorGUILayout.LabelField("Recibos de desbloqueo", profile.InventoryReceipts.Count.ToString());
        EditorGUILayout.HelpBox("Usa los personajes y habilidades del catálogo actual. Estos desbloqueos no cambian tu colección jugable ni conceden monedas.", MessageType.Info);
        var units = catalogue?.Units?.Where(u => u != null).ToArray();
        if (units == null || units.Length == 0) return;
        int selected = Array.FindIndex(units, u => u.Id == selectedCharacterId);
        selected = EditorGUILayout.Popup("Personaje", Mathf.Max(0, selected), units.Select(u => u.DisplayName).ToArray());
        var character = units[selected];
        selectedCharacterId = character.Id;
        using (new EditorGUI.DisabledScope(!settings.EnableLocalQA || store.NeedsRecovery))
        {
            using (new EditorGUI.DisabledScope(profile.OwnsCharacter(character.Id)))
                if (GUILayout.Button(profile.OwnsCharacter(character.Id) ? "Personaje obtenido" : "Desbloquear personaje (QA)"))
                    GrantInventory(character.Id, null);
            if (character.Tricks == null) return;
            foreach (var ability in character.Tricks)
            {
                if (ability == null || string.IsNullOrWhiteSpace(ability.Id)) continue;
                bool owned = profile.OwnsAbility(character.Id, ability.Id);
                using (new EditorGUI.DisabledScope(!profile.OwnsCharacter(character.Id) || owned))
                    if (GUILayout.Button(ability.Name + (owned ? " · obtenida" : " · desbloquear (QA)")))
                        GrantInventory(character.Id, ability.Id);
            }
        }
    }
    void GrantInventory(string characterId, string abilityId)
    {
        Run(() => {
            message = store.GrantInventory(settings, catalogue, "qa-unlock:" + characterId + ":" + abilityId,
                InventoryRewardSource.InternalQA, characterId, abilityId).ToString();
            profile = store.OpenOrCreate();
        });
    }
    void Run(Action action)
    {
        try { action(); }
        catch (Exception error) { message = error.Message; }
    }
}
