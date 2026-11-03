using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        Text duelSearchStatus;
        bool duelErrorDisplayed;

        long ReadAutomaticDuelTrophies()
        {
            if (!ReloadGuestAvailability() || !TrophyProfileReady || TrophySavePending)
                throw new InvalidOperationException("Trophy profile is not ready.");
            return AccountTrophies;
        }
        public async void OpenAutomaticDuel()
        {
            if (Match.IsOnline) return;
            if (online.DuelPending) { ShowAutomaticDuelSearch(); return; }
            if (online.Active || online.Busy) return;
            if (!PrepareCurrentDeckForMatch()) { CloseModal(); RenderPage(); return; }
            ShowAutomaticDuelSearch();
            await online.SearchDuel(chosen, chosenWhelps.ToArray());
        }
        void ShowAutomaticDuelSearch()
        {
            roomView = null;
            CloseModal(); joinPanel = null; keyboardShift = 0;
            roomView = "duel"; duelErrorDisplayed = false;
            modal = Panel(CanvasRoot, "Automatic duel", new Rect(0, 0, 540, 960), new Color(.015f, .025f, .055f, 1));
            modal.gameObject.AddComponent<AutomaticDuelScreenLifetime>().Closed = () =>
            {
                if (roomView != "duel" || Match.IsOnline) return;
                roomView = null; online.Leave();
            };
            float center = (LobbyTop + LobbyBottom) * .5f;
            Figure(modal, chosen, new Rect(140, center - 205, 260, 260), false);
            duelSearchStatus = Label(modal, GameText.Spanish ? "Buscando" : "Searching", new Rect(40, center + 70, 460, 56), 30, Color.white, FontStyle.Bold);
            GameButton(modal, GameText.Spanish ? "CANCELAR" : "CANCEL", new Rect(140, center + 160, 260, 64), CancelDuelSearch, false, 23);
        }
        void CancelDuelSearch()
        {
            roomView = null; online.Leave(); CloseModal();
        }
        void HandleOnlineFailure()
        {
            if (online.AutomaticDuel) ShowAutomaticDuelFailure();
            else OpenOnline();
        }
        void ShowAutomaticDuelFailure()
        {
            roomView = null; duelErrorDisplayed = true;
            var box = RoomModal("Automatic duel", GameText.Spanish ? "DUELO" : "DUEL", 290, 380, CancelDuelSearch);
            roomView = "duel";
            Label(box, GameText.Spanish ? "No se pudo conectar con un rival. Vuelve a intentarlo." : "Could not connect to an opponent. Please try again.", new Rect(24, 90, 444, 100), 23, Color.white);
            GameButton(box, GameText.Spanish ? "REINTENTAR" : "RETRY", new Rect(84, 232, 324, 68), OpenAutomaticDuel, true, 25);
        }
        bool RefreshAutomaticDuel()
        {
            if (roomView != "duel") return false;
            if (Match.IsOnline) { roomView = null; CloseModal(); return true; }
            if (modal == null || modal.name != "Automatic duel")
            {
                // Closing/replacing the screen cannot leave an invisible search.
                roomView = null; online.Leave(); return true;
            }
            if (online.DuelState == AutomaticDuelState.Failed && !online.DuelPending)
            {
                if (!duelErrorDisplayed) ShowAutomaticDuelFailure();
                return true;
            }
            if (online.DuelState == AutomaticDuelState.Idle && !online.DuelPending)
            { roomView = null; CloseModal(); return true; }
            if (duelSearchStatus != null && !duelErrorDisplayed)
                duelSearchStatus.text = online.DuelState == AutomaticDuelState.Cancelling ?
                    (GameText.Spanish ? "Cancelando…" : "Cancelling…") : (GameText.Spanish ? "Buscando" : "Searching");
            return true;
        }
    }

    sealed class AutomaticDuelScreenLifetime : MonoBehaviour
    {
        public Action Closed;
        void OnDisable() { Closed?.Invoke(); }
    }
}
