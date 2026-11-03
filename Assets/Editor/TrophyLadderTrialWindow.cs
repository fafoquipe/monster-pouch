using System;
using System.Linq;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Progression;
using MonsterPouch.Local.Boards;
using UnityEditor;
using UnityEngine;

public sealed class TrophyLadderTrialWindow : EditorWindow
{
    [NonSerialized] TrophyLadderEngine engine;
    [NonSerialized] TrophyLadderState state;
    [NonSerialized] string message;
    [NonSerialized] BoardProgressionScreen boardScreen;
    [NonSerialized] string selectedBoardId = "classic";
    [NonSerialized] bool spanish = true;
    [NonSerialized] bool ownedHotelFixture;
    [NonSerialized] Sprite trophyIcon;

    [MenuItem("Tools/Monster Pouch/Ladder de prueba")]
    public static void Open() => GetWindow<TrophyLadderTrialWindow>("Ladder de prueba");

    void OnEnable()
    {
        minSize = new Vector2(370, 540);
        trophyIcon = Resources.Load<Sprite>("MonsterPouch/UI/Progression/trophy");
        ResetSession();
    }

    void OnDisable()
    {
        if (boardScreen != null && Application.isPlaying) boardScreen.Close();
        boardScreen = null;
    }

    void ResetSession()
    {
        engine = null; state = null; message = ""; selectedBoardId = "classic";
        try
        {
            var catalogue = TrophyLadderTrialConfiguration.CreateCatalogue();
            if (!TrophyLadderEngine.TryCreate(TrophyLadderTrialConfiguration.CreatePolicy(), catalogue, out engine, out var reason))
                throw new InvalidOperationException(reason);
            state = engine.CreateState("internal-qa.editor." + Guid.NewGuid().ToString("N"));
        }
        catch (Exception error) { engine = null; state = null; message = error.Message; }
        RefreshBoardScreen();
        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Simulador QA en memoria. No usa tu cuenta ni guarda copas. La sesión se reinicia al cerrar o recargar el editor.", MessageType.Info);
        if (engine != null && state != null)
        {
            var preview = engine.PreviewStage(state);
            if (preview.Status == TrophyLadderStatus.Ready)
            {
                EditorGUILayout.LabelField("Copas", state.CurrentTrophies.ToString());
                EditorGUILayout.LabelField("Máximo alcanzado", state.HighestTrophies.ToString());
                var style = Resources.Load<EnvironmentStyle>("MonsterPouch/Styles/" + preview.BoardId);
                EditorGUILayout.LabelField("Tablero de la etapa", style != null ? style.DisplayName : preview.BoardId);
                EditorGUILayout.LabelField("Resultados simulados", state.Revision.ToString());
                EditorGUILayout.LabelField("Siguiente etapa", preview.NextStageAt.HasValue
                    ? preview.NextStageAt.Value + " copas (faltan " + (preview.NextStageAt.Value - state.CurrentTrophies) + ")"
                    : "Recorrido completo; copas sin tope de etapa");
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Simular victoria")) Apply(TrophyMatchOutcome.Victory);
                    if (GUILayout.Button("Simular derrota")) Apply(TrophyMatchOutcome.Defeat);
                }
            }
            else EditorGUILayout.HelpBox(preview.Reason, MessageType.Error);
        }
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, engine == null ? MessageType.Error : MessageType.Info);
        EditorGUILayout.Space();
        if (GUILayout.Button("Reiniciar sesión QA")) ResetSession();
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Pantalla de tableros", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        spanish = EditorGUILayout.Toggle("Español (English al desmarcar)", spanish);
        ownedHotelFixture = EditorGUILayout.Toggle("Fixture QA: Hotel en propiedad", ownedHotelFixture);
        trophyIcon = (Sprite)EditorGUILayout.ObjectField("Icono de trofeo aprobado", trophyIcon, typeof(Sprite), false);
        if (EditorGUI.EndChangeCheck())
        {
            if (!ownedHotelFixture && selectedBoardId == "lounge") selectedBoardId = "classic";
            RefreshBoardScreen();
        }
        EditorGUILayout.HelpBox("La selección y la propiedad de esta vista son fixtures en memoria. No cambian tu tablero equipado, tu colección ni tu cuenta. El icono queda vacío hasta asignar el recurso aprobado.", MessageType.Info);
        if (state != null && !CreateBoardScreenState().SelectionDefined)
            EditorGUILayout.HelpBox("Escenario de selección pendiente de decisión: descenso tras alcanzar Campo Real, o Hotel antes de 150 copas. La vista mantiene sus botones desactivados; no define una regla de producto.", MessageType.Warning);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying || state == null))
        {
            if (GUILayout.Button("Abrir TABLEROS en Play Mode"))
            {
                if (boardScreen != null) boardScreen.Close();
                boardScreen = BoardProgressionScreen.Open(CreateBoardScreenState(), SelectBoardInMemory, trophyIcon);
            }
        }
        if (!EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Entra en Play Mode para abrir la pantalla Unity. Escape/atrás cierra la vista.", MessageType.None);
        using (new EditorGUI.DisabledScope(boardScreen == null))
            if (GUILayout.Button("Cerrar pantalla de tableros")) boardScreen.Close();
    }

    void Apply(TrophyMatchOutcome outcome)
    {
        string matchId = Guid.NewGuid().ToString("N");
        var result = engine.Record(state, new TrophyMatchReceipt {
            PlayerId = state.PlayerId, OpponentPlayerId = "internal-qa.editor-opponent", MatchId = matchId,
            EligibilityEvidenceId = "internal-qa.simulated." + matchId, SourceModeId = "internal-qa.editor-simulation",
            PlayerTrophiesBefore = state.CurrentTrophies, OpponentTrophiesBefore = state.CurrentTrophies,
            Outcome = outcome, Eligibility = TrophyMatchEligibility.ApprovedCompetitive
        }, state.Revision);
        if (result.Status == TrophyLadderStatus.Applied)
        {
            state = result.State;
            long delta = result.RecordedMatch.AppliedDelta;
            message = (outcome == TrophyMatchOutcome.Victory ? "Victoria: " : "Derrota: ") + (delta > 0 ? "+" : "") + delta + " copas.";
        }
        else message = result.Status + ": " + result.Reason;
        RefreshBoardScreen();
        Repaint();
    }

    BoardScreenState CreateBoardScreenState()
    {
        var preview = engine.PreviewStage(state);
        if (preview.Status != TrophyLadderStatus.Ready) throw new InvalidOperationException(preview.Reason);
        var policy = TrophyLadderTrialConfiguration.CreatePolicy();
        var stage = policy.Stages.Single(item => item.StageId == preview.StageId);
        var first = policy.Stages[0];
        var last = policy.Stages[policy.Stages.Length - 1];
        bool finalStage = preview.StageId == last.StageId;
        bool undefinedDescent = !finalStage && state.HighestTrophies >= last.MinimumTrophies;
        bool undefinedEarlyHotel = !finalStage && ownedHotelFixture;
        bool defined = !undefinedDescent && !undefinedEarlyHotel;
        return new BoardScreenState {
            Spanish = spanish,
            Trophies = state.CurrentTrophies,
            CurrentBoardId = preview.BoardId,
            StageMinimum = preview.StageMinimumTrophies,
            NextStageAt = preview.NextStageAt,
            AdjacentBoardId = finalStage ? first.BoardId : last.BoardId,
            AdjacentIsNext = !finalStage,
            VictoryGain = policy.DifferenceBands[0].WinGain,
            DefeatLoss = stage.DefeatLossOverride ?? policy.DifferenceBands[0].LossAmount,
            SelectedBoardId = selectedBoardId,
            SelectionDefined = defined,
            Options = new[] {
                new BoardSelectionOption { BoardId = first.BoardId, CanSelect = defined, Status = BoardOptionStatus.Available },
                new BoardSelectionOption { BoardId = last.BoardId, CanSelect = defined && finalStage,
                    Status = finalStage ? BoardOptionStatus.Available : BoardOptionStatus.LockedByTrophies, UnlockAt = last.MinimumTrophies },
                new BoardSelectionOption { BoardId = "lounge", CanSelect = defined && finalStage && ownedHotelFixture,
                    Status = ownedHotelFixture ? BoardOptionStatus.OwnedCosmetic : BoardOptionStatus.PaidCosmetic }
            }
        };
    }

    void SelectBoardInMemory(string boardId)
    {
        var presentation = CreateBoardScreenState();
        if (!presentation.SelectionDefined || !presentation.Options.Any(option => option.BoardId == boardId && option.CanSelect)) return;
        selectedBoardId = boardId;
        RefreshBoardScreen();
        Repaint();
    }

    void RefreshBoardScreen()
    {
        if (boardScreen == null) return;
        if (engine == null || state == null) { boardScreen.Close(); return; }
        boardScreen.Present(CreateBoardScreenState());
        boardScreen.SetTrophyIcon(trophyIcon);
    }
}
