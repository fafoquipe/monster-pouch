using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    public static class VolkanarFlightRendering
    {
        public static bool Owns(string renderer)=>renderer=="volkanar-slag"||renderer=="volkanar-crater-bomb";
        public static ProjectileVisualPose Sample(Sprite[] frames,ProjectileVisualSlot slot,float progress,float tile,bool bomb,float angle=0)
        {
            float major=1;
            if(slot?.Frames!=null)foreach(var frame in slot.Frames)
                if(frame?.Sprite!=null)major=Mathf.Max(major,frame.Sprite.rect.width*frame.VisibleRect.width,frame.Sprite.rect.height*frame.VisibleRect.height);
            if(major<=1)foreach(var frame in frames)if(frame!=null)major=Mathf.Max(major,frame.rect.width,frame.rect.height);
            var sprite=frames[Mathf.Min(frames.Length-1,Mathf.FloorToInt(Mathf.Clamp01(progress)*frames.Length))];
            return new ProjectileVisualPose(sprite,sprite!=null?sprite.rect.size*(tile*(bomb?.65f:.5f)/major):Vector2.one*tile*.5f,true){Rotation=angle};
        }
    }
}
