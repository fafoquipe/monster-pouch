using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    public sealed partial class UnitPresentation
    {
        private MartioVisualAssets martioHurtAssets;

        // Movement owns the single rest-state guard and the existing hit clock.
        // This implementation only selects Martio's authored damage sprite.
        partial void ApplyMartioIdleHurtPose()
        {
            if (art == null || art.Id != "martio" || body == null) return;

            if (martioHurtAssets == null)
                martioHurtAssets = Resources.Load<MartioVisualAssets>("MonsterPouch/Martio/MartioVisualAssets");
            var frames = martioHurtAssets != null ? martioHurtAssets.HurtDirections : null;
            int direction = (int)facing;
            var clip = art.Animation(facing);
            if (clip == null || frames == null || direction < 0 || direction >= frames.Length ||
                frames[direction] == null) return;

            body.sprite = frames[direction];
            body.flipX = clip.FlipX;
        }
    }
}
