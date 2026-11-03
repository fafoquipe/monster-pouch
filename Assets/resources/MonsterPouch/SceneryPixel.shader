Shader "MonsterPouch/PortraitPixelScenery"
{
    Properties
    {
        [PerRendererData] _MainTex ("Scenery", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CropRect ("Centered source crop", Vector) = (0,0,1,1)
        _PixelSize ("Source pixels per block", Float) = 1
        _Saturation ("Palette saturation", Range(0,1)) = .84
        _Brightness ("Brightness", Range(0,2)) = 1
        _Contrast ("Contrast", Range(0,2)) = 1
        _WarmAccentBoost ("Yellow and red accents", Range(0,1)) = 0
        _FabricRed ("Red velvet fabric", Range(0,1)) = 0
        _VelvetBacking ("Padded velvet backing", Range(0,1)) = 0
        _WarmTint ("Warm room tint", Range(0,1)) = 0
        _RoyalBannerPalette ("Royal lobby crimson and gold banners", Range(0,1)) = 0
        _HotelCarpetRed ("Hotel carpet muted wine red", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 local:TEXCOORD1; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _CropRect, _ClipRect;
            fixed4 _Color, _TextureSampleAdd;
            float _PixelSize,_Saturation,_Brightness,_Contrast,_WarmAccentBoost,_FabricRed,_VelvetBacking,_WarmTint,_RoyalBannerPalette,_HotelCarpetRed;
            v2f vert(appdata v)
            {
                v2f o; o.local=v.vertex; o.vertex=UnityObjectToClipPos(v.vertex);
                o.uv=_CropRect.xy+v.uv*_CropRect.zw; o.color=v.color*_Color; return o;
            }
            float2 snapUV(float2 uv,float2 texel)
            {
                float2 block=texel*max(1,_PixelSize);
                return clamp((floor(uv/block)+.5)*block,texel*.5,1-texel*.5);
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,snapUV(i.uv,_MainTex_TexelSize.xy));
                c=(c+_TextureSampleAdd)*i.color;
                float yellow=smoothstep(.03,.22,min(c.r,c.g)-c.b)*smoothstep(.30,.65,c.r)*step(c.g*.85,c.r);
                float red=smoothstep(.05,.25,c.r-max(c.g,c.b));
                float3 source=c.rgb;
                float luminance=dot(source,float3(.2126,.7152,.0722));
                float fabric=smoothstep(.005,.045,max(source.g,source.b)-source.r);
                c.rgb=lerp(dot(c.rgb,float3(.2126,.7152,.0722)).xxx,c.rgb,_Saturation);
                c.rgb=lerp(c.rgb,source,max(yellow,red)*_WarmAccentBoost);
                c.rgb=lerp(c.rgb,float3(source.r,min(source.r,source.g*1.12),source.b*.50),yellow*_WarmAccentBoost);
                c.rgb=lerp(c.rgb,float3(source.r,source.g*.78,source.b*.78),red*_WarmAccentBoost);
                c.rgb=lerp(c.rgb,float3(luminance*2.6+.02,luminance*.32+.003,luminance*.38+.004),fabric*_FabricRed);
                if(_VelvetBacking>.5) {
                // Fixed-size upholstery diamonds and fine pile remain continuous across the panel.
                float2 cloth=floor(i.local.xy)+.5;
                float2 cell=frac(float2(cloth.x/72+cloth.y/56,cloth.x/72-cloth.y/56))-.5;
                float2 edge=.5-abs(cell);
                float padding=pow(saturate((1-4*cell.x*cell.x)*(1-4*cell.y*cell.y)),.65);
                float tuft=1-smoothstep(.012,.075,length(edge));
                float pile=frac(52.9829189*frac(dot(floor(i.local.xy*2),float2(.06711056,.00583715))))-.5;
                float3 velvet=lerp(float3(.22,.021,.037),float3(.43,.058,.087),padding);
                velvet+=float3(.025,.007,.009)*(cell.y-cell.x)*padding+pile*.019;
                velvet=lerp(velvet,float3(.12,.009,.021),tuft*.82);
                c.rgb=lerp(c.rgb,velvet,fabric*_VelvetBacking);
                }
                c.rgb=lerp(c.rgb,luminance*float3(1.55,.65,.72),_WarmTint);
                c.rgb=saturate(((c.rgb-.5)*_Contrast+.5)*_Brightness);
                if(_HotelCarpetRed>0) {
                    // Follow the carpet's perspective, retaining the original weave and lighting.
                    float y=1-i.uv.y;
                    float halfWidth=lerp(.24,.46,saturate((y-.41)/.30));
                    float floorMask=smoothstep(.407,.43,y)*(1-smoothstep(halfWidth-.015,halfWidth,abs(i.uv.x-.5)));
                    float3 carpet=c.rgb;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    carpet=LinearToGammaSpace(carpet);
                    #endif
                    float3 wine=saturate(carpet*float3(1.36,.95,.97));
                    carpet=lerp(carpet,wine,floorMask*_HotelCarpetRed);
                    #ifndef UNITY_COLORSPACE_GAMMA
                    carpet=GammaToLinearSpace(carpet);
                    #endif
                    c.rgb=carpet;
                }
                if(_RoyalBannerPalette>.5) {
                    // Four wall banners and two small standards beside the weapons.
                    float x=min(i.uv.x,1-i.uv.x),y=1-i.uv.y;
                    float outer=step(.022,x)*step(x,.084)*step(y,.29);
                    float inner=step(.298,x)*step(x,.382)*step(.045,y)*step(y,.32);
                    float lower=step(.027,x)*step(x,.076)*step(.438,y)*step(y,.523);
                    float banner=saturate(outer+inner+lower);
                    // Palette thresholds refer to the source PNG's displayed sRGB colors.
                    float3 palette=source;
                    #ifndef UNITY_COLORSPACE_GAMMA
                    palette=LinearToGammaSpace(source);
                    #endif
                    float tone=dot(palette,float3(.2126,.7152,.0722));
                    float cloth=smoothstep(.025,.060,palette.r-max(palette.g,palette.b));
                    float embroidery=smoothstep(.045,.15,min(palette.r,palette.g)-palette.b)*smoothstep(.32,.48,palette.r);
                    float3 crimson=saturate(float3(.72,.045,.10)*clamp(tone/.22,.65,1.25));
                    float3 gold=saturate(float3(1,.75,.22)*clamp(tone/.50,.7,1.1));
                    #ifndef UNITY_COLORSPACE_GAMMA
                    crimson=GammaToLinearSpace(crimson);gold=GammaToLinearSpace(gold);
                    #endif
                    c.rgb=lerp(c.rgb,saturate(crimson),banner*cloth*(1-embroidery));
                    c.rgb=lerp(c.rgb,saturate(gold),banner*embroidery);
                }
                #ifdef UNITY_UI_CLIP_RECT
                c.a*=UnityGet2DClipping(i.local.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}

