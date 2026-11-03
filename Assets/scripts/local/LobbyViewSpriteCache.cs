using System;
using System.Collections.Generic;
using UnityEngine;

namespace MonsterPouch.Local
{
    // Owns only the Sprite.Create results for opted-in LobbyViews. Imported
    // textures, legacy idles and combat sprites are never destroyed/unloaded here.
    public static class LobbyViewSpriteCache
    {
        public const int MaximumIdleEntries=2;
        public const long MaximumIdleRgbaBytes=16L*1024*1024;
        internal sealed class Entry
        {
            public LobbyFrameClip Clip;
            public Sprite Sprite;
            public int Users;
            public long RgbaBytes,LastRelease;
        }
        public sealed class Lease : IDisposable
        {
            Entry entry;
            internal Lease(Entry value){entry=value;value.Users++;}
            public Sprite Sprite=>entry?.Sprite;
            public void Dispose()
            {
                var old=entry;entry=null;
                if(old==null)return;
                old.Users--;old.LastRelease=++sequence;TrimToBudget();
            }
        }
        static readonly Dictionary<LobbyFrameClip,Entry> entries=new Dictionary<LobbyFrameClip,Entry>();
        static long sequence;
        static AsyncOperation pressureUnload;
        static float nextPressureUnload;
        public static int ActiveLeaseCount {get{int count=0;foreach(var e in entries.Values)count+=e.Users;return count;}}
        public static int IdleEntryCount {get{int count=0;foreach(var e in entries.Values)if(e.Users==0)count++;return count;}}
        // Pixel footprint is a cache budget, not a claim of actual native memory.
        public static long IdleRgbaBytes {get{long bytes=0;foreach(var e in entries.Values)if(e.Users==0)bytes+=e.RgbaBytes;return bytes;}}

        internal static Lease Acquire(LobbyFrameClip clip)
        {
            if(clip==null||string.IsNullOrEmpty(clip.idleTexture)||
                !clip.idleTexture.StartsWith("MonsterPouch/UI/LobbyViews/",StringComparison.Ordinal))return null;
            if(entries.TryGetValue(clip,out var found)&&found.Sprite!=null)return new Lease(found);
            // A destroyed Unity object can remain in a managed dictionary with
            // domain reload disabled. Old leases must not remove its replacement.
            if(found!=null)entries.Remove(clip);
            var texture=Resources.Load<Texture2D>(clip.idleTexture);
            var r=clip.idleRect;
            if(texture==null||r==null||r.Length<4)return null;
            for(int i=0;i<4;i++)if(float.IsNaN(r[i])||float.IsInfinity(r[i]))return null;
            if(r[0]<0||r[1]<0||r[2]<=0||r[3]<=0||r[0]+r[2]>texture.width||r[1]+r[3]>texture.height)return null;
            var sprite=Sprite.Create(texture,new Rect(r[0],r[1],r[2],r[3]),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
            sprite.name="gogos-minis-idle-"+clip.id;
            var entry=new Entry{Clip=clip,Sprite=sprite,RgbaBytes=(long)texture.width*texture.height*4};
            entries.Add(clip,entry);return new Lease(entry);
        }
        static void Evict(Entry entry)
        {
            if(entry.Users!=0)return;
            if(entries.TryGetValue(entry.Clip,out var current)&&ReferenceEquals(entry,current))entries.Remove(entry.Clip);
            var owned=entry.Sprite;entry.Sprite=null;
            if(owned==null)return;
            if(Application.isPlaying)UnityEngine.Object.Destroy(owned);
            else UnityEngine.Object.DestroyImmediate(owned);
        }
        static void TrimToBudget()
        {
            while(IdleEntryCount>MaximumIdleEntries||IdleRgbaBytes>MaximumIdleRgbaBytes)
            {
                Entry oldest=null;
                foreach(var entry in entries.Values)if(entry.Users==0&&(oldest==null||entry.LastRelease<oldest.LastRelease))oldest=entry;
                if(oldest==null)break;Evict(oldest);
            }
        }
        public static void TrimUnused()
        {
            // Snapshot only on an explicit trim/OS pressure event, never per frame.
            var unused=new List<Entry>();foreach(var e in entries.Values)if(e.Users==0)unused.Add(e);
            foreach(var e in unused)Evict(e);
        }
        static void OnLowMemory()
        {
            TrimUnused();
#if !UNITY_EDITOR
            // A system memory warning is the only trigger. Coalesce warnings and
            // let Unity preserve assets still reached by active/shared consumers.
            // No global collection is performed on card scrolling/navigation.
            if(!Application.isPlaying||(pressureUnload!=null&&!pressureUnload.isDone)||Time.unscaledTime<nextPressureUnload)return;
            nextPressureUnload=Time.unscaledTime+30;
            pressureUnload=Resources.UnloadUnusedAssets();
#endif
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            Application.lowMemory-=OnLowMemory;Application.lowMemory+=OnLowMemory;
            // Preserve live leases if scene reload is disabled. Their owners will
            // release/reacquire normally; only unused entries can be destroyed.
            TrimUnused();nextPressureUnload=0;
        }
    }
}
