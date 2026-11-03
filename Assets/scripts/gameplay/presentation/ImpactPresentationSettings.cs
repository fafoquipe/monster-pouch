using UnityEngine;
namespace MonsterPouch.Gameplay.Presentation
{
    [CreateAssetMenu(menuName="Monster Pouch/Presentation/Impact settings")]
    public sealed class ImpactPresentationSettings:ScriptableObject
    {
        [Min(.01f)] public float PopowRise=.10f;
        [Min(.01f)] public float PopowFall=.14f;
        [Min(0)] public float PopowHeightInCells=.32f;
        // Contact footprint only; never used by combat range, targets, energy or motion.
        [Min(0)] public float EnergyBasicContactCells=.35f;
        [Min(0)] public float PassiveBasicContactCells=.50f;
        [Min(0)] public float EnergySuperContactCells=.70f;
        public float ContactDiameter(bool hasEnergySuper,bool ability)
            => Mathf.Max(0,hasEnergySuper?(ability?EnergySuperContactCells:EnergyBasicContactCells):PassiveBasicContactCells);
        public static float ContactCells(bool hasEnergySuper,bool ability)
            => Current!=null?Current.ContactDiameter(hasEnergySuper,ability):(hasEnergySuper?(ability?.70f:.35f):.50f);
        static ImpactPresentationSettings cached;
        public static ImpactPresentationSettings Current=>cached!=null?cached:cached=Resources.Load<ImpactPresentationSettings>("MonsterPouch/ImpactPresentation");
        public static float Lift(float age,float rise,float fall,float height)
        {
            if(age<0||age>=rise+fall)return 0;
            return height*(age<rise?Mathf.Sin(Mathf.Clamp01(age/Mathf.Max(.01f,rise))*Mathf.PI*.5f):1-Mathf.Pow(Mathf.Clamp01((age-rise)/Mathf.Max(.01f,fall)),2));
        }
    }
}
