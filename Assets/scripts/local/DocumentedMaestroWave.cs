using System.Collections;
using MonsterPouch.Gameplay.Match;
using UnityEngine;

namespace MonsterPouch.Local
{
    public sealed partial class DocumentedBattleEffects
    {
        bool TryMaestroWave(CombatAttackPresentationSnapshot emission)
        {
            if(emission?.Wave==null)return false;
            if(!isActiveAndEnabled||layer==null||match?.Mapper==null)return true;
            var sprite=Resources.Load<Sprite>(MaestroWaveArt.Resource);
            if(sprite!=null)StartCoroutine(MaestroWave(emission,sprite));
            return true;
        }
        Vector2 MaestroBoardPoint(float x,float y)
        {
            var mapper=match.Mapper;
            Vector3 origin=mapper.GetWorldPosition(match.Board.GetCell(0,0));
            float sign=mapper.Flipped?-1:1;
            return Point(origin+new Vector3(sign*x*mapper.CellSize.x,sign*y*mapper.CellSize.y,0));
        }
        IEnumerator MaestroWave(CombatAttackPresentationSnapshot emission,Sprite sprite)
        {
            var simulation=match.Simulation;
            float released=simulation.Elapsed,visualElapsed=0;
            var wave=emission.Wave;
            var root=EffectRoot("maestro-traveling-wave-"+emission.EmissionId);
            var graphic=root.gameObject.AddComponent<MaestroWaveGraphic>();
            try
            {
                while(root!=null&&Current(simulation)&&emission.Source!=null&&
                    emission.Source.CombatResetVersion==emission.SourceResetVersion)
                {
                    float authoritative=Mathf.Max(0,simulation.Elapsed-released);
                    visualElapsed=Mathf.Min(Mathf.Max(visualElapsed,authoritative),authoritative+CombatSimulation.TickDuration);
                    float shown=authoritative+.00001f<wave.TravelSeconds
                        ?Mathf.Min(visualElapsed,wave.TravelSeconds-.00001f):Mathf.Max(wave.TravelSeconds,visualElapsed);
                    if(shown>=wave.TravelSeconds+MaestroWaveFlight.CollapseSeconds)break;
                    wave.Sample(shown,out _,out _,out float alpha);
                    var origin=MaestroBoardPoint(0,0);
                    bool reverseX=MaestroBoardPoint(1,0).x<origin.x,reverseY=MaestroBoardPoint(0,1).y<origin.y;
                    graphic.Begin(sprite,alpha);
                    for(int x=wave.MinX;x<=wave.MaxX;x++)
                    {
                        wave.SampleColumn(x,shown,out var bounds,out var section,out float columnAlpha);
                        if(columnAlpha<=0)continue;
                        graphic.Add(MaestroWaveArt.Uv(section,wave.Forward,reverseX,reverseY),columnAlpha,
                            MaestroBoardPoint(bounds.xMin,bounds.yMin),MaestroBoardPoint(bounds.xMin,bounds.yMax),
                            MaestroBoardPoint(bounds.xMax,bounds.yMax),MaestroBoardPoint(bounds.xMax,bounds.yMin));
                    }
                    graphic.Complete();
                    yield return null;
                    visualElapsed+=EffectDelta;
                }
            }
            finally{ReleaseEffect(root);}
        }
    }
}
