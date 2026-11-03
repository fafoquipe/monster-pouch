using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class LobbyRigPart
    {
        public string name;
        public int owner;
        public float[] polygon,pivot,hand;
        public Vector2 Pivot=>new Vector2(pivot[0],pivot[1]);
        public Vector2 Hand=>hand!=null&&hand.Length==2?new Vector2(hand[0],hand[1]):Pivot;
        public bool Contains(Vector2 p)
        {
            bool inside=false;int count=polygon.Length/2;
            for(int i=0,j=count-1;i<count;j=i++)
            {
                float xi=polygon[i*2],yi=polygon[i*2+1],xj=polygon[j*2],yj=polygon[j*2+1];
                if((yi>p.y)!=(yj>p.y)&&p.x<(xj-xi)*(p.y-yi)/(yj-yi)+xi)inside=!inside;
            }
            return inside;
        }
    }
    [Serializable] public sealed class LobbyRigPatch
    {
        public float[] polygon,uv;
    }
    [Serializable] public sealed class LobbyRigDefinition
    {
        public string id,gesture;
        public LobbyRigPart[] parts;
        public LobbyRigPatch[] patches;
        public int OwnerAt(Vector2 point)
        {
            // An original source region is emitted by exactly one layer. Priority
            // matters at the shoulder, where the hand region meets the torso.
            foreach(var part in parts)if(part.Contains(point))return part.owner;
            return 0;
        }
        public LobbyRigPart Part(int owner){foreach(var part in parts)if(part.owner==owner)return part;return null;}
    }
    [Serializable] sealed class LobbyRigLibrary { public LobbyRigDefinition[] rigs; }
    public static class LobbyRigs
    {
        static Dictionary<string,LobbyRigDefinition> definitions;
        public static LobbyRigDefinition Get(string id)
        {
            if(definitions==null)
            {
                definitions=new Dictionary<string,LobbyRigDefinition>();
                var json=Resources.Load<TextAsset>("MonsterPouch/UI/LobbyRigs/rigs");
                if(json!=null)foreach(var rig in JsonUtility.FromJson<LobbyRigLibrary>(json.text).rigs)definitions[rig.id]=rig;
            }
            return definitions.TryGetValue(id,out var result)?result:null;
        }
    }
    // Source cells are immutable. Only their vertex positions are resampled.
    public readonly struct LobbyRigCell
    {
        public readonly Vector2 Min,Max;
        public readonly int Owner;
        public LobbyRigCell(Vector2 min,Vector2 max,int owner){Min=min;Max=max;Owner=owner;}
    }
}
