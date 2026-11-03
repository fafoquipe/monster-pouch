using System.Collections;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public sealed partial class LocalGameUI
    {
        void MoonkensProduced(MoonkenDelivery delivery)
        {
            BattleUnit actor=delivery.Source;
            if (actor == null || MoonIcon == null) return;
            var renderer = actor.GetComponent<UnitPresentation>()?.Renderer;
            Vector3 head = renderer != null ? new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, actor.transform.position.z) :
                actor.transform.position + Vector3.up * 1.2f;
            Vector2 origin = ToCanvas(GameCamera.WorldToScreenPoint(head)) + new Vector2(0,-14);
            StartCoroutine(BlotanMoonkenIncome(origin, delivery));
        }

        IEnumerator BlotanMoonkenIncome(Vector2 origin, MoonkenDelivery delivery)
        {
            bool local=delivery.Recipient==Match.Player;
            float duration=delivery.ShineDuration;
            // A full-size readable Moonken shines above the hat before following the sell flight.
            var root = Panel(effects, "Blotan shining Moonken", new Rect(origin - Vector2.one*34, Vector2.one*68), Color.clear, false);
            var halo = Panel(root, "Moonken lilac glow", new Rect(0,0,68,68), Color.clear, false).gameObject.AddComponent<Image>();
            halo.sprite = MoonIcon; halo.color = new Color(.7f,.4f,1,.45f); halo.raycastTarget = false;
            var disk = Panel(root, "Moonken", new Rect(7,7,54,54), Color.clear, false).gameObject.AddComponent<Image>();
            disk.sprite = MoonIcon; disk.preserveAspect = true; disk.raycastTarget = false;
            var rim = disk.gameObject.AddComponent<Outline>(); rim.effectColor = new Color(.92f,.82f,1,.95f); rim.effectDistance = new Vector2(2,2);
            var sparks = new RectTransform[4];
            for (int i=0;i<sparks.Length;i++)
            {
                sparks[i]=Panel(root,"Moonken pixel glint",new Rect(0,0,14,14),Color.clear,false);
                Panel(sparks[i],"Vertical",new Rect(5,0,4,14),Color.white,false);
                Panel(sparks[i],"Horizontal",new Rect(0,5,14,4),Color.white,false);
            }
            float elapsed=0;
            Vector2 from=origin;
            while(elapsed<duration && !delivery.Canceled && Match.Phase!=MatchPhase.Menu)
            {
                elapsed=delivery.Elapsed;
                float t=Mathf.Clamp01(elapsed/Mathf.Max(.1f,duration)), pulse=Mathf.Sin(t*Mathf.PI);
                float rise=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.65f));
                from=origin+new Vector2(0,-68*rise);
                SetRect(root,new Rect(from-Vector2.one*34,Vector2.one*68));
                halo.color=new Color(.76f,.48f,1,.4f+.35f*pulse);
                float haloSize=64+14*pulse;
                SetRect(halo.rectTransform,new Rect((68-haloSize)*.5f,(68-haloSize)*.5f,haloSize,haloSize));
                for(int i=0;i<sparks.Length;i++)
                {
                    float angle=i*Mathf.PI*.5f+.35f;
                    Vector2 offset=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*(27+8*pulse);
                    SetRect(sparks[i],new Rect(Vector2.one*27+offset,Vector2.one*14));
                    sparks[i].localScale=Vector3.one*(.65f+.5f*pulse);
                }
                yield return null;
            }
            if(root!=null)Destroy(root.gameObject);
            if(local && !delivery.Canceled && Match.Phase!=MatchPhase.Menu)
                yield return SellMoonkens(from,delivery.Amount,delivery);
        }
    }
}
