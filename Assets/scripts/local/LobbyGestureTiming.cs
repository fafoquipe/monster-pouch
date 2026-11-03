using UnityEngine;
namespace MonsterPouch.Local
{
    // Reveal, readable main action, then put away. Times are authored per personality.
    public readonly struct LobbyGestureTiming
    {
        public readonly float Reveal, Main, Return;
        public float Duration=>Reveal+Main+Return;
        public LobbyGestureTiming(float reveal,float main,float finish){Reveal=reveal;Main=main;Return=finish;}
        public float PoseProgress(float normalized)
        {
            float time=Mathf.Clamp01(normalized)*Duration;
            if(time<Reveal)return .20f*time/Reveal;
            if(time<Reveal+Main)return .20f+.56f*(time-Reveal)/Main;
            return .76f+.24f*Mathf.Clamp01((time-Reveal-Main)/Return);
        }
        public static LobbyGestureTiming For(string id)
        {
            switch(id)
            {
                case "anuik":return new LobbyGestureTiming(.62f,2.55f,.68f);
                case "tokoro":return new LobbyGestureTiming(.55f,2.50f,.58f);
                case "atong":return new LobbyGestureTiming(.60f,2.35f,.65f);
                case "flo":return new LobbyGestureTiming(.75f,2.15f,.70f);
                case "gochan":return new LobbyGestureTiming(.55f,1.65f,.65f);
                case "jazar":return new LobbyGestureTiming(.60f,2.15f,.60f);
                case "kayon":return new LobbyGestureTiming(.70f,2.20f,.75f);
                case "bugaloo":return new LobbyGestureTiming(.65f,2.20f,.60f);
                case "popow":return new LobbyGestureTiming(.50f,1.70f,.60f);
                case "sepora":return new LobbyGestureTiming(.60f,2.20f,.65f);
                case "tauris":return new LobbyGestureTiming(.60f,2.10f,.60f);
                case "atori":return new LobbyGestureTiming(.55f,1.75f,.55f);
                case "blotan":return new LobbyGestureTiming(.60f,2.00f,.65f);
                case "bugui":return new LobbyGestureTiming(.60f,1.95f,.65f);
                case "stein":return new LobbyGestureTiming(.60f,2.10f,.65f);
                case "trimol":return new LobbyGestureTiming(.65f,2.65f,.70f);
                case "tsu":return new LobbyGestureTiming(.55f,2.10f,.60f);
                case "aky":return new LobbyGestureTiming(.85f,2.10f,.80f);
                case "hymay":return new LobbyGestureTiming(.55f,1.90f,.65f);
                default:return new LobbyGestureTiming(.60f,2.00f,.60f);
            }
        }
    }
}
