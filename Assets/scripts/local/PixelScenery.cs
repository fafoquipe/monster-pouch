using UnityEngine;

namespace MonsterPouch.Local
{
    public static class PixelScenery
    {
        // The source artwork already contains its pixel grid. Do not pixelate it again.
        public const float PixelSize=1;
        public static readonly Color MarginColor=new Color(.025f,.04f,.045f,1);
        public static Material CreateMaterial()
        {
            var shader=Resources.Load<Shader>("MonsterPouch/SceneryPixel");
            return shader!=null?new Material(shader){name="Portrait pixel scenery"}:null;
        }
        // Uniform cover: crop excess artwork symmetrically, never stretch it.
        public static Vector4 CenterCrop(Vector2 sourceSize)
        {
            float sourceAspect=sourceSize.x/Mathf.Max(1,sourceSize.y),aspect=540f/960;
            float width=Mathf.Min(1,aspect/sourceAspect),height=Mathf.Min(1,sourceAspect/aspect);
            return new Vector4((1-width)*.5f,(1-height)*.5f,width,height);
        }
        public static void Configure(Material material,Sprite artwork)
        {
            if(material==null)return;
            material.SetFloat("_PixelSize",PixelSize);
            material.SetVector("_CropRect",artwork!=null?CenterCrop(artwork.rect.size):new Vector4(0,0,1,1));
        }
    }
}
