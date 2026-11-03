using UnityEngine;

namespace MonsterPouch.Local
{
    public readonly struct LobbyRigSample
    {
        public readonly float Time,Weight,Main,Reveal,Finish;
        public LobbyRigSample(string id,float normalized)
        {
            Time=Mathf.Clamp01(normalized);
            var timing=LobbyGestureTiming.For(id);float seconds=Time*timing.Duration;
            Reveal=Mathf.Clamp01(seconds/timing.Reveal);
            Main=Mathf.Clamp01((seconds-timing.Reveal)/timing.Main);
            Finish=Mathf.Clamp01((seconds-timing.Reveal-timing.Main)/timing.Return);
            Weight=LobbyRigMotion.Ease(Reveal)*(1-LobbyRigMotion.Ease(Finish));
        }
    }

    // All motions sample time directly: no frame index, pose image, accumulated
    // integrator or random value is involved in a gesture.
    public static class LobbyRigMotion
    {
        public static float Ease(float x){x=Mathf.Clamp01(x);return x*x*x*(10+x*(-15+6*x));}
        public static float Pulse(float x,float from,float to)
        {return Ease(Mathf.InverseLerp(from,from+(to-from)*.25f,x))*(1-Ease(Mathf.InverseLerp(to-(to-from)*.25f,to,x)));}
        public static float Cycle(float main,float count)=>Mathf.Sin(main*Mathf.PI*2*count)*Mathf.Sin(main*Mathf.PI)*Mathf.Sin(main*Mathf.PI);
        public static Vector2 Target(string id,int owner,LobbyRigPart part,LobbyRigSample pose)
        {
            Vector2 rest=part.Hand,target=rest;bool left=owner==1;
            float m=pose.Main,w=pose.Weight,c=Cycle(m,2);
            switch(id)
            {
                case "anuik":target=new Vector2(left?.445f:.555f,.28f+.009f*Cycle(m,1));break;
                case "tokoro":
                    if(!left)target=new Vector2(.82f,.645f+.014f*Cycle(m,2));break;
                case "atong":
                    if(!left)
                    {
                        var back=new Vector2(.70f,.205f);var chin=new Vector2(.635f+.034f*c,.305f+.009f*Cycle(m,4));
                        if(pose.Reveal<.48f)return Vector2.Lerp(rest,back,Ease(pose.Reveal/.48f));
                        if(pose.Finish>.52f)return Vector2.Lerp(back,rest,Ease((pose.Finish-.52f)/.48f));
                        target=Vector2.Lerp(back,chin,Ease((pose.Reveal-.48f)/.52f)*(1-Ease(pose.Finish/.52f)));
                        return target;
                    }break;
                case "flo":target=new Vector2(left?.405f:.595f,.39f+.014f*Cycle(m,1));break;
                case "gochan":if(left)target=new Vector2(.28f,.445f+.008f*c);break;
                case "jazar":target=left?new Vector2(.40f,.40f):new Vector2(.67f-.17f*Pulse(m,.61f,.85f),.42f);break;
                case "kayon":
                    if(!left)target=new Vector2(.70f,.32f+.01f*Cycle(m,1));break;
                case "bugaloo":if(left)target=new Vector2(.355f+.02f*Cycle(m,3),.36f+.022f*Cycle(m,2));break;
                case "popow":target=new Vector2(left?.245f:.755f,.46f);
                    target+=new Vector2((left?-.07f:.07f),.045f)*Pulse(m,left?.08f:.4f,left?.36f:.68f);break;
                case "sepora":target=new Vector2(left?.455f:.555f,.305f+.012f*Cycle(m,1));break;
                case "tauris":if(left)target=new Vector2(.22f+.025f*c,.43f+.006f*Cycle(m,3));break;
                case "atori":if(left)target=new Vector2(.185f+.033f*c,.305f+.007f*Cycle(m,2));break;
                case "blotan":target=new Vector2(left?.26f:.72f,.335f+.009f*Cycle(m,1));break;
                case "bugui":target=new Vector2(left?.245f:.755f,.345f+(left?.025f:-.025f)*c);break;
                case "stein":target=new Vector2(left?.495f:.555f,.17f+(left?.012f:-.012f)*Cycle(m,3));break;
                case "trimol":target=new Vector2(left?.145f:.855f,.30f+.022f*Cycle(m,left?2:2.5f));break;
                case "aky":target=new Vector2(left?-.05f:1.05f,.40f+.015f*Cycle(m,1));break;
                case "hymay":if(!left)target=new Vector2(.855f+.015f*Cycle(m,2),.295f+.02f*Cycle(m,2));break;
            }
            return Vector2.Lerp(rest,target,w);
        }
        public static Vector2 Rotate(Vector2 v,float degrees)
        {float a=degrees*Mathf.Deg2Rad,s=Mathf.Sin(a),c=Mathf.Cos(a);return new Vector2(v.x*c-v.y*s,v.x*s+v.y*c);}

        public static float RigidAngle(string id,int owner,LobbyRigSample p)
        {
            bool left=owner==1;float angle=0,m=p.Main,c=Cycle(m,2);
            switch(id)
            {
                case "anuik":angle=left?100:-100;break;
                case "tokoro":angle=left?0:175+1.5f*c;break;
                case "atong":
                    if(!left)
                    {
                        float reach=Ease(p.Reveal)*(1-Ease(p.Finish));
                        return (135+45*Ease(Mathf.InverseLerp(.48f,1,p.Reveal))*(1-Ease(p.Finish/.52f))+5*c)*reach;
                    }break;
                case "flo":angle=(left?40:-40)+(left?2:-2)*Cycle(m,1);break;
                case "gochan":angle=left?26+3*c:0;break;
                case "jazar":angle=left?35:-25-18*Pulse(m,.61f,.85f);break;
                case "kayon":angle=left?0:15+3*Cycle(m,1);break;
                case "bugaloo":angle=left?60+6*Cycle(m,3):0;break;
                case "popow":angle=(left?-32:32)+(left?-10:10)*Pulse(m,left?.08f:.40f,left?.36f:.68f);break;
                case "sepora":angle=left?105:-16;break;
                case "tauris":angle=left?-50-5*c:0;break;
                case "atori":angle=left?-65+8*c:0;break;
                case "blotan":angle=left?25+4*Cycle(m,1):-12;break;
                case "bugui":angle=(left?-20:20)+(left?-5:5)*c;break;
                case "stein":angle=(left?4:-4)+(left?3:-3)*Cycle(m,3);break;
                case "trimol":angle=(left?-65:65)+(left?-3:3)*Cycle(m,2);break;
                case "aky":angle=left?-1.5f:1.5f;break;
                case "hymay":angle=left?0:8+3*c;break;
            }
            return angle*p.Weight;
        }
        // Every source cell is a rigid cutout. A single stable joint per piece
        // avoids the folded/skewed texture wedges caused by blending elbow bones.
        public readonly struct Arm
        {
            readonly Vector2 root,hand;
            readonly float angle;
            public Vector2 Hand=>hand;
            public Arm(Vector2 root,Vector2 restHand,float degrees)
            {
                this.root=root;angle=degrees;hand=root+Rotate(restHand-root,angle);
            }
            public Vector2 Map(Vector2 point)=>root+Rotate(point-root,angle);
        }
    }
}

