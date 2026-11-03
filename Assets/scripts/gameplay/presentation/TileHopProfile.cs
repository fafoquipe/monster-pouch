using UnityEngine;
namespace MonsterPouch.Gameplay.Presentation
{
 public readonly struct TileHopProfile
 {
  public readonly float Prepare,Air,Height,Squash;
  public readonly bool Walk;
  public TileHopProfile(float prepare,float air,float height,float squash=.06f,bool walk=false)
  {Prepare=prepare;Air=air;Height=height;Squash=squash;Walk=walk;}
  public float Flight(float phase)=>Mathf.Clamp01((phase-Prepare)/Air);
  public static TileHopProfile For(string id)
  {
   switch(id)
   {
    case "anuik":return new TileHopProfile(0,1,0,.02f,true);
    case "atong":return new TileHopProfile(.54f,.28f,.10f,.045f);
    case "jazar":case "trimol":case "tokoro":return new TileHopProfile(.48f,.31f,.06f,.035f);
    case "dummy":return new TileHopProfile(.40f,.38f,.10f,.045f);
    case "bugaloo":case "kayon":case "blotan":return new TileHopProfile(.45f,.34f,.08f,.05f);
    case "flo":return new TileHopProfile(.43f,.35f,.08f,.035f);
    case "suki":return new TileHopProfile(.40f,.40f,.10f,.035f);
    // Kazuru keeps the existing travel phases; only his visible lift and compression increase.
    case "kazuru":return new TileHopProfile(.43f,.36f,.18f,.08f);
    case "aky":case "bugui":return new TileHopProfile(.35f,.43f,.15f,.06f);
    default:return new TileHopProfile(.43f,.36f,.12f,.055f);
   }
  }
 }
}
