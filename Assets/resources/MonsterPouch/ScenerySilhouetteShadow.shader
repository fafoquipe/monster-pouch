Shader "MonsterPouch/ScenerySilhouetteShadow"
{
    Properties
    {
        _MainTex ("Source sprite alpha", 2D) = "white" {}
        _BoardExclusion ("Board frame world bounds", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 position:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float2 world:TEXCOORD1; };
            sampler2D _MainTex; float4 _BoardExclusion;
            v2f vert(appdata v)
            {
                v2f o;o.position=UnityObjectToClipPos(v.vertex);o.world=mul(unity_ObjectToWorld,v.vertex).xy;o.color=v.color;o.uv=v.uv;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                // Keep the complete playable board and frame free of decoration shadows.
                if(i.world.x>=_BoardExclusion.x && i.world.y>=_BoardExclusion.y &&
                   i.world.x<=_BoardExclusion.z && i.world.y<=_BoardExclusion.w)discard;
                return fixed4(.018,.027,.029,tex2D(_MainTex,i.uv).a*i.color.a);
            }
            ENDCG
        }
    }
}

