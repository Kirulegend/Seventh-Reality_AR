Shader "Dissolve/UI_Dissolve"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _DissolveAmount ("Dissolve Amount", Range(0, 1)) = 0.0
        _CutoutMap ("Cutout Map", 2D) = "white" {}
        _CutoutTiling ("Cutout Tiling", Vector) = (1, 1, 0, 0)
        _RampMap ("Ramp Map", 2D) = "white" {}
        _UseRampMap ("Use Ramp Map", Float) = 1.0

        _EdgeWidth ("Edge Width", Range(0.001, 0.4)) = 0.08
        [HDR] _EdgeColor ("Edge Color", Color) = (0.0, 0.8, 1.0, 1.0)
        [HDR] _CoreColor ("Core Color", Color) = (2.0, 2.5, 3.0, 1.0)
        _EdgeIntensity ("Edge Intensity", Range(0.0, 10.0)) = 2.0

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
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            sampler2D _CutoutMap;
            float4 _CutoutTiling;
            sampler2D _RampMap;
            float _UseRampMap;
            float _DissolveAmount;
            float _EdgeWidth;
            half4 _EdgeColor;
            half4 _CoreColor;
            float _EdgeIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                float dissolve = _DissolveAmount;
                if ( dissolve >= 0.999 ) clip(-1.0);

                float2 cutoutUV = IN.texcoord * _CutoutTiling.xy + _CutoutTiling.zw;
                float noise = tex2D(_CutoutMap, cutoutUV).r;

                float adjustedAmount = dissolve * (1.0 + _EdgeWidth);
                float clipVal = noise - adjustedAmount;
                if ( dissolve > 0.001 ) clip(clipVal);

                float edgeFactor = saturate(clipVal / max(0.0001, _EdgeWidth));
                float rampCoord = 1.0 - edgeFactor;

                half4 glow = lerp(_CoreColor, _EdgeColor, edgeFactor) * _EdgeIntensity;
                if ( _UseRampMap > 0.5 )
                {
                    half4 rampCol = tex2D(_RampMap, float2(rampCoord, 0.5));
                    glow = rampCol * _EdgeIntensity;
                }

                float edgePresence = saturate(dissolve * 50.0);
                half3 finalRGB = lerp(color.rgb, lerp(glow.rgb, color.rgb, edgeFactor), edgePresence);
                half finalAlpha = color.a;

                #ifdef UNITY_UI_ALPHACLIP
                clip(finalAlpha - 0.001);
                #endif

                return fixed4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }
}

