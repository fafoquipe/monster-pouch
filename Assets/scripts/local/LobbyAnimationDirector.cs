using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    // One director per UI owns every visible portrait, including the featured hero.
    // Its private random source never consumes the match simulation's random sequence.
    public sealed class LobbyAnimationDirector : MonoBehaviour
    {
        readonly List<LobbyPersonality> portraits=new List<LobbyPersonality>();
        readonly List<LobbyPersonality> ready=new List<LobbyPersonality>();
        readonly System.Random random=new System.Random();
        LocalGameUI owner;float nextStart;
        // Temporarily paused at the player's request. Keep the drawings and
        // scheduler available for a future animation pass, but ship static idles.
        public static bool PlaybackEnabled { get; set; } = false;
        public const int MaximumSimultaneous=2;
        public int ActiveCount { get { int count=0;foreach(var p in portraits)if(p!=null&&p.IsGesturing)count++;return count; } }
        public void Configure(LocalGameUI ui){owner=ui;}
        float Range(float min,float max)=>min+(float)random.NextDouble()*(max-min);
        public void Register(LobbyPersonality portrait)
        {
            if(portraits.Contains(portrait))return;
            portraits.Add(portrait);
            portrait.Rest(portrait.PreviewOnOpen?Range(.45f,1f):Range(portrait.Featured?6:3,portrait.Featured?12:10));
            // An explicitly opened detail card should show its personality promptly.
            // Covered portraits are suspended before it claims an animation slot.
            if(portrait.PreviewOnOpen)nextStart=0;
        }
        public void Unregister(LobbyPersonality portrait){portraits.Remove(portrait);portrait.Rest(0);}
        bool Visible(LobbyPersonality p)=>p!=null&&p.isActiveAndEnabled&&(owner==null||owner.LobbyPortraitVisible(p));
        void SuspendCovered()
        {
            foreach(var p in portraits)if(p!=null&&!Visible(p)&&p.IsGesturing)
                p.Rest(Range(5,11));
        }
        public bool TryPlay(LobbyPersonality portrait)
        {
            if(!PlaybackEnabled){RestAll();return false;}
            SuspendCovered();
            if(!Visible(portrait)||!portrait.HasAuthoredFrames||portrait.IsGesturing||ActiveCount>=MaximumSimultaneous)return false;
            portrait.BeginGesture(Range(.94f,1.12f));nextStart=Range(1.8f,3.8f);return true;
        }
        void Update(){Advance(Time.unscaledDeltaTime);}
        void RestAll()
        {
            ready.Clear();nextStart=0;
            foreach(var p in portraits)if(p!=null&&p.IsGesturing)p.Rest(0);
        }
        public void Advance(float delta)
        {
            if(!PlaybackEnabled){RestAll();return;}
            SuspendCovered();if(delta<=0||float.IsNaN(delta)||float.IsInfinity(delta))return;
            nextStart-=delta;ready.Clear();
            foreach(var p in portraits)
            {
                if(!Visible(p))continue;
                if(p.IsGesturing)
                {
                    p.Advance(delta);
                    if(!p.IsGesturing)p.Rest(Range(p.Featured?12:14,p.Featured?24:29));
                }
                else
                {
                    p.Wait(delta);
                    if(p.HasAuthoredFrames&&p.SecondsUntilGesture<=0)ready.Add(p);
                }
            }
            if(nextStart<=0&&ready.Count>0&&ActiveCount<MaximumSimultaneous)
            {
                // Random rest times keep starts varied; the longest overdue portrait
                // takes the next slot so repeated lucky draws cannot leave one out.
                var next=ready[0];
                for(int i=1;i<ready.Count;i++)if(ready[i].ReadyWait>next.ReadyWait)next=ready[i];
                TryPlay(next);
            }
        }
    }

    public sealed partial class LocalGameUI
    {
        LobbyAnimationDirector lobbyDirector;
        public LobbyAnimationDirector LobbyDirector
        {
            get
            {
                if(lobbyDirector==null){lobbyDirector=CanvasRoot.gameObject.AddComponent<LobbyAnimationDirector>();lobbyDirector.Configure(this);}
                return lobbyDirector;
            }
        }
        internal bool LobbyPortraitVisible(LobbyPersonality portrait)=>
            Match.Phase==MonsterPouch.Gameplay.Match.MatchPhase.Menu&&
            (modal==null||portrait.transform.IsChildOf(modal));
    }
}
