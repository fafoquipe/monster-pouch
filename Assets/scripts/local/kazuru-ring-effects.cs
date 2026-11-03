using System.Collections;
using System.Linq;
using MonsterPouch.Gameplay.Board;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        IEnumerator RisingKazuruRings(BoardCell[] cells)
        {
            var simulation=match.Simulation;
            var sprite=Resources.LoadAll<Sprite>("MonsterPouch/Kazuru/kazuru-rings").FirstOrDefault(s=>s.name=="ground");
            if(sprite==null)yield break;
            var root=Rect("kazuru-rising-rings",layer,Vector2.zero);
            var rings=new Image[cells.Length];var centers=new Vector2[cells.Length];
            for(int i=0;i<cells.Length;i++)
            {
                centers[i]=Point(match.Mapper.GetWorldPosition(cells[i]));
                rings[i]=Box("ground-ring",root,Vector2.one*CellPixels(cells[i])*.88f,Color.white);
                rings[i].sprite=sprite;rings[i].preserveAspect=true;
            }
            float elapsed=0;
            while(elapsed<.85f&&Current(simulation))
            {
                elapsed+=EffectDelta;
                float t=Mathf.Clamp01(elapsed/.85f);
                for(int i=0;i<rings.Length;i++)
                {
                    rings[i].rectTransform.anchoredPosition=Round(centers[i]+Vector2.up*(t*22));
                    rings[i].color=new Color(1,1,1,Mathf.Min(1,t*7)*(1-Mathf.SmoothStep(.55f,1,t)));
                    rings[i].rectTransform.localScale=Vector3.one*(.6f+.4f*Mathf.Sin(t*Mathf.PI*.5f));
                }
                yield return null;
            }
            if(root!=null)Destroy(root.gameObject);
        }
    }
}
