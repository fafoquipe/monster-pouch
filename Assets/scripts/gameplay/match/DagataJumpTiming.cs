using System;

namespace MonsterPouch.Gameplay.Match
{
    // One simulation timeline; rendering only samples it. Zero-length phases remain zero.
    public readonly struct DagataJumpTiming
    {
        public readonly int Start, Depart, Land, Recover, End;
        public DagataJumpTiming(int start, int prepare, int travel, int hold, int recovery)
        {
            Start=start; Depart=start+Math.Max(0,prepare); Land=Depart+Math.Max(0,travel);
            Recover=Land+Math.Max(0,hold); End=Recover+Math.Max(0,recovery);
        }
        public bool Finished(int tick)=>tick>=End;
        public float TravelProgress(int tick)=>Land<=Depart?(tick>=Land?1:0):Clamp((tick-Depart)/(float)(Land-Depart));
        public float RecoveryProgress(int tick)=>End<=Recover?(tick>=End?1:0):Clamp((tick-Recover)/(float)(End-Recover));
        public int Pose(int tick)=>tick<Depart?0:tick<Land?1:2;
        static float Clamp(float value)=>Math.Max(0,Math.Min(1,value));
    }
}
