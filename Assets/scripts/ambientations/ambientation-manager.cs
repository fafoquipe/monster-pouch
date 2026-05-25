using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Ambientations
{
    public sealed class AmbientationManager : MonoBehaviour
    {
        [SerializeField] private string startingAmbientationKey = "grand-cas-hotel";
        [SerializeField] private List<AmbientationId> ambientations = new();

        public AmbientationId CurrentAmbientation { get; private set; }

        private void Awake()
        {
            if (ambientations.Count > 0 && !string.IsNullOrEmpty(startingAmbientationKey))
                ActivateAmbientation(startingAmbientationKey);
        }

        public bool ActivateAmbientation(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            foreach (var ambientation in ambientations)
            {
                if (ambientation == null)
                    continue;

                bool shouldBeActive = ambientation.Matches(key);
                SetActiveState(ambientation, shouldBeActive);

                if (shouldBeActive)
                    CurrentAmbientation = ambientation;
            }

            return CurrentAmbientation != null;
        }

        public void Register(AmbientationId ambientation)
        {
            if (ambientation == null)
                return;

            if (!ambientations.Contains(ambientation))
                ambientations.Add(ambientation);
        }

        private void SetActiveState(AmbientationId ambientation, bool active)
        {
            if (ambientation == null)
                return;

            ambientation.gameObject.SetActive(active);
        }
    }
}
