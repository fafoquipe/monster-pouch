using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class PlayerAvatarEntry { public string id,resource; public float[] face; }
    [Serializable] public sealed class PlayerAvatarManifest { public PlayerAvatarEntry[] entries; }

    // Authored compact Icon first; legacy face windows remain a fallback for non-roster art.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class PlayerAvatarGraphic : MaskableGraphic
    {
        static Dictionary<string,PlayerAvatarEntry> entries;
        Texture2D texture;
        Rect uv=new Rect(0,0,1,1);
        public string CharacterId { get; private set; }
        public override Texture mainTexture=>texture!=null?texture:Texture2D.whiteTexture;
        public bool Configure(string id)
        {
            raycastTarget=false;CharacterId=id;texture=null;uv=new Rect(0,0,1,1);
            SetAllDirty();
            // Personality portraits are exclusive to profile customization.
            var profile=string.IsNullOrEmpty(id)?null:Resources.Load<Texture2D>("MonsterPouch/UI/ProfilePersonalityIcons/"+id);
            if(profile!=null)
            {
                texture=profile;
                return true;
            }
            var authored=CharacterUiArtwork.Icon(id);
            if(authored!=null)
            {
                texture=authored.texture;
                var bounds=UnityEngine.Sprites.DataUtility.GetOuterUV(authored);
                uv=new Rect(bounds.x,bounds.y,bounds.z-bounds.x,bounds.w-bounds.y);
                return true;
            }
            if(entries==null)
            {
                entries=new Dictionary<string,PlayerAvatarEntry>(StringComparer.Ordinal);
                var source=Resources.Load<TextAsset>("MonsterPouch/UI/PlayerAvatars");
                var data=source!=null?JsonUtility.FromJson<PlayerAvatarManifest>(source.text):null;
                if(data?.entries!=null)foreach(var item in data.entries)if(item!=null&&!string.IsNullOrEmpty(item.id))entries[item.id]=item;
            }
            if(string.IsNullOrEmpty(id)||!entries.TryGetValue(id,out var entry)||entry.face==null||entry.face.Length!=4)return false;
            texture=Resources.Load<Texture2D>(entry.resource);
            uv=new Rect(entry.face[0],1-entry.face[1]-entry.face[3],entry.face[2],entry.face[3]);
            SetAllDirty();return texture!=null;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(texture==null)return;var r=rectTransform.rect;
            vh.AddVert(new Vector2(r.xMin,r.yMin),color,new Vector2(uv.xMin,uv.yMin));
            vh.AddVert(new Vector2(r.xMin,r.yMax),color,new Vector2(uv.xMin,uv.yMax));
            vh.AddVert(new Vector2(r.xMax,r.yMax),color,new Vector2(uv.xMax,uv.yMax));
            vh.AddVert(new Vector2(r.xMax,r.yMin),color,new Vector2(uv.xMax,uv.yMin));
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }
    }
}
