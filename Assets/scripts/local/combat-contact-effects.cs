using System.Collections.Generic;
using MonsterPouch.Gameplay.Match;
using MonsterPouch.Gameplay.Presentation;
using MonsterPouch.Gameplay.Units;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class CombatStatusEffects
    {
        sealed class ContactVisual
        {
            public Color Color;
            public Sprite[] Frames;
            public Vector3 Direction;
            public float Diameter;
            public bool Fire, Dissolve;
        }
        sealed class ContactFlight
        {
            public BattleUnit Source,Target;
            public int SourceReset,TargetReset;
            public long EmissionId;
            public bool Ability,Auxiliary;
            public float Release, Due;
            public int AttackReset;
            public bool Released;
            public Vector3 Endpoint;
            public string MaterialId;
        }
        readonly List<ContactFlight> contactFlights=new List<ContactFlight>(32);
        readonly Stack<ContactFlight> contactFlightPool=new Stack<ContactFlight>(32);
        long lastContactSequence;
        public int PendingContactFlightCount=>contactFlights.Count;
        public int ActiveContactCount
        {
            get { int count=0;foreach(var fx in active)if(fx.Contact!=null)count++;return count; }
        }

        // Match events are ordered and scoped by the simulation, not by a visual frame number.
        // Several simultaneous packets remain distinct; replaying the same callback is ignored.
        public bool ObserveContact(BattleUnit source,BattleUnit target,int amount,CombatImpactPresentationSnapshot hit)
        {
            if(match==null||source==null||target==null)return false;
            EnsureSimulation();
            if(hit!=null)
            {
                if(hit.IsPeriodic||!hit.Applied||hit.Source!=source||hit.Target!=target||
                    source.CombatResetVersion!=hit.SourceResetVersion||target.CombatResetVersion!=hit.TargetResetVersion)return false;
                if(hit.Sequence<=lastContactSequence)return false;
                lastContactSequence=hit.Sequence;
            }
            else if(amount<=0)return false;
            // A real contact consumes only the matching due emission, never an unrelated later shot.
            float now=simulation==null?0:simulation.Elapsed;
            for(int i=0;i<contactFlights.Count;i++)
            {
                var flight=contactFlights[i];
                if(flight.Source==source&&flight.Target==target&&flight.Due<=now+CombatSimulation.TickDuration+.001f&&
                    (flight.Auxiliary||hit==null||flight.Ability==hit.IsAbility))
                { ReleaseContactFlight(i);break; }
            }
            return true;
        }

        public void PresentContact(BattleUnit source,BattleUnit target,CombatImpactPresentationSnapshot hit,ProjectileArt art)
        {
            if(source==null||target==null)return;
            string id=source.BaseStats.DefinitionId;
            Vector3 position=hit?.Position??target.transform.position;
            Vector3 from=hit?.SourcePosition??source.transform.position;
            bool ability=hit!=null&&hit.IsAbility;
            var visual=ContactStyle(id,ability,false);
            visual.Diameter=ImpactPresentationSettings.ContactCells(source.BaseStats.EnergyMax>0,ability);
            if(art?.Impact!=null&&art.Impact.Length>0)visual.Frames=art.Impact;
            // Reuse the authored slag contact for Orvek's basic; its bomb keeps its area owner.
            if(id=="canonero-del-orvek")
                visual.Frames=Resources.Load<VolkanarEffectsCatalog>("MonsterPouch/Eronya/Volkanar/EffectsCatalog")?.OrvekImpact;
            visual.Direction=position-from;
            SpawnContact(position+Vector3.up*Mathf.Abs(match.Mapper.CellSize.y)*.28f,visual);
        }

        static ContactVisual ContactStyle(string id,bool ability,bool dissolve)
        {
            Color color=new Color(1,.87f,.5f);bool fire=false;
            switch(id)
            {
                case "gochan":case "serya":case "legionario":case "dagata":case "canonero-del-orvek":
                    color=new Color(1,.43f,.12f);fire=true;break;
                case "drayven":case "titanica":case "jabalinero":case "maestro-de-las-mareas":
                case "stein":case "zephalontropo":case "alveyr":case "palista":
                    color=new Color(.44f,.88f,1);break;
                case "cael":case "arboreo":case "guarda-veterano":case "antidota":case "flo":
                    color=new Color(.55f,.83f,.31f);break;
                case "jazar":color=new Color(.65f,1,.15f);break;
                case "tsu":case "anuik":case "rosie":color=new Color(1,.49f,.72f);break;
                case "martio":case "bugaloo":case "alboguera":case "kayon":case "sepora":
                    color=new Color(.77f,.66f,1);break;
                case "trimol":case "korran":case "golem-tectonico":case "la-minera":case "protectora-del-mausoleo":
                    color=new Color(.76f,.63f,.43f);break;
            }
            return new ContactVisual {Color=color,Fire=fire,Dissolve=dissolve,Diameter=dissolve?.16f:ability?.35f:.30f};
        }

        void SpawnContact(Vector3 position,ContactVisual visual)
        {
            var fx=Spawn(visual.Dissolve?"projectile-dissolve":"contact",null,position,visual.Fire?.27f:.23f);
            if(fx==null)return;
            fx.Contact=visual;fx.Follow=false;fx.Persistent=false;fx.Body=null;
            fx.Caption.text="";fx.Floor.enabled=false;
            fx.Dome.enabled=!visual.Dissolve;fx.Dome.preserveAspect=true;
            foreach(var spark in fx.Sparks)spark.sprite=SpriteFor("spark");
            DrawContact(fx);
        }

        Vector2 ContactPoint(Vector3 world)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer,cameraView.WorldToScreenPoint(world),null,out var point);
            return point;
        }
        void DrawContact(Effect fx)
        {
            var visual=fx.Contact;
            float t=Mathf.Clamp01(fx.Age/Mathf.Max(.01f,fx.Duration));
            Vector2 origin=ContactPoint(fx.Anchor);
            float cell=Vector2.Distance(origin,ContactPoint(fx.Anchor+Vector3.right*Mathf.Abs(match.Mapper.CellSize.x)));
            float extent=cell*visual.Diameter;
            Vector2 direction=ContactPoint(fx.Anchor+visual.Direction)-origin;
            float angle=direction.sqrMagnitude>.001f?Mathf.Atan2(direction.y,direction.x):0;
            fx.Root.anchoredPosition=origin;fx.Floor.enabled=false;fx.Caption.text="";
            fx.Dome.rectTransform.localRotation=Quaternion.identity;
            fx.Dome.rectTransform.anchoredPosition=visual.Fire?Vector2.up*cell*.16f*t:Vector2.zero;
            bool authored=visual.Frames!=null&&visual.Frames.Length>0;
            if(authored)
            {
                var sprite=visual.Frames[Mathf.Min(visual.Frames.Length-1,Mathf.FloorToInt(t*visual.Frames.Length))];
                fx.Dome.sprite=sprite;
                fx.Dome.rectTransform.sizeDelta=Vector2.one*extent;
                fx.Dome.color=new Color(1,1,1,1-Mathf.Clamp01((t-.7f)/.3f));
            }
            else
            {
                fx.Dome.sprite=visual.Fire?ProjectileProceduralArt.Pixel("dash-flame",sprites):SpriteFor("star");
                fx.Dome.rectTransform.sizeDelta=Vector2.one*extent*(visual.Fire?.7f:.48f)*(1-t*.7f);
                fx.Dome.color=new Color(visual.Color.r,visual.Color.g,visual.Color.b,Mathf.Clamp01(1-t*2.5f));
            }
            for(int i=0;i<fx.Sparks.Length;i++)
            {
                var spark=fx.Sparks[i];spark.enabled=!authored||visual.Fire;
                float theta=angle+i*Mathf.PI/3;
                Vector2 p=new Vector2(Mathf.Cos(theta),Mathf.Sin(theta))*extent*(.12f+.30f*t);
                if(visual.Fire)p=new Vector2((i-2.5f)*extent*.11f,cell*(.04f+.16f*t)+Mathf.Sin(i*2)*extent*.08f);
                if(visual.Dissolve)p*=.55f;
                spark.rectTransform.anchoredPosition=p;
                spark.rectTransform.localRotation=Quaternion.Euler(0,0,visual.Fire?0:theta*Mathf.Rad2Deg);
                spark.rectTransform.sizeDelta=Vector2.one*extent*(visual.Dissolve?.10f:.13f)*(1-t*.7f);
                Color c=visual.Fire&&t>.45f?new Color(.35f,.29f,.24f):visual.Color;
                spark.color=new Color(c.r,c.g,c.b,(1-t)*(visual.Dissolve?.6f:1));
            }
        }

        public void TrackContactFlight(CombatAttackPresentationSnapshot emission,bool projectile,ProjectileVisualRegistry registry)
        {
            if(!isActiveAndEnabled||match==null||match.Phase!=MatchPhase.Combat||emission==null||!projectile||
                emission.Source==null||emission.Target==null||emission.Wave!=null||CombatSimulation.IsInstantRay(emission.Source))return;
            string id=emission.Source.BaseStats.DefinitionId;
            if(id=="hymay"||id=="martio")return; // Their dedicated strand/lane views own terminal visuals.
            var slot=ProjectileVisualSizing.ResolveEmission(registry,emission,emission.Source,emission.Target);
            if(slot!=null&&slot.OverrideSize&&slot.ClampedPercent==0)return;
            RecordContactFlight(emission.Source,emission.Target,emission.EmissionId,emission.IsAbility,false,
                emission.PreparationSeconds,emission.TravelSeconds);
        }

        public void TrackReleasedContactFlight(BattleUnit source,BattleUnit target,float travel)
        {
            if(!isActiveAndEnabled||match==null||source==null||target==null||match.Phase!=MatchPhase.Combat)return;
            RecordContactFlight(source,target,0,false,true,0,travel);
        }
        void RecordContactFlight(BattleUnit source,BattleUnit target,long emissionId,bool ability,bool auxiliary,float preparation,float travel)
        {
            EnsureSimulation();
            if(simulation==null||contactFlights.Count>=128)return;
            if(!auxiliary)foreach(var pending in contactFlights)if(!pending.Auxiliary&&pending.EmissionId==emissionId)return;
            var flight=contactFlightPool.Count>0?contactFlightPool.Pop():new ContactFlight();
            flight.Source=source;flight.Target=target;flight.SourceReset=source.CombatResetVersion;flight.TargetReset=target.CombatResetVersion;
            flight.EmissionId=emissionId;flight.Ability=ability;flight.Auxiliary=auxiliary;
            flight.Release=simulation.Elapsed+preparation;flight.Due=flight.Release+travel;flight.AttackReset=source.AttackResetVersion;
            flight.Released=preparation<=0;flight.Endpoint=target.transform.position;
            flight.MaterialId=source.BaseStats.DefinitionId;contactFlights.Add(flight);
        }

        void UpdateContactFlights()
        {
            if(simulation==null||match.Phase!=MatchPhase.Combat){ClearContactFlights();return;}
            float now=simulation.Elapsed;
            for(int i=contactFlights.Count-1;i>=0;i--)
            {
                var flight=contactFlights[i];
                bool identities=(flight.Source==null?flight.Released:flight.Source.CombatResetVersion==flight.SourceReset)&&
                    (flight.Target==null||flight.Target.CombatResetVersion==flight.TargetReset);
                if(!identities||(!flight.Released&&(flight.Source==null||flight.Source.AttackResetVersion!=flight.AttackReset||
                    !flight.Source.IsAlive||!flight.Source.isActiveAndEnabled||flight.Target==null||!flight.Target.IsAlive)))
                {ReleaseContactFlight(i);continue;}
                if(now>=flight.Release)flight.Released=true;
                if(flight.Target!=null&&flight.Target.IsAlive&&flight.Target.isActiveAndEnabled)
                    flight.Endpoint=flight.Target.transform.position;
                // One settled simulation tick of grace. Empty endpoints only dissolve, without a flash/ring.
                if(now+ .00001f<flight.Due+CombatSimulation.TickDuration)continue;
                SpawnContact(flight.Endpoint+Vector3.up*Mathf.Abs(match.Mapper.CellSize.y)*.28f,ContactStyle(flight.MaterialId,false,true));
                ReleaseContactFlight(i);
            }
        }
        void ReleaseContactFlight(int index)
        {
            var flight=contactFlights[index];contactFlights.RemoveAt(index);flight.Source=flight.Target=null;contactFlightPool.Push(flight);
        }
        void ClearContactFlights()
        {
            for(int i=contactFlights.Count-1;i>=0;i--)ReleaseContactFlight(i);
            lastContactSequence=0;
        }
    }

    public sealed partial class DocumentedBattleEffects
    {
        CombatStatusEffects contactEffects;
        CombatStatusEffects ContactEffects=>contactEffects!=null?contactEffects:(contactEffects=GetComponent<CombatStatusEffects>());
        void ContactProjectileBounced(BattleUnit source,BattleUnit origin,BattleUnit target,float travel)
            =>ContactEffects?.TrackReleasedContactFlight(source,target,travel);
        Vector3 ContactPosition(BattleUnit target)=>match.Simulation?.CurrentImpactPresentation?.Position??target.transform.position;
        float ContactCellPixels(Vector3 position)=>Vector2.Distance(Point(position),Point(position+Vector3.right*Mathf.Abs(match.Mapper.CellSize.x)));
    }
}
