using System;
using System.IO;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Profiles;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    // Only account-specific team/map preferences are scoped. Language/audio remain device settings.
    public static class LocalAccountPreferences
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static string DevelopmentScope;
#endif
        public static string Key(string key)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevelopmentScope != null) return "monster-pouch.development." + DevelopmentScope + "." + key;
#endif
            return key;
        }
    }

    public sealed partial class LocalGameUI
    {
        string AccountDeckDirectory => DevelopmentAccountDirectory("PlayerDecks") ?? DeckDirectoryOverride ?? Path.Combine(Application.persistentDataPath, "PlayerDecks");
        string AccountProgressDirectory => DevelopmentAccountDirectory("PlayerProgress") ?? ProgressionDirectoryOverride ?? Path.Combine(Application.persistentDataPath, "PlayerProgress");
        string AccountDailyGiftDirectory => DevelopmentAccountDirectory("DailyGifts") ?? (GuestProfileDirectoryOverride != null ? Path.Combine(GuestProfileDirectoryOverride, "DailyGifts") : Path.Combine(Application.persistentDataPath, "PlayerDailyGifts"));
        string LegacyAccountValue(string key) => DevelopmentAccountDirectory("GuestProfile") != null ? "" : PlayerPrefs.GetString(key, "");
        string DevelopmentAccountDirectory(string child)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (developmentAccountRequested)
                return Path.Combine(developmentAccountStore?.GenerationId != null ? developmentAccountStore.AccountDirectory :
                    Path.Combine(DevelopmentPersistentDirectoryOverride ?? Application.persistentDataPath, DevelopmentAccountStore.FolderName, "Unavailable"), child);
#endif
            return null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static string DevelopmentPersistentDirectoryOverride { get; set; }
        DevelopmentAccountStore developmentAccountStore;
        bool developmentAccountRequested, developmentAccountChanging;
        bool DevelopmentAccountActive => developmentAccountRequested && developmentAccountStore?.GenerationId != null && guestProfile != null && guestProfileError == null;

        bool SetupDevelopmentAccount()
        {
            LocalAccountPreferences.DevelopmentScope = null;
            if (DevelopmentPersistentDirectoryOverride == null &&
                (GuestProfileDirectoryOverride != null || DeckDirectoryOverride != null || ProgressionDirectoryOverride != null)) return false;
            developmentAccountRequested = true;
            LocalAccountPreferences.DevelopmentScope = "unavailable";
            developmentAccountStore = new DevelopmentAccountStore(DevelopmentPersistentDirectoryOverride ?? Application.persistentDataPath);
            var asset = Resources.Load<MatchConfig>("MonsterPouch/MatchConfig");
            var catalogue = asset != null ? asset.CreateRuntimeConfig() : MatchConfig.CreateDefault();
            try { guestProfile = developmentAccountStore.Open(catalogue); }
            finally { Destroy(catalogue); }
            LocalAccountPreferences.DevelopmentScope = guestProfile.ProfileId;
            guestProfileStore = new LocalGuestProfileStore(Path.Combine(developmentAccountStore.AccountDirectory, "GuestProfile"));
            guestProfileError = null;
            return true;
        }

        bool CanChangeDevelopmentAccount => DevelopmentAccountActive && !developmentAccountChanging &&
            Match != null && Match.Phase == MatchPhase.Menu && !Match.IsTraining && !Match.IsOnline && !MapTransitionActive &&
            !TrophySavePending && pendingProgressAwards.Count == 0 && pendingPouchVictories.Count == 0 &&
            pendingPouchOpenRequests.Count == 0 && pendingAbilityPurchases.Count == 0 && pendingVendingRequests.Count == 0 &&
            !pouchRoomBusy && !permanentAbilityBusy && !vendingBusy && pendingDeckJson == null &&
            progressError == null && deckStorageError == null && dailyGiftError == null && !guestProfileStore.NeedsRecovery &&
            (deckStore == null || !deckStore.NeedsRecovery) && (progressStore == null || !progressStore.NeedsRecovery);

        void ShowDevelopmentAccountConfirmation(bool fresh)
        {
            if (!DevelopmentAccountActive) return;
            string title = fresh ? (GameText.Spanish ? "COMENZAR DESDE CERO" : "START FROM ZERO") :
                (GameText.Spanish ? "DESBLOQUEAR TODO" : "UNLOCK ALL");
            var box = LobbyModal("Development account confirmation", title, 175, 620, ShowSettings);
            bool ready = CanChangeDevelopmentAccount;
            string body = !ready ? (GameText.Spanish ?
                "Vuelve a Home y resuelve los guardados pendientes antes de cambiar la cuenta de desarrollo." :
                "Return Home and resolve pending saves before changing the development account.") : fresh ? (GameText.Spanish ?
                "Solo DESARROLLO. Se guardará un respaldo y el juego reiniciará con una cuenta nueva: elección inicial, tutorial y progreso cero. Tu cuenta normal se conserva. Este modo seguirá activo al reiniciar." :
                "DEVELOPMENT only. A backup will be saved and the game will restart with a new account: starter choice, tutorial and zero progress. Your normal account is preserved. This mode persists after restarting.") : (GameText.Spanish ?
                "Solo DESARROLLO. Habilita los personajes, habilidades permanentes y tableros para pruebas. Conserva el progreso y las monedas actuales. Las mejoras de combate mantienen sus costes." :
                "DEVELOPMENT only. Enables characters, permanent abilities and boards for testing. Keeps current progress and currencies. Combat upgrades retain their costs.");
            LobbyNumber(box, body, new Rect(28, 100, 436, 276), 21, LobbyPaper);
            string requestId = Guid.NewGuid().ToString("N");
            string generation = developmentAccountStore.GenerationId;
            bool submitted = false;
            var confirm = LobbyButton(box, "Confirm development account change", new Rect(32, 412, 428, 80), () =>
            {
                if (submitted || !CanChangeDevelopmentAccount) return;
                submitted = true;
                ChangeDevelopmentAccount(fresh, generation, requestId);
            }, true);
            confirm.GetComponent<Button>().interactable = ready;
            Label(confirm, fresh ? (GameText.Spanish ? "RESPALDAR Y REINICIAR" : "BACK UP AND RESTART") :
                (GameText.Spanish ? "DESBLOQUEAR Y REINICIAR" : "UNLOCK AND RESTART"), new Rect(12, 10, 404, 60), 22, LobbyInk, FontStyle.Bold);
            var cancel = LobbyButton(box, "Cancel development account change", new Rect(90, 510, 312, 68), ShowSettings);
            Label(cancel, GameText.Spanish ? "CANCELAR" : "CANCEL", new Rect(8, 8, 296, 52), 22, LobbyPaper, FontStyle.Bold);
        }

        void ChangeDevelopmentAccount(bool fresh, string generation, string requestId)
        {
            if (!CanChangeDevelopmentAccount) return;
            developmentAccountChanging = true;
            bool committed = false;
            try
            {
                if (fresh) developmentAccountStore.StartFresh(generation, requestId, Match.Config);
                else developmentAccountStore.UnlockAll(generation, Match.Config);
                committed = true;
                // Recreate every view/store and stop old callbacks against the retained generation.
                var scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                SceneManager.LoadScene(scene.buildIndex, LoadSceneMode.Single);
#endif
            }
            catch (Exception error) when (committed || IsDeckStorageError(error))
            {
                if (committed)
                {
                    // The new active pointer is durable. Do not resume writes through old stores.
                    ShowLobbyNotice("DEVELOPMENT", GameText.Spanish ? "El modo se guardó. Cierra y abre el juego para continuar." : "The mode was saved. Close and reopen the game to continue.");
                    var gate = CanvasRoot.gameObject.GetComponent<CanvasGroup>() ?? CanvasRoot.gameObject.AddComponent<CanvasGroup>();
                    gate.interactable = false;
                }
                else
                {
                    developmentAccountChanging = false;
                    ShowLobbyNotice("DEVELOPMENT", GameText.Spanish ?
                        "No se pudo guardar el cambio. Tu cuenta anterior y los respaldos se conservan. Reintenta desde Ajustes." :
                        "The change could not be saved. Your previous account and backups are preserved. Retry from Settings.");
                }
            }
        }
#endif
    }
}
