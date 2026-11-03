using UnityEngine;

namespace MonsterPouch.Gameplay.Presentation
{
    // One runtime writer. The default preserves the existing 60 FPS gameplay target.
    // Device/thermal policy is deliberately separate from simulation timing.
    public static class MobileFrameRatePolicy
    {
        public const int DefaultTarget=60;
        static bool initialized;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession(){initialized=false;}
        public static void Initialize(){if(!initialized)SetTarget(DefaultTarget);}
        public static void SetTarget(int target)
        {initialized=true;Application.targetFrameRate=target;}
    }
}
