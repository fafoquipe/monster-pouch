using UnityEngine;

namespace MonsterPouch.Ambientations
{
    public sealed class AmbientationId : MonoBehaviour
    {
        [SerializeField] private string ambientationKey;

        public string Key => ambientationKey;

        public bool Matches(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;

            return string.Equals(ambientationKey, key, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
