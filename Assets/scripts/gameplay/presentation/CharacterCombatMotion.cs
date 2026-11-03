using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    /// <summary>Body accents for the reference roster. All inputs use the simulation's action phases.</summary>
    public static class CharacterCombatMotion
    {
        enum Gesture { Strike, Slam, Bite, Heart, Drain, Belly, Throw, Seal, Whip, Spark, Plant, Summon, Guard, Treasure }

        readonly struct Profile
        {
            public readonly Gesture Gesture;
            public readonly float Reach, Coil, Lift, WindAngle, HitAngle, Squash, SnapStart;
            public Profile(Gesture gesture, float reach, float coil, float lift, float windAngle, float hitAngle, float squash, float snapStart = .78f)
            { Gesture=gesture; Reach=reach; Coil=coil; Lift=lift; WindAngle=windAngle; HitAngle=hitAngle; Squash=squash; SnapStart=snapStart; }
        }

        public readonly struct Pose
        {
            public readonly Vector3 Offset, Scale;
            public readonly float Angle;
            public Pose(Vector3 offset, float width, float height, float angle)
            { Offset=offset; Scale=new Vector3(width,height,1); Angle=angle; }
        }

        static bool ProfileFor(string id, bool special, out Profile p)
        {
            // Reach/coil are world distances, independent of sprite resolution and character size.
            // The existing hand, jaw, wing and weapon poses supply the limb animation.
            switch(id)
            {
                case "aky": p=new Profile(Gesture.Drain,.32f,.15f,.055f,10,14,.10f); break;
                case "anuik": p=new Profile(Gesture.Heart,.24f,.13f,.13f,9,10,.09f,.73f); break;
                case "atong": p=new Profile(Gesture.Slam,.43f,.24f,.14f,13,17,.15f,.84f); break;
                case "atori": p=new Profile(Gesture.Bite,.34f,.16f,.07f,12,16,.11f,.81f); break;
                case "blotan": p=new Profile(special?Gesture.Treasure:Gesture.Strike,.28f,.14f,.06f,13,12,.10f); break;
                case "bugaloo": p=new Profile(special?Gesture.Guard:Gesture.Belly,.35f,.17f,.17f,8,11,.14f,.76f); break;
                case "bugui": p=new Profile(Gesture.Heart,.22f,.10f,.08f,8,11,.085f,.82f); break;
                case "flo": p=new Profile(special?Gesture.Plant:Gesture.Strike,.26f,.13f,.065f,10,12,.10f); break;
                case "gochan": p=new Profile(Gesture.Seal,.27f,.16f,.09f,12,16,.12f,.82f); break;
                case "hymay": p=new Profile(Gesture.Whip,.34f,.20f,.09f,16,20,.11f,.82f); break;
                case "jazar": p=new Profile(Gesture.Throw,.30f,.19f,.10f,13,17,.115f,.82f); break;
                case "kayon": p=new Profile(special?Gesture.Summon:Gesture.Throw,.28f,.16f,.09f,12,17,.11f); break;
                case "suki": p=new Profile(special?Gesture.Throw:Gesture.Strike,.30f,.14f,.055f,7,10,.06f,.80f); break;
                case "kazuru": p=new Profile(special?Gesture.Summon:Gesture.Strike,.25f,.10f,.06f,8,12,.06f); break;
                case "pokrak": p=new Profile(special?Gesture.Guard:Gesture.Strike,.26f,.14f,.08f,9,13,.09f); break;
                case "rosie": p=new Profile(special?Gesture.Plant:Gesture.Throw,.23f,.12f,.08f,9,12,.07f); break;
                case "popow": p=new Profile(Gesture.Strike,.38f,.18f,.025f,13,19,.13f,.84f); break;
                case "sepora": p=new Profile(Gesture.Spark,.25f,.14f,.09f,8,12,.10f,.85f); break;
                case "stein": p=new Profile(Gesture.Spark,.30f,.17f,.16f,10,14,.13f,.86f); break;
                case "tauris": p=new Profile(Gesture.Bite,.39f,.21f,.085f,15,19,.13f,.83f); break;
                case "tokoro": p=new Profile(Gesture.Slam,.35f,.22f,.10f,14,18,.14f,.83f); break;
                case "trimol": p=new Profile(Gesture.Throw,.37f,.23f,.14f,16,19,.13f,.82f); break;
                case "tsu": p=new Profile(Gesture.Throw,.25f,.14f,.055f,12,15,.09f,.80f); break;
                default: p=default; return false;
            }
            return true;
        }

        public static bool Supports(string id) => ProfileFor(id,false,out _);

        // Keep the contact drawing through the hold and early recovery, then settle into the idle drawing.
        // No visual hold adds time to an action or postpones the next attack.
        public static int AttackFrame(UnitDirectionalAnimation clip, float windup, float recovery, bool contact)
        {
            int first=Mathf.Clamp(clip.AttackContactFrame,0,clip.Attack.Length-1);
            if(!contact)return Mathf.Min(Mathf.Max(0,first-1),Mathf.FloorToInt(windup*first));
            if(recovery<.52f)return first;
            float settle=Mathf.Clamp01((recovery-.52f)/.48f);
            return Mathf.Min(clip.Attack.Length-1,first+1+Mathf.FloorToInt(settle*Mathf.Max(0,clip.Attack.Length-first-1)));
        }

        public static int SpecialFrame(UnitDirectionalAnimation clip, float recovery, bool released)
        {
            // Reference sheets contain preparation, release, release, idle. Never show release during cast.
            if(!released)return 0;
            int release=Mathf.Min(1,clip.Special.Length-1);
            if(recovery<.56f)return release;
            return Mathf.Min(clip.Special.Length-1,release+1+Mathf.FloorToInt(Mathf.Clamp01((recovery-.56f)/.44f)*Mathf.Max(0,clip.Special.Length-release-1)));
        }

        public static Pose Evaluate(string id, UnitDirectionalAnimation clip, Vector3 direction,
            float windup, float recovery, bool contact, bool special=false)
        {
            if(!ProfileFor(id,special,out var p))return new Pose(Vector3.zero,1,1,0);
            windup=Mathf.Clamp01(windup); recovery=Mathf.Clamp01(recovery);
            direction.z=0; direction=direction.sqrMagnitude>.00001f?direction.normalized:Vector3.down;
            float snap=Mathf.Pow(Mathf.Clamp01((windup-p.SnapStart)/(1-p.SnapStart)),3);
            float coil=contact?0:Mathf.SmoothStep(0,1,Mathf.Clamp01(windup/.62f))*(1-snap);
            float drive=contact?1-Mathf.SmoothStep(0,1,Mathf.Clamp01((recovery-.18f)/.82f)):snap;
            float after=contact?Mathf.Sin(recovery*Mathf.PI):0;
            float power=special?1.22f:1;
            float reach=Mathf.Max(p.Reach,special?clip.SpecialLungeDistance:clip.AttackLungeDistance)*power;
            float squash=Mathf.Max(p.Squash,Mathf.Clamp(special?clip.SpecialPunchScale:clip.AttackImpactSquash,0,.18f));
            float along=drive*reach-coil*p.Coil*power;
            float lift=coil*p.Lift;
            float angle=direction.x*(coil*p.WindAngle-drive*p.HitAngle)*power;
            float width=1+drive*squash-coil*.025f;
            float height=1-coil*.055f-drive*squash*.45f;

            switch(p.Gesture)
            {
                case Gesture.Slam:
                    // Raise the heavy mass, then bring it down onto a broad, planted contact pose.
                    lift=coil*p.Lift+drive*.018f;
                    width=1+drive*squash;
                    height=1+coil*.045f-drive*squash*.65f;
                    break;
                case Gesture.Bite:
                    // Pull the open jaw back before the fast bite; release the grip during recovery.
                    lift=coil*p.Lift+drive*.035f;
                    height=1+coil*.065f-drive*.07f;
                    along-=after*reach*.16f;
                    break;
                case Gesture.Drain:
                    // Reach into the target, then draw the stolen energy back toward the chest.
                    along-=after*reach*.38f;
                    angle+=direction.x*after*7;
                    break;
                case Gesture.Heart:
                    // Cup the heart, rise, then recoil softly when the projectile leaves the hands.
                    along=-coil*p.Coil*.35f-drive*reach*.68f;
                    lift=coil*p.Lift+drive*p.Lift*.4f;
                    angle=direction.x*(-coil*5+drive*9);
                    width=1-coil*.05f+drive*squash;
                    height=1+coil*.07f-drive*.04f;
                    break;
                case Gesture.Belly:
                    lift=coil*p.Lift+drive*.025f;
                    width=1-coil*.06f+drive*squash;
                    height=1+coil*.08f-drive*squash*.65f;
                    break;
                case Gesture.Throw:
                    // Weight on the rear leg, overhead lift, then a decisive forward arm release.
                    lift=coil*p.Lift+drive*.025f;
                    angle+=direction.x*after*4;
                    break;
                case Gesture.Seal:
                    // Bow the hat/forehead to project its seal; the seal effect remains independent.
                    height=1+coil*.07f-drive*.09f;
                    lift=coil*p.Lift;
                    break;
                case Gesture.Whip:
                    // Shoulder first on release, then a stronger pull back for the grapple Super.
                    along-=after*reach*(special?.62f:.22f);
                    angle+=direction.x*after*(special?13:7);
                    break;
                case Gesture.Spark:
                    // Compress the charge and recoil on discharge. A small, bounded body tremor
                    // builds only during charge, without creating any new lightning/projectile pixels.
                    float buzz=contact?0:Mathf.Sin(windup*Mathf.PI*12)*coil*.014f;
                    along=-coil*p.Coil*.55f-drive*reach*.8f;
                    lift=coil*p.Lift+drive*.045f;
                    angle=direction.x*(-coil*5+drive*p.HitAngle)+buzz*80;
                    width=1-coil*.07f+drive*squash;
                    height=1-coil*.08f+drive*.075f;
                    along+=buzz;
                    break;
                case Gesture.Guard:
                    along=-coil*.08f;
                    lift=coil*.06f;
                    width=1+drive*.13f;
                    height=1-coil*.07f-drive*.06f;
                    angle=direction.x*coil*5;
                    break;
                case Gesture.Summon:
                case Gesture.Plant:
                    along=drive*.16f-coil*.10f;
                    lift=coil*.14f;
                    width=1+drive*.06f;
                    height=1+coil*.05f-drive*.10f;
                    angle=direction.x*(coil*10-drive*17);
                    break;
                case Gesture.Treasure:
                    along=0; lift=coil*.08f+drive*.16f;
                    width=1-coil*.06f+drive*.05f;
                    height=1+coil*.05f+drive*.06f;
                    angle=direction.x*(coil*7-drive*8);
                    break;
            }
            return new Pose(direction*along+Vector3.up*lift,width,height,angle);
        }
    }
}
