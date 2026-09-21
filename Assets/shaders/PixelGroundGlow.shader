Shader "MonsterPouch/PixelGroundGlow"
{
    Properties { [PerRendererData] _MainTex("Glow",2D)="white"{} _BoardRect("Board bounds",Vector)=(-100,-100,100,100) }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always Blend SrcAlpha One
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _BoardRect;
            struct Attributes { float4 positionOS:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float2 world:TEXCOORD1;float4 color:COLOR; };
            Varyings vert(Attributes input)
            {
                Varyings o;o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.world=TransformObjectToWorld(input.positionOS.xyz).xy;o.uv=input.uv;o.color=input.color;return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                clip(input.world-_BoardRect.xy);clip(_BoardRect.zw-input.world);
                return SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*input.color;
            }
            ENDHLSL
        }
    }
}
