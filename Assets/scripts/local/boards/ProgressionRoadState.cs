using System;
using UnityEngine;

namespace MonsterPouch.Local.Boards
{
    // Presentation snapshot only. Ownership, thresholds, rewards and claims come from the store.
    public enum RoadRewardStatus { Locked, Claimable, Claimed }
    public sealed class RoadCharacter
    {
        public string Id, Name;
        public bool Owned, HideLockedArtwork;
    }
    public sealed class RoadWorld
    {
        public string BoardId, Name;
        public long StartsAt;
        public RoadCharacter[] Characters=Array.Empty<RoadCharacter>();
    }
    public sealed class RoadReward
    {
        public string Id, Title, IconResource;
        public long Trophies;
        public RoadRewardStatus Status;
    }
    public sealed class ProgressionRoadState
    {
        public bool Spanish;
        public long Trophies;
        public RoadWorld[] Worlds=Array.Empty<RoadWorld>();
        public RoadReward[] Rewards=Array.Empty<RoadReward>();
        public string Error;
    }

    // Pure geometry shared by renderer and focused safe-area/state checks.
    public static class ProgressionRoadLayout
    {
        public const float StepHeight=112;
        public static int Columns(float width)=>Mathf.Clamp(Mathf.FloorToInt((width-32)/64),4,7);
        public static float WorldHeight(float width,int characters)=>236+Mathf.Ceil(characters/(float)Columns(width))*82;
        public static float StepCenter(float width,int step)=>width*.5f+Mathf.Sin(step*Mathf.PI/3)*Mathf.Min(96,(width-148)*.45f);
        public static long CurrentStep(long trophies)=>Math.Min(1500,Math.Max(0,trophies)/50*50);
        public static float FocusOffset(float center,float viewHeight,float contentHeight)=>Mathf.Clamp(center-viewHeight*.55f,0,Mathf.Max(0,contentHeight-viewHeight));
    }
}
