using System;
using System.Collections.Generic;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    public sealed partial class MatchController
    {
        public bool IsTraining { get; private set; }
        public TrainingLesson CurrentTrainingLesson { get; private set; }
        public BattleUnit TrainingTarget { get; private set; }
        public bool TrainingCombatFinished { get; private set; }
        public bool TrainingCombatWon { get; private set; }
        public int TrainingAttackCount { get; private set; }
        public int TrainingDamageDealt { get; private set; }
        public int TrainingAbilityCount { get; private set; }
        public string TrainingWhelpId { get; private set; }
        readonly List<OwnedUnit> trainingTargets = new List<OwnedUnit>();
        MatchConfig trainingRivalConfig;
        TrainingCombatOptions trainingCombatOptions;
        bool trainingCombatStarting;
        bool TrainingEconomyAllowed => IsTraining && (CurrentTrainingLesson==TrainingLesson.PreparationEconomy||CurrentTrainingLesson==TrainingLesson.TacticalBank);

        /// <summary>
        /// Creates the first-contact practice board, without a combat session,
        /// opponent deployment, preparation timer or changes to normal balance.
        /// Placement and movement keep using the normal board validation.
        /// </summary>
        public bool StartTraining(string monsterId, IEnumerable<string> whelpIds = null)
            => StartTraining(monsterId,whelpIds,TrainingLesson.FirstContact);

        public bool StartTraining(string monsterId, IEnumerable<string> whelpIds, TrainingLesson lesson, TrainingCombatOptions options=null)
        {
            if (IsOnline) return Reject("Sal de la sala antes de iniciar el tutorial.");
            if (Phase != MatchPhase.Menu && !IsTraining) return Reject("Vuelve al menú antes de iniciar el tutorial.");
            if (Config == null || Board == null || Mapper == null) return Reject("No se cargó la configuración del tutorial.");
            if (!Config.TryResolveWhelpSelection(whelpIds, out string[] selection, out string reason)) return Reject(reason);
            UnitDefinition monster = Config.Get(monsterId);
            if (monster == null || !monster.IsMonster) return Reject("Elige un Monster válido.");
            if(!Enum.IsDefined(typeof(TrainingLesson),lesson))return Reject("Módulo de entrenamiento inválido.");
            bool economy=lesson==TrainingLesson.PreparationEconomy||lesson==TrainingLesson.TacticalBank;
            if((economy||lesson==TrainingLesson.Positioning)&&selection.Length==0)return Reject("El equipo necesita un Whelp para esta práctica.");
            int trainingSeed = CreateRuntimeSeed(seed);
            PouchState nextPlayer, nextBot;
            MatchConfig nextRivalConfig=null;
            TrainingCombatOptions resolvedOptions=options?.Copy();
            try
            {
                nextPlayer = new PouchState(Config, trainingSeed, monsterId, BoardSide.Blue, economy?selection.Take(1):selection);
                string rivalId=monsterId;
                if(lesson==TrainingLesson.SimpleCombat)
                {
                    resolvedOptions=resolvedOptions??TrainingCombatOptions.ForStarterOpponent(monsterId);
                    if(resolvedOptions==null||!resolvedOptions.IsValid(Config))return Reject("Falta una configuración válida del rival de práctica.");
                    rivalId=resolvedOptions.RivalMonsterId;
                    nextRivalConfig=Config.CreateRuntimeConfig();
                    var source=nextRivalConfig.Get(rivalId);
                    int index=Array.IndexOf(nextRivalConfig.Units,source);
                    nextRivalConfig.Units[index]=TrainingStatScaling.Weaken(source,resolvedOptions.StatMultiplier);
                }
                nextBot = new PouchState(nextRivalConfig??Config, trainingSeed ^ 0x45fa1, rivalId, BoardSide.Red, selection);
            }
            catch (ArgumentException error) { DisposeTrainingConfig(nextRivalConfig);return Reject(error.Message); }

            CancelMoonkenDeliveries(); Simulation?.Stop(); Simulation = null;
            ClearActors(); Board.BuildBoard(); ResetOnlinePreparation();
            ResetTrainingState();trainingRivalConfig=nextRivalConfig;
            previousOpponent.Clear(); InitialOpponent = null;
            IsTraining = true;CurrentTrainingLesson=lesson;trainingCombatOptions=resolvedOptions;
            Paused = false; Time.timeScale = 1; Mapper.Flipped = false;
            seed = trainingSeed; chosenMonster = monsterId; chosenWhelpIds = selection.ToArray();
            Player = nextPlayer; Bot = nextBot;
            Player.BeginTrainingPreparation();
            TrainingWhelpId=selection.FirstOrDefault();
            Bot.Monster.Location = UnitLocation.Brief;
            Bot.Monster.BriefSlot = -1;
            SessionId = null; Round = 1; PlayerWins = BotWins = 0;
            LastRoundWinner = null; RoundResultElapsed = 0; accumulator = 0;
            Phase = MatchPhase.Preparation; Remaining = 0; Result = Notice = "";
            if(lesson==TrainingLesson.Positioning||economy)
            {
                EnsureMonsterPosition(Player);
                if(lesson==TrainingLesson.Positioning||lesson==TrainingLesson.TacticalBank)
                {
                    var practice=Player.AddTrainingWhelp(TrainingWhelpId);
                    var cell=FormationCell(Player,practice);
                    if(cell!=null)Player.TrySetLocation(TrainingWhelpId,UnitLocation.Field,cell.Coordinates,out _);
                }
                if(economy)Player.PrepareTrainingOffers(lesson==TrainingLesson.PreparationEconomy);
            }
            if(lesson==TrainingLesson.SimpleCombat)
            {
                Bot.BeginTrainingPreparation();EnsureMonsterPosition(Bot);
            }
            SyncActors();
            if(lesson==TrainingLesson.Positioning||lesson==TrainingLesson.AttacksHealth)CreateTrainingDummy();
            Changed?.Invoke();
            return true;
        }

        public bool BeginTrainingCombat()
        {
            if(!IsTraining||Paused||Phase!=MatchPhase.Preparation||Player.Monster.Location!=UnitLocation.Field||
                (CurrentTrainingLesson!=TrainingLesson.AttacksHealth&&CurrentTrainingLesson!=TrainingLesson.SimpleCombat))return false;
            trainingCombatStarting=true;
            try{BeginCombat();}
            finally{trainingCombatStarting=false;}
            return Phase==MatchPhase.Combat;
        }

        public bool RestartTraining()=>IsTraining&&StartTraining(chosenMonster,chosenWhelpIds,CurrentTrainingLesson,trainingCombatOptions);

        void CreateTrainingDummy()
        {
            var definition=ExpandedRoster.CreateSummonedDummy();
            definition.Damage=0;
            if(CurrentTrainingLesson==TrainingLesson.AttacksHealth)
            {
                var stats=UnitStats.FromDefinition(Player.Monster.Definition);
                int energyHits=stats.EnergyPerAttack>0?Mathf.CeilToInt(stats.EnergyMax/stats.EnergyPerAttack):0;
                // Survive a complete real energy cycle plus two basic hits. No
                // energy is granted to the player and the target is not immortal.
                definition.MaxHealth=Mathf.Max(definition.MaxHealth,checked((Mathf.Max(1,energyHits)+2)*Mathf.Max(1,stats.Attack)));
            }
            var cell=Config.FormationCells(Bot.Monster.Definition,BoardSide.Red).Select(p=>Board.GetCell(p.x,p.y))
                .FirstOrDefault(c=>c!=null&&!c.IsBlocked&&c.OccupiedBy==null);
            if(cell==null)return;
            var owned=new OwnedUnit(definition){Location=UnitLocation.Field,Deployment=cell.Coordinates,Copies=1};
            var go=new GameObject("Training-Red-dummy");go.transform.SetParent(transform,false);
            var actor=go.AddComponent<WhelpUnit>();
            actor.Initialize("Training-Red-dummy",BoardSide.Red,UnitStats.FromDefinition(definition),true);
            actor.IsPassiveTrainingTarget=true;
            if(!Board.TryOccupyCell(actor,cell.X,cell.Y)){Dispose(go);return;}
            go.transform.position=Mapper.GetWorldPosition(cell);
            trainingTargets.Add(owned);actors.Add(owned,actor);TrainingTarget=actor;
            ActorCreated?.Invoke(actor,owned);
        }

        void AdvanceTraining(float dt)
        {
            if(Phase!=MatchPhase.Combat||TrainingCombatFinished||Simulation==null)return;
            accumulator+=dt;
            while(accumulator+.00001f>=.1f&&!TrainingCombatFinished)
            {
                accumulator-=.1f;Simulation.Step(.1f);
                if(!Simulation.Finished)continue;
                TrainingCombatFinished=true;TrainingCombatWon=Simulation.Winner==LocalSide;
                // Keep the practice result visible for retry/continue, without
                // entering RoundResult/MatchResult or incrementing BO5 counters.
                Remaining=0;Changed?.Invoke();
            }
        }

        int TrainingEnergyCycleTickBudget()
        {
            var stats=UnitStats.FromDefinition(Player.Monster.Definition);
            int attacks=stats.EnergyPerAttack>0?Mathf.CeilToInt(stats.EnergyMax/stats.EnergyPerAttack)+3:3;
            float action=BasicAttackTiming.TrainingCycleSeconds(stats);
            float seconds=Config.OpeningAbilityWindowSeconds+attacks*action+BoardManager.Width*BoardManager.Height*stats.MoveInterval;
            return Mathf.Max(CombatSimulation.TimeoutTicks,Mathf.CeilToInt(seconds/CombatSimulation.TickDuration));
        }

        void ResetTrainingState()
        {
            IsTraining=false;CurrentTrainingLesson=TrainingLesson.FirstContact;trainingTargets.Clear();TrainingTarget=null;
            TrainingCombatFinished=TrainingCombatWon=false;TrainingAttackCount=TrainingDamageDealt=TrainingAbilityCount=0;
            TrainingWhelpId=null;trainingCombatOptions=null;trainingCombatStarting=false;
            DisposeTrainingConfig(trainingRivalConfig);trainingRivalConfig=null;
        }

        static void DisposeTrainingConfig(MatchConfig config)
        {if(config!=null){if(Application.isPlaying)Destroy(config);else DestroyImmediate(config);}}
    }
}
