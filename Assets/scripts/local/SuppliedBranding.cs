using UnityEngine;

namespace MonsterPouch.Local
{
    public static class SuppliedBranding
    {
        public const string ResourcePath="MonsterPouch/UI/Branding/eronya-logo";
        public const string DisplayName="MONSTERS OF ERONYA";
        // The launcher still uses the separate legacy icon tile. This is not the logo crop.
        public static readonly Rect IconRect=new Rect(1270,0,756,756);
        static Sprite logo;
        public static Sprite Logo
        {
            get
            {
                if(logo!=null)return logo;
                var source=Resources.Load<Texture2D>(ResourcePath);
                if(source==null)return null;
                logo=Sprite.Create(source,VisibleLogoRect(source),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
                logo.name="Monsters of Eronya supplied logo";
                return logo;
            }
        }

        static Rect VisibleLogoRect(Texture2D source)
        {
            // The supplied PNG has almost-transparent (alpha 1-3) noise in its margins.
            // Find the actual artwork once, retaining every leaf, flame and spiral.
            // A small transparent gutter prevents the outer pixels touching the sprite edge.
            if(!source.isReadable)return new Rect(0,0,source.width,source.height);
            var pixels=source.GetPixels32();
            int left=source.width,bottom=source.height,right=-1,top=-1;
            for(int y=0;y<source.height;y++)for(int x=0;x<source.width;x++)
            {
                if(pixels[y*source.width+x].a<4)continue;
                left=Mathf.Min(left,x);right=Mathf.Max(right,x);
                bottom=Mathf.Min(bottom,y);top=Mathf.Max(top,y);
            }
            if(right<left)return new Rect(0,0,source.width,source.height);
            return Rect.MinMaxRect(Mathf.Max(0,left-2),Mathf.Max(0,bottom-2),
                Mathf.Min(source.width,right+3),Mathf.Min(source.height,top+3));
        }
    }
}
