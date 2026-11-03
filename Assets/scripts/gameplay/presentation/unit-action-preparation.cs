using System;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    public sealed partial class UnitPresentation
    {
        Func<CombatActionPreparation> actionPreparationSource;
        string activeAbilityEffect;
        CombatActionPreparation actionPreparation;
        int actionResetVersion;
        bool releasedEronyaSpecial;
        bool UsesActionPreparation => actionPreparationSource!=null&&unit!=null&&unit.BaseStats.UsesEronyaRules;

        // Bind only live combat views. Existing idle previews and legacy actors retain their path.
        public void BindActionPreparation(Func<CombatActionPreparation> source,string activeEffect)
        { actionPreparationSource=source;activeAbilityEffect=activeEffect;actionResetVersion=unit?.CombatResetVersion??0; }

        void ClearBoundActionPresentation(bool clearHit=false)
        {
            actionPreparation=default;releasedEronyaSpecial=false;
            if(!UsesActionPreparation)return;
            attacking=specialActive=false;abilityRemaining=0;
            if(clearHit)hitRemaining=0;
            if(visualRoot!=null&&art!=null)ResetPose();
        }

        void ReadActionPreparation()
        {
            actionPreparation=default;
            if(!UsesActionPreparation)return;
            bool reset=actionResetVersion!=unit.CombatResetVersion||!unit.IsAlive||unit.IsRoundFrozen;
            if(reset||unit.IsStunned||unit.IsBeingPulled)
            { ClearBoundActionPresentation(reset);actionResetVersion=unit.CombatResetVersion; }
            if(dead||reviving||frozen||moving||displacementActive||!unit.IsAlive||unit.IsStunned||unit.IsBeingPulled||unit.IsRoundFrozen)return;
            var sample=actionPreparationSource();
            if(sample.Source!=unit||sample.ResetVersion!=unit.CombatResetVersion)return;
            actionPreparation=sample;
            // A real Super preparation supersedes the previous basic's cosmetic return.
            if(sample.IsSuper){attacking=false;specialActive=false;releasedEronyaSpecial=false;}
        }

        bool AnimateActionPreparation()
        {
            if(actionPreparation.Source==null)return false;
            var target=actionPreparation.Target;
            if(target!=null&&target!=unit)FaceWorld(target.transform.position-transform.position);
            var clip=art.Animation(facing);
            bool super=actionPreparation.IsSuper;
            if(clip==null||(super?clip.Special==null||clip.Special.Length==0:clip.Attack==null||clip.Attack.Length==0))return false;
            float windup=Mathf.Clamp01(actionPreparation.Progress);
            if(super)ShowSpecialFrame(clip,CharacterCombatMotion.SpecialFrame(clip,0,false));
            else ShowFrame(clip.Attack[CharacterCombatMotion.AttackFrame(clip,windup,0,false)],clip);
            // Same bounded anticipation as the shared full-body path; retain each character's authored art/tuning.
            float angle=(int)facing*Mathf.PI/4;Vector3 direction=new Vector3(Mathf.Sin(angle),Mathf.Cos(angle),0);
            float lunge=Mathf.Max(0,super?clip.SpecialLungeDistance:clip.AttackLungeDistance);
            visualRoot.localPosition=-direction*(Mathf.SmoothStep(0,1,windup)*lunge*.34f);
            return true;
        }
    }
}
