using System;
using MonsterPouch.Gameplay.Board;
using UnityEngine;

namespace MonsterPouch.Gameplay.Match
{
    // Immutable release geometry shared by gameplay, the board renderer and the inspector.
    public sealed class MaestroWaveFlight
    {
        public const string EffectId = "maestro-de-las-mareas-oleaje";
        public const float DefaultSpeed = 5.5f;
        public const int DefaultLength = 9;
        public const float CrestDepth = .8f;
        public const float CollapseSeconds = .2f;
        public readonly int OriginX, OriginY, Forward, Width, Length, MinX, MaxX;
        public readonly float Speed, TravelSeconds, Left, Right, EndRow, Distance;
        readonly float[] columnDistances=new float[BoardManager.Width];
        public MaestroWaveFlight(int x, int y, int forward, int width, int length, float speed,Func<int,int,bool> blocked=null)
        {
            OriginX=x; OriginY=y; Forward=forward<0?-1:1; Width=Math.Max(1,width); Length=Math.Max(1,length);
            int a=x-((Width-1)/2)*Forward, b=x+(Width/2)*Forward;
            Left=Math.Min(a,b)-.5f; Right=Math.Max(a,b)+.5f;
            MinX=Math.Max(0,Math.Min(a,b)); MaxX=Math.Min(BoardManager.Width-1,Math.Max(a,b));
            Speed=NormalizeSpeed(speed);
            float wall=Forward>0?BoardManager.Height-.5f-y-CrestDepth*.5f:y+.5f-CrestDepth*.5f;
            float maximum=Math.Min(Length,Math.Max(0,wall));
            for(int column=MinX;column<=MaxX;column++)
            {
                float distance=maximum;
                if(blocked!=null)for(int row=y+Forward;row>=0&&row<BoardManager.Height;row+=Forward)
                {
                    float ahead=(row-y)*Forward;
                    if(ahead>Length)break;
                    if(blocked(column,row)){distance=Math.Min(distance,Math.Max(0,ahead-.5f-CrestDepth*.5f));break;}
                }
                columnDistances[column]=distance;
                Distance=Math.Max(Distance,distance);
            }
            EndRow=y+Forward*Distance;
            TravelSeconds=CombatSimulation.GetProjectileTravelTime(Distance,Speed);
        }
        public static float NormalizeSpeed(float speed) =>
            float.IsNaN(speed)||float.IsInfinity(speed)?DefaultSpeed:Math.Max(.1f,speed);
        public float ColumnTravelSeconds(int x)=>CombatSimulation.GetProjectileTravelTime(columnDistances[x],Distance/TravelSeconds);
        public bool ContainsPath(int x,int y) => x>=MinX&&x<=MaxX&&(y-OriginY)*Forward>0&&
            (y-OriginY)*Forward<=Length&&(y-OriginY)*Forward<=columnDistances[x]+CrestDepth*.5f;
        public bool ContainsSweep(int x,int y,float from,float to) => ContainsPath(x,y)&&from<ColumnTravelSeconds(x)-.00001f&&
            y>=Math.Min(ColumnRow(x,from),ColumnRow(x,to))-CrestDepth*.5f-.00001f&&
            y<=Math.Max(ColumnRow(x,from),ColumnRow(x,to))+CrestDepth*.5f+.00001f;
        public float ColumnRow(int x,float elapsed)=>OriginY+Forward*Math.Min(columnDistances[x],Distance*Mathf.Clamp01(elapsed/TravelSeconds));
        public float Row(float elapsed) => Mathf.Lerp(OriginY,EndRow,Mathf.Clamp01(elapsed/TravelSeconds));
        public void Sample(float elapsed,out Rect bounds,out Rect uv,out float alpha)
        {
            float collapse=Mathf.Clamp01((elapsed-TravelSeconds)/CollapseSeconds);
            float depth=CrestDepth*(1-collapse), row=Row(elapsed);
            float bottom=row-depth*.5f, top=row+depth*.5f;
            float left=Mathf.Max(-.5f,Left), right=Mathf.Min(BoardManager.Width-.5f,Right);
            float clippedBottom=Mathf.Max(-.5f,bottom), clippedTop=Mathf.Min(BoardManager.Height-.5f,top);
            bounds=Rect.MinMaxRect(left,clippedBottom,right,clippedTop);
            float u0=(left-Left)/(Right-Left),u1=(right-Left)/(Right-Left);
            float v0=depth>0?(clippedBottom-bottom)/depth:0,v1=depth>0?(clippedTop-bottom)/depth:1;
            uv=new Rect(u0,v0,u1-u0,v1-v0);
            alpha=1-collapse;
        }
        public void SampleColumn(int x,float elapsed,out Rect bounds,out Rect uv,out float alpha)
        {
            float collapse=Mathf.Clamp01((elapsed-ColumnTravelSeconds(x))/CollapseSeconds);
            float depth=CrestDepth*(1-collapse),row=ColumnRow(x,elapsed);
            float left=Mathf.Max(-.5f,x-.5f),right=Mathf.Min(BoardManager.Width-.5f,x+.5f);
            bounds=Rect.MinMaxRect(left,Mathf.Max(-.5f,row-depth*.5f),right,Mathf.Min(BoardManager.Height-.5f,row+depth*.5f));
            uv=new Rect((left-Left)/(Right-Left),0,(right-left)/(Right-Left),1);
            alpha=1-collapse;
        }
    }
}
