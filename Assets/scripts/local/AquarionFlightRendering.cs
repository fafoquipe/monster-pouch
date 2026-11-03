using MonsterPouch.Gameplay.Presentation;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Shared by the actual Aquarion flight and the editor playback; no combat timing here.
    public static class AquarionFlightRendering
    {
        public static bool Owns(string renderer) => renderer=="aquarion-javelin" || renderer=="aquarion-water-bolt" || renderer=="aquarion-binding-javelin";
        public static float Width(string renderer) => renderer=="aquarion-water-bolt"?.42f:.5f;
        public static ProjectileVisualPose Sample(Sprite sprite,ProjectileVisualSlot slot,float cellPixels,float cellWidth,float angle=0)
        {
            var frame=slot?.Frame(sprite);
            float visibleMajor=frame!=null?Mathf.Max(frame.VisibleRect.width,frame.VisibleRect.height):1f;
            return new ProjectileVisualPose(sprite,Vector2.one*(cellPixels*cellWidth/Mathf.Max(.001f,visibleMajor)),true){Rotation=angle};
        }
    }
}
