using System;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    /// <summary>Desaturates presentation only; inspection buttons and descriptions stay interactive.</summary>
    public sealed class AbilityAvailabilityVisual : MonoBehaviour
    {
        Func<bool> canPurchase;
        Func<bool> showPurchaseState;
        Action<bool> changed;
        Graphic[] graphics = Array.Empty<Graphic>();
        Material[] originalMaterials = Array.Empty<Material>();
        Material grayscale;
        bool? available;
        bool? desaturated;
        public bool IsAvailable => available == true;
        public bool IsGrayscale => desaturated == true;

        public void Configure(Func<bool> eligibility, Action<bool> onChanged = null,
            Func<bool> purchaseStateVisible = null, Graphic[] artwork = null)
        {
            Restore();
            canPurchase = eligibility ?? throw new ArgumentNullException(nameof(eligibility));
            showPurchaseState = purchaseStateVisible ?? (() => true);
            changed = onChanged;
            graphics = artwork ?? GetComponentsInChildren<Graphic>();
            originalMaterials = new Material[graphics.Length];
            for (int i = 0; i < graphics.Length; i++) originalMaterials[i] = graphics[i].material;
            available = null;
            desaturated = null;
            Refresh();
        }

        public void Refresh()
        {
            if (canPurchase == null) return;
            bool next = canPurchase();
            bool gray = showPurchaseState() && !next;
            if (available == next && desaturated == gray) return;
            if (gray && grayscale == null)
            {
                var shader = Resources.Load<Shader>("MonsterPouch/UI/AbilityGrayscale");
                if (shader == null) throw new InvalidOperationException("The ability grayscale UI shader is missing.");
                grayscale = new Material(shader) { name = "Ability availability grayscale", hideFlags = HideFlags.HideAndDontSave };
            }
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i] != null) graphics[i].material = gray ? grayscale : originalMaterials[i];
            available = next;
            desaturated = gray;
            changed?.Invoke(next);
        }

        void LateUpdate() => Refresh();
        void Restore()
        {
            for (int i = 0; i < graphics.Length; i++)
                if (graphics[i] != null) graphics[i].material = originalMaterials[i];
        }
        void OnDestroy()
        {
            Restore();canPurchase = null;changed = null;
            if (grayscale != null)
            {
                if (Application.isPlaying) Destroy(grayscale);
                else DestroyImmediate(grayscale);
            }
        }
    }
}
