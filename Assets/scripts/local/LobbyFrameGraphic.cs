using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    public enum LobbyArtworkContext { Standard, BattleHome }

    // Display complete authored drawings. Each pose is exactly one unwarped
    // textured quad. Optional measured registration corrects drawing size with
    // one deterministic uniform scale per pose, without deforming its artwork.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyFrameGraphic : MaskableGraphic
    {
        static Dictionary<string,LobbyFrameClip> clips,idles,homeIdles,lobbyViews;
        bool usesLobbyView;
        public bool UsesLobbyView=>usesLobbyView;
        LobbyFrameClip clip;
        Sprite idle;
        LobbyViewSpriteCache.Lease viewLease;
        int frame=-1;
        float progress;
        bool loadFrames;
        MonsterPouch.Gameplay.Presentation.UnitArt presentationArt;
        LobbyArtworkContext artworkContext;

        public string UnitId=>clip?.id;
        public LobbyFrameClip SourceClip=>clip;
        public LobbyFramePose CurrentFrame=>frame>=0&&HasAuthoredFrames?clip.frames[frame]:null;
        public bool HasAuthoredFrames=>loadFrames&&clip!=null&&clip.atlas!=null&&clip.sprites!=null&&clip.sprites.Length>0;
        public int PoseCount=>HasAuthoredFrames?clip.sprites.Length:0;
        public int FrameIndex=>frame;
        public Sprite CurrentSprite=>frame>=0&&HasAuthoredFrames?clip.sprites[frame]:idle;
        public static Vector2 GroundAnchor(Rect bounds)=>new Vector2(bounds.center.x,bounds.yMin);
        public Vector2 FootAnchor=>GroundAnchor(rectTransform.rect);
        // Register the complete idle drawing, independently of padding or an old gesture pivot.
        public Vector2 IdleSourceAnchor=>clip==null?Vector2.zero:
            usesLobbyView?LobbyViewLayout.SourceAnchor(clip):new Vector2(clip.idleBounds[0]+clip.idleBounds[2]*.5f,clip.idleBounds[1]);
        Vector2 IdleTargetAnchor=>usesLobbyView?LobbyViewLayout.TargetAnchor(rectTransform.rect,clip.idleFloating):FootAnchor;
        public bool UsesCharacterIdle=>clip!=null&&string.IsNullOrEmpty(clip.idleTexture)&&clip.idleSprite!=null;
        // Apply the menu percentage only after the inherited fit. Callers size the
        // figure with their old baseline and must not multiply this percentage again.
        public float ContextScale=>presentationArt==null?1:artworkContext==LobbyArtworkContext.BattleHome?
            presentationArt.GetLobbyScale():presentationArt.GetBriefScale();
        public float BodyHeight=>clip==null?0:FitScale*clip.idleBounds[3];
        public float BodyWidth=>clip==null?0:FitScale*clip.idleBounds[2];
        public float ReferenceHeight=>clip?.resolvedReferenceHeight??0;
        public float UniformFrameScale=>HasAuthoredFrames?BodyHeight/clip.resolvedReferenceHeight*(CurrentFrame?.ResolvedRegistrationScale??1):0;
        public bool IsAtRest=>frame<0;
        public float NormalizedGestureTime=>progress;
        float FitScale
        {
            get
            {
                if(clip==null||clip.idleBounds==null)return 0;
                var box=rectTransform.rect;
                if(usesLobbyView)return LobbyViewLayout.FitScale(box,clip,ContextScale);
                return Mathf.Min(box.width*.94f/clip.idleBounds[2],box.height*.94f/clip.idleBounds[3])*ContextScale;
            }
        }
        public override Texture mainTexture=>CurrentSprite!=null?CurrentSprite.texture:Texture2D.whiteTexture;

        static Dictionary<string,LobbyFrameClip> ReadManifest(string path)
        {
            var result=new Dictionary<string,LobbyFrameClip>();
            var json=Resources.Load<TextAsset>(path);
            if(json==null)return result;
            var parsed=JsonUtility.FromJson<LobbyFrameManifest>(json.text);
            if(parsed?.clips!=null)foreach(var value in parsed.clips)if(value!=null&&!string.IsNullOrEmpty(value.id))result[value.id]=value;
            return result;
        }
        static bool Fields(float[] values,int length)=>values!=null&&values.Length>=length;
        static bool Finite(float number)=>!float.IsNaN(number)&&!float.IsInfinity(number);
        static void CompleteIdle(LobbyFrameClip target,LobbyFrameClip original)
        {
            if(string.IsNullOrEmpty(target.idleTexture))target.idleTexture=original.idleTexture;
            if(!Fields(target.idleRect,4))target.idleRect=original.idleRect;
            if(!Fields(target.idleBounds,4))target.idleBounds=original.idleBounds;
            if(!Fields(target.idleAnchor,2))target.idleAnchor=original.idleAnchor;
        }
        public bool Configure(string id,bool loadAuthoredFrames=false,LobbyArtworkContext context=LobbyArtworkContext.Standard,bool useLobbyView=false)
        {
            ReleaseLobbyView();idle=null;frame=-1;progress=0;
            loadFrames=loadAuthoredFrames;usesLobbyView=false;
            presentationArt=CharacterUiArtwork.GetCharacter(id)?.Visuals;artworkContext=context;
            if(homeIdles==null)homeIdles=ReadManifest("MonsterPouch/UI/BattleHomeIdles/clips");
            // New stills are opt-in menu art. Authored Standard gestures keep their
            // original idle and atlas; the BattleHome surface is already a still.
            if(useLobbyView&&lobbyViews==null)lobbyViews=ReadManifest("MonsterPouch/UI/LobbyViews/clips");
            LobbyFrameClip view=null;
            usesLobbyView=useLobbyView&&(!loadAuthoredFrames||context==LobbyArtworkContext.BattleHome)&&lobbyViews.TryGetValue(id,out view);
            bool home=!usesLobbyView&&context==LobbyArtworkContext.BattleHome&&homeIdles.TryGetValue(id,out clip);
            material=!usesLobbyView&&!home&&id=="anuik"?Resources.Load<Material>("MonsterPouch/AnuikContourUI"):null;
            if(usesLobbyView){clip=view;loadFrames=false;}
            else if(home)loadFrames=false;
            else
            {
                if(clips==null)clips=ReadManifest("MonsterPouch/UI/LobbyFrames/clips");
                // This reads only the old reference-idle coordinates. Neither old
                // animation textures nor the abandoned rigs are ever instantiated.
                if(idles==null)idles=ReadManifest("MonsterPouch/UI/LobbyGestures/clips");
                idles.TryGetValue(id,out var original);
                if(clips.TryGetValue(id,out clip))
                {
                    if(original!=null)CompleteIdle(clip,original);
                }
                else
                {
                    clip=original!=null?new LobbyFrameClip{id=id,idleTexture=original.idleTexture,idleRect=original.idleRect,
                        idleBounds=original.idleBounds,idleAnchor=original.idleAnchor,frames=Array.Empty<LobbyFramePose>()}:CharacterIdle(id);
                    if(clip==null)return false;
                    clips[id]=clip;
                }
            }
            if(!Fields(clip.idleRect,4)||!Fields(clip.idleBounds,4)||!Fields(clip.idleAnchor,2))return false;
            if(usesLobbyView)
            {
                if(!AcquireLobbyView())return false;
            }
            else if(clip.idleSprite==null)
            {
                var texture=Resources.Load<Texture2D>(clip.idleTexture);var r=clip.idleRect;
                if(texture==null)return false;
                clip.idleSprite=Sprite.Create(texture,new Rect(r[0],r[1],r[2],r[3]),new Vector2(.5f,0),100,0,SpriteMeshType.FullRect);
                clip.idleSprite.name="gogos-minis-idle-"+id;
            }
            if(!usesLobbyView)idle=clip.idleSprite;
            if(loadFrames&&clip.frames!=null&&clip.frames.Length>0&&!string.IsNullOrEmpty(clip.texture))PrepareFrames(clip);
            frame=-1;progress=0;raycastTarget=false;SetAllDirty();
            var shadow=transform.parent!=null?transform.parent.GetComponentInChildren<LobbyContactShadow>():null;
            if(shadow!=null&&shadow.transform.parent==transform.parent)shadow.Configure(this);
            if(usesLobbyView&&!isActiveAndEnabled)ReleaseLobbyView();
            return true;
        }
        bool AcquireLobbyView()
        {
            if(viewLease==null||viewLease.Sprite==null)
            {
                viewLease?.Dispose();viewLease=LobbyViewSpriteCache.Acquire(clip);
            }
            idle=viewLease?.Sprite;return idle!=null;
        }
        void ReleaseLobbyView()
        {
            if(viewLease==null)return;
            idle=null;viewLease.Dispose();viewLease=null;
        }
        protected override void OnEnable()
        {
            base.OnEnable();
            if(usesLobbyView&&clip!=null){AcquireLobbyView();SetAllDirty();}
        }
        protected override void OnDisable()
        {
            base.OnDisable();ReleaseLobbyView();
        }
        protected override void OnDestroy()
        {
            ReleaseLobbyView();base.OnDestroy();
        }
        static LobbyFrameClip CharacterIdle(string id)
        {
            // Missing lobby manifests reuse the character's existing full-body idle.
            // Calibration is already authored in source pixels / feet-to-head units;
            // no texture readback, portrait substitution or combat-scale change is needed.
            var art=CharacterUiArtwork.GetCharacter(id)?.Visuals;
            var sprite=art?.Idle(MonsterPouch.Gameplay.Presentation.UnitFacing.South);
            if(sprite==null)return null;
            var r=sprite.rect;var pivot=sprite.pivot;
            float width=art.ReferencePixelWidth,height=art.VitalsHeadAnchor.y*sprite.pixelsPerUnit;
            bool calibrated=Finite(width)&&Finite(height)&&width>0&&height>0;
            var bounds=calibrated?new Rect(pivot.x-width*.5f,pivot.y,width,height):new Rect(0,0,r.width,r.height);
            return new LobbyFrameClip{id=id,idleSprite=sprite,idleRect=new[]{r.x,r.y,r.width,r.height},
                idleBounds=new[]{bounds.x,bounds.y,bounds.width,bounds.height},
                idleAnchor=new[]{bounds.center.x,bounds.yMin},frames=Array.Empty<LobbyFramePose>()};
        }
        static void PrepareFrames(LobbyFrameClip value)
        {
            if(value.sprites!=null)return;
            var texture=Resources.Load<Texture2D>(value.texture);if(texture==null)return;
            var heights=new List<float>(value.frames.Length);
            foreach(var pose in value.frames)
            {
                if(pose==null||!Fields(pose.rect,4)||!Fields(pose.anchor,2))return;
                var r=pose.rect;
                for(int k=0;k<4;k++)if(!Finite(r[k]))return;
                if(!Finite(pose.anchor[0])||!Finite(pose.anchor[1]))return;
                if(r[0]<0||r[1]<0||r[2]<=0||r[3]<=0||r[0]+r[2]>texture.width||r[1]+r[3]>texture.height)return;
                if(pose.height>0&&!float.IsNaN(pose.height)&&!float.IsInfinity(pose.height))heights.Add(pose.height);
            }
            if(value.referenceHeight>0&&Finite(value.referenceHeight))value.resolvedReferenceHeight=value.referenceHeight;
            else
            {
                if(heights.Count==0)return;
                heights.Sort();int middle=heights.Count/2;
                value.resolvedReferenceHeight=heights.Count%2==0?(heights[middle-1]+heights[middle])*.5f:heights[middle];
            }
            var sprites=new Sprite[value.frames.Length];
            for(int i=0;i<sprites.Length;i++)
            {
                var pose=value.frames[i];var r=pose.rect;
                sprites[i]=Sprite.Create(texture,new Rect(r[0],r[1],r[2],r[3]),
                    new Vector2(pose.anchor[0]/r[2],pose.anchor[1]/r[3]),100,0,SpriteMeshType.FullRect);
                sprites[i].name="lobby-frame-"+value.id+"-"+i;
            }
            value.atlas=texture;value.sprites=sprites;
        }
        public int FrameCountForPhase(int phase)=>LobbyFramePlayback.FrameCountForPhase(PoseCount,phase);
        public int FrameAtTime(float normalized)=>LobbyFramePlayback.FrameAtTime(UnitId,PoseCount,normalized);
        public void SampleFrame(float normalized,bool active=true)
        {
            if(!Finite(normalized)){progress=0;SetPose(-1);return;}
            progress=Mathf.Clamp01(normalized);
            SetPose(active?FrameAtTime(progress):-1);
        }
        public void SampleGesture(float normalized,bool active=true)=>SampleFrame(normalized,active);
        public void SetPose(int index)
        {
            int next=HasAuthoredFrames&&index>=0?Mathf.Min(index,PoseCount-1):-1;
            if(frame==next)return;bool textureChanges=(frame<0)!=(next<0);
            frame=next;SetVerticesDirty();if(textureChanges)SetMaterialDirty();
        }
        public Vector2 RestPoint(Vector2 bodyUV)
        {
            if(clip==null)return FootAnchor;
            var b=clip.idleBounds;var a=IdleSourceAnchor;
            return IdleTargetAnchor+new Vector2(b[0]+bodyUV.x*b[2]-a.x,b[1]+bodyUV.y*b[3]-a.y)*FitScale;
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var sprite=CurrentSprite;if(sprite==null||clip==null)return;
            Color light=color*MonsterPouch.Gameplay.Presentation.SceneCharacterLight.Sample(0,.6f,(int)CardTableTheme.Lobby);
            Rect r=sprite.rect;float scale;Vector2 anchor;
            if(frame<0){scale=FitScale;anchor=IdleSourceAnchor;}
            else{scale=UniformFrameScale;var a=clip.frames[frame].anchor;anchor=new Vector2(a[0],a[1]);}
            Vector2 origin=(frame<0?IdleTargetAnchor:FootAnchor)-anchor*scale;
            float width=r.width*scale,height=r.height*scale;
            float u=r.x/sprite.texture.width,v=r.y/sprite.texture.height,du=r.width/sprite.texture.width,dv=r.height/sprite.texture.height;
            vh.AddVert(origin,light,new Vector2(u,v));
            vh.AddVert(origin+new Vector2(width,0),light,new Vector2(u+du,v));
            vh.AddVert(origin+new Vector2(width,height),light,new Vector2(u+du,v+dv));
            vh.AddVert(origin+new Vector2(0,height),light,new Vector2(u,v+dv));
            vh.AddTriangle(0,1,2);vh.AddTriangle(0,2,3);
        }

        // Archived rig APIs remain compile-compatible while old editor helpers are
        // migrated. They report no rig and can never activate its rendering path.
        public bool HasContinuousGesture=>false;
        public bool UsesTwoStoneJuggle=>false;
        public float GestureHorizontalScale=>1;
        public LobbyRigDefinition RigDefinition=>null;
        public LobbyRigSample MotionSample=>default;
        public LobbyRigPartGraphic PartsGraphic=>null;
        public LobbyRigPropGraphic PropsGraphic=>null;
        public LobbyRigEffectsGraphic EffectsGraphic=>null;
        public LobbyRigWingGraphic WingsGraphic=>null;
        public LobbyRigBackGraphic BackGraphic=>null;
        public int SourceCellCount=>0;
        public int MovingCellCount=>0;
        public LobbyRigCell[] BodySourceCells=>Array.Empty<LobbyRigCell>();
        public LobbyRigCell[] MovingSourceCells=>Array.Empty<LobbyRigCell>();
        public float Wingspan=>BodyWidth;
        public float HeartGlowStrength=>0;
        public Vector2 HeartPosition=>RestPoint(new Vector2(.5f,.152f));
        public Vector2 LeftHandPosition=>FootAnchor;
        public Vector2 RightHandPosition=>FootAnchor;
        public bool RightArmBehindBack=>false;
        public int SourceOwnerAt(Vector2 p)=>0;
        public Vector2 Map(Vector2 p,int owner)=>RestPoint(p);
        public Vector2 DeformedPoint(Vector2 p)=>RestPoint(p);
        public Vector2 ToLocal(Vector2 p)=>FootAnchor+p*BodyHeight;
        public Vector2 SourceUV(Vector2 p)
        {
            if(clip==null||idle==null)return Vector2.zero;var r=clip.idleRect;var b=clip.idleBounds;
            return new Vector2((r[0]+b[0]+p.x*b[2])/idle.texture.width,(r[1]+b[1]+p.y*b[3])/idle.texture.height);
        }
        public Vector2 StabilizePoint(Vector2 point)=>point;
        public Vector2 StabilizeContinuousPoint(Vector2 point,float normalized)=>point;
        public void PopulateMoving(VertexHelper vh,bool behind=false)=>vh.Clear();
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyRigPartGraphic:MaskableGraphic
    {
        public void Configure(LobbyFrameGraphic body){raycastTarget=false;SetAllDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)=>vh.Clear();
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyRigBackGraphic:MaskableGraphic
    {
        public void Configure(LobbyFrameGraphic body){raycastTarget=false;SetAllDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)=>vh.Clear();
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyContactShadow : MaskableGraphic
    {
        LobbyFrameGraphic body;
        public void Configure(LobbyFrameGraphic value){body=value;SetVerticesDirty();}
        LobbyFrameGraphic Body
        {
            get
            {
                if(body==null&&transform.parent!=null)
                {
                    var candidate=transform.parent.GetComponentInChildren<LobbyFrameGraphic>();
                    if(candidate!=null&&candidate.transform.parent==transform.parent)body=candidate;
                }
                return body;
            }
        }
        public Vector2 FootAnchor=>Body!=null?(Vector2)transform.InverseTransformPoint(Body.transform.TransformPoint(Body.FootAnchor)):
            LobbyFrameGraphic.GroundAnchor(rectTransform.rect);
        protected override void Awake(){base.Awake();raycastTarget=false;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var rect=rectTransform.rect;var center=FootAnchor;
            float rx=Mathf.Min(rect.width*.34f,48),ry=Mathf.Clamp(rect.height*.052f,4,9);
            float scale=Body!=null?Body.ContextScale:1;rx*=scale;ry*=scale;
            const int segments=32;Color shade=new Color(.045f,.06f,.13f,.32f);vh.AddVert(center,shade,Vector2.zero);
            for(int ring=0;ring<3;ring++)for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments,radius=ring==0?.45f:ring==1?.78f:1;
                var c=shade;c.a=ring==0?.27f:ring==1?.12f:0;
                vh.AddVert(center+new Vector2(Mathf.Cos(angle)*rx,Mathf.Sin(angle)*ry)*radius,c,Vector2.zero);
            }
            for(int i=0;i<segments;i++)
            {
                int next=(i+1)%segments;vh.AddTriangle(0,1+i,1+next);
                for(int ring=0;ring<2;ring++)
                {
                    int a=1+ring*segments+i,b=1+ring*segments+next,c=a+segments,d=b+segments;
                    vh.AddTriangle(a,c,b);vh.AddTriangle(b,c,d);
                }
            }
        }
    }
}
