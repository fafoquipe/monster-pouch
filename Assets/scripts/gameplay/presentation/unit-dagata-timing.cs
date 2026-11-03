using System;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    public sealed partial class UnitPresentation
    {
        Func<float> dagataElapsedSource;
        Func<DagataJumpSnapshot> dagataJumpSource;
        DagataJumpSnapshot dagataJump;
        float dagataLastElapsed;
        bool displacementTimedDagata;

        // At a .1s simulation tick the existing short recovery must still show its
        // authored return pose, rather than holding contact through half of it.
        int DagataAttackFrame(UnitDirectionalAnimation clip,float windup,float recovery,bool contact)=>
            !contact?CharacterCombatMotion.AttackFrame(clip,windup,recovery,false):
            recovery>0?clip.Attack.Length-1:Mathf.Clamp(clip.AttackContactFrame,0,clip.Attack.Length-1);
        int DagataSpecialFrame(UnitDirectionalAnimation clip,float recovery)=>
            recovery>0?clip.Special.Length-1:Mathf.Min(1,clip.Special.Length-1);

        public void BindDagataTiming(Func<float> elapsed,Func<DagataJumpSnapshot> jump)
        {
            if(art?.Id!="dagata"){dagataElapsedSource=null;dagataJumpSource=null;return;}
            dagataElapsedSource=elapsed;dagataJumpSource=jump;ResetDagataPresentationClock();
        }
        void ResetDagataPresentationClock()
        {
            if(dagataElapsedSource!=null)dagataLastElapsed=dagataElapsedSource();
        }
        float DagataPresentationDelta(float fallback)
        {
            if(dagataElapsedSource==null)return fallback;
            float now=dagataElapsedSource();
            if(float.IsNaN(now)||float.IsInfinity(now)){dagataLastElapsed=float.NaN;return fallback;}
            float delta=float.IsNaN(dagataLastElapsed)?0:Mathf.Max(0,now-dagataLastElapsed);
            dagataLastElapsed=now;return delta;
        }
        void ReadDagataJump()
        {
            if(dagataJumpSource==null)return;
            var sample=dagataJumpSource();
            bool valid=sample.Active&&sample.Unit==unit&&sample.ResetVersion==unit.CombatResetVersion&&
                !dead&&!reviving&&!frozen&&unit.IsAlive&&!unit.IsStunned&&!unit.IsRooted&&!unit.IsBeingPulled&&
                mapper!=null&&sample.From!=null&&sample.To!=null;
            if(!valid)
            {
                if(displacementTimedDagata){SnapToCell();ResetPose();AnimateIdle();}
                dagataJump=default;return;
            }
            dagataJump=sample;
            displacementActive=displacementTimedDagata=true;displacementTimedTunnel=false;
            displacementKind=CombatDisplacementKind.OpeningJump;
            displacementResetVersion=sample.ResetVersion;displacementDestination=sample.To;
            displacementFrom=mapper.GetWorldPosition(sample.From);displacementTo=mapper.GetWorldPosition(sample.To);
            transform.position=mapper.GetWorldPosition(unit.CurrentCell);
            FaceWorld(displacementTo-displacementFrom);
        }
        void AnimateDagataJump()
        {
            var sample=dagataJump;
            if(!sample.Active)return;
            float flight=sample.Timing.TravelProgress(sample.Tick);
            Vector3 ground=Vector3.Lerp(displacementFrom,displacementTo,Mathf.SmoothStep(0,1,flight));
            displacementGroundWorldPosition=ground;displacementGroundY=ground.y;
            Vector3 offset=transform.InverseTransformVector(ground-transform.position);
            float arc=Mathf.Sin(flight*Mathf.PI)*Mathf.Abs(mapper.CellSize.y)*TileHopProfile.For(art.Id).Height;
            var clip=art.Animation(facing);
            var poses=clip?.OpeningJump;
            if(poses==null||poses.Length<3)poses=clip?.HopLeft;
            if(poses!=null&&poses.Length>=3)ShowFrame(poses[sample.Timing.Pose(sample.Tick)],clip);
            visualRoot.localPosition=offset+transform.InverseTransformVector(Vector3.up*arc);
            shadow.transform.localPosition=offset+new Vector3(0,-.035f,.02f);
            float air=Mathf.Sin(flight*Mathf.PI);
            shadow.transform.localScale*=1-air*.09f;
            shadow.color=new Color(.015f,.025f,.035f,ShadowOpacity-air*.05f);
        }
    }
}
