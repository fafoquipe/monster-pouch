using UnityEngine;
using MonsterPouch.Gameplay.Presentation;

namespace MonsterPouch.Core
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int targetFrameRate = MobileFrameRatePolicy.DefaultTarget;

        private void Awake()
        {
            MobileFrameRatePolicy.SetTarget(targetFrameRate);
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Input.multiTouchEnabled = true;

            DontDestroyOnLoad(gameObject);
        }
    }
}
