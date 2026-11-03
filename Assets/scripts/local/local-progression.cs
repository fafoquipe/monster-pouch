using System.Collections;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Progression;
using UnityEngine;
using UnityEngine.UI;
namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        const string ProgressKey="monster-pouch.progress.v1";
        PlayerProgress progress;
        ProgressionConfig progressionConfig;
        LocalProgressStore progressStore;
        string progressError;
        readonly List<KeyValuePair<string,bool>> pendingProgressAwards=new List<KeyValuePair<string,bool>>();
        // Set only by the test assembly before loading a scene. Tests must never
        // import or write the player's real progress files.
        internal static string ProgressionDirectoryOverride { get; set; }
        void SetupProgression()
        {
            progressionConfig=Resources.Load<ProgressionConfig>("MonsterPouch/Progression");
            progressStore=new LocalProgressStore(AccountProgressDirectory);
            LoadProgression();
            Match.Changed-=UpdateProgression;
            Match.Changed+=UpdateProgression;
        }
        void LoadProgression()
        {
            try
            {
                progress=progressStore.LoadOrImport(LegacyAccountValue(ProgressKey));
                progressError=progressStore.NeedsRecovery?"recovery":null;
            }
            catch(Exception error) when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is ArgumentException||error is NotSupportedException)
            {progressError=error is NotSupportedException?"version":"load";}
        }
        void UpdateProgression()
        {
            UpdatePouchRoomVictory();
            RetryTrophyResults();
            if(progressStore==null)return;
            if(!Match.IsTraining&&Match.Phase==MatchPhase.MatchResult&&!string.IsNullOrWhiteSpace(Match.SessionId)&&
                progress?.CompletedMatches.Contains(Match.SessionId)!=true&&!pendingProgressAwards.Any(p=>p.Key==Match.SessionId))
                pendingProgressAwards.Add(new KeyValuePair<string,bool>(Match.SessionId,Match.PlayerWins>=Match.WinsToWin));
            // Keep failed receipts in memory so returning to the menu or starting
            // another match does not discard the result before a retry can commit.
            while(pendingProgressAwards.Count>0)
            {
                try
                {
                    var next=pendingProgressAwards[0];
                    var result=progressStore.Award(progressionConfig,next.Key,next.Value,out var committed);
                    if(committed!=null)progress=committed;
                    if(result==LocalProgressStore.AwardResult.RecoveryRequired){progressError="recovery";return;}
                    if(result==LocalProgressStore.AwardResult.InvalidRequest){progressError="save";return;}
                    pendingProgressAwards.RemoveAt(0);progressError=null;
                }
                catch(Exception error) when(error is IOException||error is InvalidDataException||error is UnauthorizedAccessException||error is ArgumentException||error is NotSupportedException||error is InvalidOperationException)
                {progressError=error is NotSupportedException?"version":"save";return;}
            }
        }
        public void ShowProgression()
        {
            if(progressStore==null)SetupProgression();CloseModal();
            modal=Panel(CanvasRoot,"Player progression",new Rect(0,0,540,960),new Color(.10f,.14f,.16f,.98f));
            Image(modal,SuppliedBranding.Logo,new Rect(70,75,400,200));
            Label(modal,GameText.Spanish?"PROGRESO":"PROGRESS",new Rect(40,285,460,55),30,RetroButtonMotion.Cream,FontStyle.Bold);
            GameButton(modal,GameText.Back,new Rect(140,820,260,68),CloseModal);
            if(progress==null)
            {
                Label(modal,ProgressErrorText(),new Rect(50,385,440,180),20,RetroButtonMotion.Cream);
                ProgressRetryButton();return;
            }
            Label(modal,(GameText.Spanish?"NIVEL ":"LEVEL ")+progress.Level,new Rect(40,360,460,55),32,RetroButtonMotion.Gold,FontStyle.Bold);
            int needed=progressionConfig!=null?progressionConfig.Requirement(progress.Level):100;
            var bar=Panel(modal,"XP track",new Rect(50,455,440,30),new Color(.06f,.08f,.1f),false);
            var fill=Panel(bar,"XP",new Rect(3,3,1,24),new Color(.31f,.65f,.73f),false);
            Label(modal,progress.Experience+" / "+needed+" XP",new Rect(50,496,440,30),20,Color.white);
            Label(modal,progressError!=null?ProgressErrorText():(GameText.Spanish?"Los desbloqueables configurados aparecerán aquí.":"Configured unlocks will appear here."),new Rect(60,570,420,60),17,RetroButtonMotion.Cream);
            if(progress.Unlocks.Count>0)Label(modal,string.Join(" · ",progress.Unlocks.Select(ProgressionRewardName)),new Rect(50,640,440,90),17,Color.white);
            if(progressError!=null)ProgressRetryButton();
            StartCoroutine(FillProgress(fill,Mathf.Clamp01((float)progress.Experience/needed)));
        }
        void ProgressRetryButton()=>GameButton(modal,GameText.Spanish?"REINTENTAR":"RETRY",new Rect(140,740,260,68),()=>
        {
            LoadProgression();UpdateProgression();ShowProgression();
        });
        string ProgressErrorText()
        {
            if(progressError=="version")return GameText.Spanish?"Este progreso requiere una versión más reciente del juego. Tus datos se conservan.":"This progress requires a newer game version. Your data is preserved.";
            if(progressError=="recovery")return GameText.Spanish?"Se muestra un respaldo. El progreso necesita recuperarse antes de seguir guardando.":"A backup is shown. Progress must be recovered before saving again.";
            return GameText.Spanish?"No se pudo cargar o guardar el progreso. Tus datos anteriores se conservan.":"Progress could not be loaded or saved. Your previous data is preserved.";
        }
        string ProgressionRewardName(string id)
        {
            var character=Match.Config.Get(id);
            if(character!=null&&!string.IsNullOrWhiteSpace(character.DisplayName))return character.DisplayName;
            var reward=progressionConfig?.Rewards?.FirstOrDefault(r=>r!=null&&r.UnlockId==id);
            return !string.IsNullOrWhiteSpace(reward?.DisplayName)?GameText.Resolve(reward.DisplayName):id;
        }
        IEnumerator FillProgress(RectTransform fill,float fraction)
        {
            for(float age=0;age<.5f&&fill!=null;age+=Time.unscaledDeltaTime)
            {SetRect(fill,new Rect(3,3,434*fraction*Mathf.SmoothStep(0,1,age/.5f),24));yield return null;}
            if(fill!=null)SetRect(fill,new Rect(3,3,434*fraction,24));
        }
    }
}
