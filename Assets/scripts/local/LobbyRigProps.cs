using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterPouch.Local
{
    [Serializable] public sealed class LobbyRigSpriteRect { public string name;public float[] rect,pivot; }
    [Serializable] sealed class LobbyRigSpriteAtlas { public int width,height;public LobbyRigSpriteRect[] sprites; }
    public static class LobbyRigAssets
    {
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        static readonly HashSet<string> loaded=new HashSet<string>();
        public static Sprite Get(string atlas,string name)
        {
            string key=atlas+"/"+name;
            if(!loaded.Contains(atlas))
            {
                loaded.Add(atlas);
                string path="MonsterPouch/UI/LobbyRigs/"+atlas;
                var texture=Resources.Load<Texture2D>(path);var json=Resources.Load<TextAsset>(path+".parts");
                if(texture!=null&&json!=null)
                {
                    var data=JsonUtility.FromJson<LobbyRigSpriteAtlas>(json.text);
                    foreach(var part in data.sprites)
                    {
                        var r=part.rect;
                        var sprite=Sprite.Create(texture,new Rect(r[0],r[1],r[2],r[3]),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                        sprite.name="lobby-rig-"+part.name;sprites[atlas+"/"+part.name]=sprite;
                    }
                }
            }
            return sprites.TryGetValue(key,out var value)?value:null;
        }
        public static void Quad(VertexHelper vh,Sprite sprite,Vector2 center,Vector2 size,float angle,Color color)
        {
            if(sprite==null||size.x<=.0001f||size.y<=.0001f||color.a<=0)return;
            var rect=sprite.rect;var tex=sprite.texture;int start=vh.currentVertCount;
            float u=rect.x/tex.width,v=rect.y/tex.height,w=rect.width/tex.width,h=rect.height/tex.height;
            vh.AddVert(center+LobbyRigMotion.Rotate(new Vector2(-size.x,-size.y)*.5f,angle),color,new Vector2(u,v));
            vh.AddVert(center+LobbyRigMotion.Rotate(new Vector2(size.x,-size.y)*.5f,angle),color,new Vector2(u+w,v));
            vh.AddVert(center+LobbyRigMotion.Rotate(new Vector2(size.x,size.y)*.5f,angle),color,new Vector2(u+w,v+h));
            vh.AddVert(center+LobbyRigMotion.Rotate(new Vector2(-size.x,size.y)*.5f,angle),color,new Vector2(u,v+h));
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyRigPropGraphic:MaskableGraphic
    {
        LobbyFrameGraphic body;Sprite sprite;
        public Sprite PropSprite=>sprite;
        public override Texture mainTexture=>sprite!=null?sprite.texture:Texture2D.whiteTexture;
        public void Configure(LobbyFrameGraphic value)
        {
            body=value;string name=null;
            switch(body.UnitId)
            {
                case "tokoro":name="ice-cream";break;case "atong":name="handkerchief";break;
                case "kayon":name="wooden-dummy";break;case "blotan":name="gold-badge";break;
                case "gochan":name="talisman";break;case "tauris":name="toothpick";break;
            }
            if(name!=null)sprite=LobbyRigAssets.Get("props",name);raycastTarget=false;SetAllDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(body==null||body.IsAtRest||sprite==null)return;
            var pose=body.MotionSample;float h=body.BodyHeight,scale=pose.Weight,angle=0,propHeight=.16f;
            var position=body.RightHandPosition;
            switch(body.UnitId)
            {
                case "tokoro":propHeight=.23f;position+=new Vector2(-.008f,.075f)*h;
                    scale=LobbyRigMotion.Ease(Mathf.InverseLerp(.24f,.62f,pose.Reveal))*(1-LobbyRigMotion.Ease(Mathf.InverseLerp(.4f,.8f,pose.Finish)));break;
                case "atong":propHeight=.115f;position+=new Vector2(-.016f,.012f)*h;angle=15+10*LobbyRigMotion.Cycle(pose.Main,2);
                    scale=LobbyRigMotion.Ease(Mathf.InverseLerp(.45f,.66f,pose.Reveal))*(1-LobbyRigMotion.Ease(Mathf.InverseLerp(.35f,.53f,pose.Finish)));break;
                case "kayon":propHeight=.185f;position+=new Vector2(-.04f,.055f)*h;angle=5*LobbyRigMotion.Cycle(pose.Main,1);break;
                case "blotan":propHeight=.135f;position=Vector2.Lerp(body.LeftHandPosition,body.RightHandPosition,.5f)+Vector2.up*.045f*h;
                    angle=12*LobbyRigMotion.Cycle(pose.Main,1);break;
                case "gochan":propHeight=.10f;position=body.LeftHandPosition+new Vector2(.02f,.025f)*h;angle=-14;break;
                case "tauris":propHeight=.105f;position=body.LeftHandPosition+new Vector2(.025f,.026f)*h;angle=-18+6*LobbyRigMotion.Cycle(pose.Main,2);break;
            }
            var size=new Vector2(sprite.rect.width/sprite.rect.height,1)*propHeight*h*scale;
            // Growing from the gripping palm is a geometric reveal, never a blend
            // between alternative character silhouettes.
            LobbyRigAssets.Quad(vh,sprite,position,size,angle,body.color);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyRigWingGraphic:MaskableGraphic
    {
        LobbyFrameGraphic body;Sprite left,right;
        public Sprite LeftWing=>left;
        public Sprite RightWing=>right;
        public override Texture mainTexture=>left!=null?left.texture:Texture2D.whiteTexture;
        public void Configure(LobbyFrameGraphic value)
        {
            body=value;left=LobbyRigAssets.Get("aky-wings","aky-wing-left");right=LobbyRigAssets.Get("aky-wings","aky-wing-right");
            raycastTarget=false;SetAllDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(body==null||body.IsAtRest||left==null||right==null)return;
            float opening=body.MotionSample.Weight;
            // Full span is 1.96 original body widths. Each root remains hidden
            // behind the original torso while the membrane fans out continuously.
            float width=body.BodyWidth*.67f*opening,height=body.BodyWidth*.435f;
            var l=body.RestPoint(new Vector2(.19f,.42f));var r=body.RestPoint(new Vector2(.81f,.42f));
            LobbyRigAssets.Quad(vh,left,l+new Vector2(-width*.5f,0),new Vector2(width,height),0,body.color);
            LobbyRigAssets.Quad(vh,right,r+new Vector2(width*.5f,0),new Vector2(width,height),0,body.color);
        }
    }
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class LobbyRigEffectsGraphic:MaskableGraphic
    {
        LobbyFrameGraphic body;
        public float HeartGlowAlpha=>body!=null?body.HeartGlowStrength:0;
        public Vector2 GlowCenter=>body!=null?body.HeartPosition:Vector2.zero;
        public float GlowRadius=>body!=null?body.BodyHeight*.12f:0;
        public void Configure(LobbyFrameGraphic value){body=value;raycastTarget=false;SetAllDirty();}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(body==null||body.IsAtRest)return;
            var p=body.MotionSample;float h=body.BodyHeight,w=p.Weight,m=p.Main;
            switch(body.UnitId)
            {
                case "anuik":
                    Glow(vh,body.HeartPosition,h*.115f,new Color(1,.94f,.48f,.24f*body.HeartGlowStrength));
                    Star(vh,body.HeartPosition+new Vector2(.067f,.065f)*h,h*.026f,body.HeartGlowStrength);
                    Star(vh,body.HeartPosition+new Vector2(-.064f,.024f)*h,h*.016f,body.HeartGlowStrength*.6f);break;
                case "tokoro":
                    float lick=LobbyRigMotion.Pulse(m,.12f,.43f)+LobbyRigMotion.Pulse(m,.53f,.84f);
                    var mouth=body.RestPoint(new Vector2(.73f,.75f));
                    Line(vh,mouth,mouth+new Vector2(.050f,-.045f)*h*lick,h*.021f*lick,new Color(.94f,.42f,.47f,w));break;
                case "atong":
                    Star(vh,body.RestPoint(new Vector2(.635f,.327f)),h*.04f,LobbyRigMotion.Pulse(m,.72f,.99f)*w);break;
                case "flo":
                    float nurture=.5f+.5f*Mathf.Sin(m*Mathf.PI);
                    Star(vh,body.RestPoint(new Vector2(.59f,.72f)),h*.026f,w*.65f*nurture);break;
                case "jazar":
                    float before=1-LobbyRigMotion.Ease(Mathf.InverseLerp(.75f,.80f,m));
                    var bubble=body.LeftHandPosition+new Vector2(.015f,.09f)*h;
                    Bubble(vh,bubble,.07f*h*w*before,new Color(.52f,1,.15f,.7f*w));
                    float pop=LobbyRigMotion.Pulse(m,.76f,.99f);
                    for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;Disc(vh,bubble+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.095f*h*LobbyRigMotion.Ease(Mathf.InverseLerp(.76f,.94f,m)),.01f*h*pop,new Color(.61f,1,.17f,pop));}break;
                case "sepora":
                    var spark=(body.LeftHandPosition+body.RightHandPosition)*.5f+Vector2.up*h*.055f;
                    Glow(vh,spark,h*.065f,new Color(.78f,.43f,1,.45f*w));
                    Star(vh,spark,h*.025f,w,new Color(.9f,.71f,1,1));break;
                case "blotan":Star(vh,(body.LeftHandPosition+body.RightHandPosition)*.5f+new Vector2(.035f,.08f)*h,h*.027f,LobbyRigMotion.Pulse(m,.40f,.80f)*w);break;
                case "stein":
                    var aHand=body.LeftHandPosition;var bHand=body.RightHandPosition;
                    var mid=(aHand+bHand)*.5f+Vector2.up*.025f*h;
                    Line(vh,aHand,mid,h*.008f,new Color(.55f,.96f,1,w*.85f));
                    Line(vh,mid,bHand,h*.008f,new Color(.55f,.96f,1,w*.85f));
                    Star(vh,mid,h*.021f,w,new Color(.63f,1,1,1));break;
                case "tsu":
                    Bubble(vh,body.RestPoint(new Vector2(.5f,.455f)),h*.067f*w*(1+.025f*LobbyRigMotion.Cycle(m,1)),new Color(1,.48f,.77f,.88f*w));break;
            }
        }
        static void Disc(VertexHelper vh,Vector2 center,float radius,Color tint)
        {
            if(radius<=.00001f||tint.a<=0)return;const int n=24;int start=vh.currentVertCount;
            vh.AddVert(center,tint,Vector2.zero);
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,tint,Vector2.zero);}
            for(int i=0;i<n;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%n);
        }
        static void Glow(VertexHelper vh,Vector2 center,float radius,Color tint)
        {
            if(tint.a<=0)return;const int n=32;int start=vh.currentVertCount;vh.AddVert(center,tint,Vector2.zero);
            Color edge=tint;edge.a=0;
            for(int i=0;i<n;i++){float a=i*Mathf.PI*2/n;vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,edge,Vector2.zero);}
            for(int i=0;i<n;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%n);
        }
        static void Bubble(VertexHelper vh,Vector2 center,float radius,Color tint)
        {
            Disc(vh,center,radius,new Color(tint.r*.55f,tint.g*.55f,tint.b*.55f,tint.a));
            Disc(vh,center,radius*.87f,tint);Disc(vh,center+new Vector2(-.3f,.35f)*radius,radius*.18f,new Color(1,1,1,tint.a));
        }
        static void Star(VertexHelper vh,Vector2 center,float radius,float alpha,Color? tint=null)
        {
            if(alpha<=0)return;var c=tint??new Color(1,1,.86f,1);c.a*=alpha;
            int start=vh.currentVertCount;vh.AddVert(center,c,Vector2.zero);
            for(int i=0;i<8;i++){float angle=i*Mathf.PI/4;float r=i%2==0?radius:radius*.23f;vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r,c,Vector2.zero);}
            for(int i=0;i<8;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%8);
        }
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color tint)
        {
            if(width<=0||tint.a<=0)return;var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int s=vh.currentVertCount;
            vh.AddVert(a-normal,tint,Vector2.zero);vh.AddVert(a+normal,tint,Vector2.zero);
            vh.AddVert(b+normal,tint,Vector2.zero);vh.AddVert(b-normal,tint,Vector2.zero);
            vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
    }
}

