using System;
using UnityEngine;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class LobbyFramePose
    {
        // Pixel rectangles use the texture's bottom-left origin. Anchor is the
        // local foot position inside that rectangle, measured in source pixels.
        public float[] rect,anchor;
        public float height;
        // Optional measured size registration for this complete drawing. Zero
        // (old manifests) means one. The same factor applies to both axes.
        public float registrationScale;
        public float ResolvedRegistrationScale=>registrationScale>0&&!float.IsNaN(registrationScale)&&!float.IsInfinity(registrationScale)?registrationScale:1;
    }
    [Serializable] public sealed class LobbyFrameClip
    {
        public string id,texture,idleTexture;
        public float[] idleRect,idleBounds,idleAnchor;
        // Only opted-in LobbyViews use this presentation registration.
        public bool idleFloating;
        public float referenceHeight;
        public LobbyFramePose[] frames;
        [NonSerialized] internal Texture2D atlas;
        [NonSerialized] internal Sprite idleSprite;
        [NonSerialized] internal Sprite[] sprites;
        [NonSerialized] internal float resolvedReferenceHeight;
    }
    [Serializable] internal sealed class LobbyFrameManifest { public LobbyFrameClip[] clips; }

    // A readable middle action gets the longest time, while its neighboring
    // drawings retain chronological order. Nothing interpolates their artwork.
    public static class LobbyFramePlayback
    {
        public static int FrameCountForPhase(int poseCount,int phase)
        {
            if(poseCount<=0)return 0;
            int reveal=Mathf.Clamp(Mathf.RoundToInt(poseCount*14f/48f),1,Mathf.Max(1,poseCount-2));
            int finish=Mathf.Clamp(Mathf.RoundToInt(poseCount*14f/48f),1,Mathf.Max(1,poseCount-reveal-1));
            if(poseCount<3)return phase==0?poseCount:0;
            return phase==0?reveal:phase==1?poseCount-reveal-finish:phase==2?finish:0;
        }
        public static int FrameAtTime(string id,int poseCount,float normalized)
        {
            if(poseCount<=0||float.IsNaN(normalized)||float.IsInfinity(normalized))return -1;
            normalized=Mathf.Clamp01(normalized);if(normalized>=1)return -1;
            if(poseCount<3)return Mathf.Min(poseCount-1,Mathf.FloorToInt(normalized*poseCount));
            var timing=LobbyGestureTiming.For(id);float time=normalized*timing.Duration;
            int reveal=FrameCountForPhase(poseCount,0),main=FrameCountForPhase(poseCount,1),finish=FrameCountForPhase(poseCount,2);
            if(time<timing.Reveal)return Mathf.Min(reveal-1,Mathf.FloorToInt(time/timing.Reveal*reveal));
            if(time<timing.Reveal+timing.Main)return reveal+Mathf.Min(main-1,Mathf.FloorToInt((time-timing.Reveal)/timing.Main*main));
            return reveal+main+Mathf.Min(finish-1,Mathf.FloorToInt((time-timing.Reveal-timing.Main)/timing.Return*finish));
        }
    }
}

