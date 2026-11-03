using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void ApplySharedBriefLayout(RectTransform tray)
        {
            // One immutable control composition for every map. Scenery may
            // change around it, but never scale or reposition the case hit targets.
            var group=Panel(tray,"Shared brief",new Rect(0,0,540,216),Color.clear,false);
            foreach(string name in new[]{"Brief body","Brief drop","Brief contents","Brief lid","Brief closed"})
            {
                var child=tray.Find(name);
                if(child!=null)child.SetParent(group,false);
            }
            group.SetAsFirstSibling();
        }
    }
}
