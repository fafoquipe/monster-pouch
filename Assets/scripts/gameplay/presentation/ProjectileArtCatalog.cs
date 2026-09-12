using System;
using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    [Serializable]
    public sealed class ProjectileArt
    {
        public string UnitId;
        public Sprite[] Flight = Array.Empty<Sprite>();
        public Sprite[] Impact = Array.Empty<Sprite>();
        public float ReferencePixels = 128;
        public float DisplaySize = 46;
        public bool OrientToTarget = true;
    }

    [CreateAssetMenu(menuName = "Monster Pouch/Projectile art catalog")]
    public sealed class ProjectileArtCatalog : ScriptableObject
    {
        public ProjectileArt[] Projectiles = Array.Empty<ProjectileArt>();
        public ProjectileArt Get(string id)
        {
            foreach (var effect in Projectiles)
                if (effect != null && effect.UnitId == id) return effect;
            return null;
        }
    }
}
