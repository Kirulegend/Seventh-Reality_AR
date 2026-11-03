Shader "Dissolve/URP_Dissolve"
{
    Properties
    {
        [Header(Surface Properties)]
        _BaseMap ("Base Map (RGB)", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _Smoothness ("Smoothness", Range(0, 1)) = 0.5
        _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1.0

        [Header(Dissolve Progress)]
        _DissolveAmount ("Dissolve Progress", Range(0, 1)) = 0.0
        [Enum(NoiseOnly, 0, VerticalSweep, 1, RadialSweep, 2)] _SweepMode ("Sweep Mode", Float) = 1
        _SweepDirection ("Sweep Direction (XYZ)", Vector) = (0, 1, 0, 0)
        _SweepBounds ("Sweep Min/Max Bounds (X=Min, Y=Max)", Vector) = (-1.0, 2.0, 0, 0)

        [Header(Cutout Pattern)]
        _CutoutMap ("Cutout Noise Map", 2D) = "white" {}
        _CutoutScale ("Cutout Frequency", Float) = 2.5
        _CutoutScroll ("Cutout Animation Scroll (XY)", Vector) = (0.02, 0.04, 0, 0)
        _DetailNoiseStrength ("Micro Sizzle Detail", Range(0, 1)) = 0.35
        _WarpStrength ("Turbulence Distortion", Range(0, 1)) = 0.3

        [Header(Color Ramp Edge)]
        [Toggle] _UseRampMap ("Use Color Ramp Map", Float) = 1
        _RampMap ("Color Ramp Map", 2D) = "white" {}
        _RampScale ("Ramp Brightness / Intensity", Range(1, 30)) = 10.0
        [HDR] _EdgeColor ("Custom Flame / Arc Color", Color) = (0.0, 0.8, 1.0, 1.0)
        [HDR] _CoreColor ("Custom White Core Color", Color) = (2.5, 3.0, 3.5, 1.0)
        _AshColor ("Scorched Ash Color", Color) = (0.04, 0.03, 0.02, 1.0)
        _EdgeWidth ("Flame / Glow Width", Range(0.005, 0.45)) = 0.12
        _CoreWidth ("Inner Core Width", Range(0.005, 0.5)) = 0.035
        _AshWidth ("Charred Soot Width", Range(0.0, 0.3)) = 0.08
        _EdgeIntensity ("Procedural Edge Multiplier", Range(1, 30)) = 8.0

        [Header(Sparks and Dynamic Debris)]
        _SparkleIntensity ("Sparkle / Micro-Embers", Range(0, 30)) = 15.0
        _PulseSpeed ("Energy Pulse Speed", Float) = 4.0
        _DisplacementAmount ("Edge Vertex Tearing", Range(0, 0.08)) = 0.02

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest+50"
        }

        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

        float3 _LightDirection;
        float3 _LightPosition;

        float4 ComputeShadowPositionHClip(float3 positionWS, float3 normalWS)
        {
            float3 lightDir = _LightDirection;
            float4 posCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            return ApplyShadowClamping(posCS);
        }

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _CutoutMap_ST;
            float4 _BaseColor;
            float4 _EdgeColor;
            float4 _CoreColor;
            float4 _AshColor;
            float4 _SweepDirection;
            float4 _SweepBounds;
            float4 _CutoutScroll;
            float _Metallic;
            float _Smoothness;
            float _BumpScale;
            float _DissolveAmount;
            float _SweepMode;
            float _CutoutScale;
            float _DetailNoiseStrength;
            float _WarpStrength;
            float _UseRampMap;
            float _RampScale;
            float _EdgeWidth;
            float _CoreWidth;
            float _AshWidth;
            float _EdgeIntensity;
            float _SparkleIntensity;
            float _PulseSpeed;
            float _DisplacementAmount;
        CBUFFER_END

        TEXTURE2D(_BaseMap);       SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap);       SAMPLER(sampler_BumpMap);
        TEXTURE2D(_CutoutMap);     SAMPLER(sampler_CutoutMap);
        TEXTURE2D(_RampMap);       SAMPLER(sampler_RampMap);

        float3 Mod289(float3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 Mod289(float4 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
        float4 Permute(float4 x) { return Mod289(((x * 34.0) + 1.0) * x); }
        float4 TaylorInvSqrt(float4 r) { return 1.79284291400159 - 0.85373472095314 * r; }

        float SimplexNoise(float3 v)
        {
            const float2 C = float2(1.0 / 6.0, 1.0 / 3.0);
            const float4 D = float4(0.0, 0.5, 1.0, 2.0);

            float3 i  = floor(v + dot(v, C.yyy));
            float3 x0 = v - i + dot(i, C.xxx);

            float3 g = step(x0.yzx, x0.xyz);
            float3 l = 1.0 - g;
            float3 i1 = min(g.xyz, l.zxy);
            float3 i2 = max(g.xyz, l.zxy);

            float3 x1 = x0 - i1 + C.xxx;
            float3 x2 = x0 - i2 + C.yyy;
            float3 x3 = x0 - D.yyy;

            i = Mod289(i);
            float4 p = Permute(Permute(Permute(
                        i.z + float4(0.0, i1.z, i2.z, 1.0))
                    + i.y + float4(0.0, i1.y, i2.y, 1.0))
                    + i.x + float4(0.0, i1.x, i2.x, 1.0));

            float n_ = 0.142857142857;
            float3 ns = n_ * D.wyz - D.xzx;

            float4 j = p - 49.0 * floor(p * ns.z * ns.z);

            float4 x_ = floor(j * ns.z);
            float4 y_ = floor(j - 7.0 * x_);

            float4 x = x_ * ns.x + ns.yyyy;
            float4 y = y_ * ns.x + ns.yyyy;
            float4 h = 1.0 - abs(x) - abs(y);

            float4 b0 = float4(x.xy, y.xy);
            float4 b1 = float4(x.zw, y.zw);

            float4 s0 = floor(b0) * 2.0 + 1.0;
            float4 s1 = floor(b1) * 2.0 + 1.0;
            float4 sh = -step(h, float4(0, 0, 0, 0));

            float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
            float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;

            float3 p0 = float3(a0.xy, h.x);
            float3 p1 = float3(a0.zw, h.y);
            float3 p2 = float3(a1.xy, h.z);
            float3 p3 = float3(a1.zw, h.w);

            float4 norm = TaylorInvSqrt(float4(dot(p0, p0), dot(p1, p1), dot(p2, p2), dot(p3, p3)));
            p0 *= norm.x;
            p1 *= norm.y;
            p2 *= norm.z;
            p3 *= norm.w;

            float4 m = max(0.6 - float4(dot(x0, x0), dot(x1, x1), dot(x2, x2), dot(x3, x3)), 0.0);
            m = m * m;
            return 42.0 * dot(m * m, float4(dot(p0, x0), dot(p1, x1), dot(p2, x2), dot(p3, x3)));
        }

        float CalculateCutout(float3 positionWS, float3 positionOS, float2 uv)
        {
            float2 scroll = _Time.y * _CutoutScroll.xy;
            float2 cutoutUV = uv * _CutoutScale + scroll;

            float warp = SimplexNoise(positionWS * 1.5 + float3(0, _Time.y * 0.25, 0)) * _WarpStrength;
            float4 tex = SAMPLE_TEXTURE2D_LOD(_CutoutMap, sampler_CutoutMap, cutoutUV + float2(warp, warp), 0);

            float baseVal = tex.r;
            float detail = SimplexNoise(positionWS * 12.0 + float3(0, _Time.y * 0.8, 0)) * 0.5 + 0.5;
            float combined = lerp(baseVal, detail, _DetailNoiseStrength);

            if ( _SweepMode > 0.5 && _SweepMode < 1.5 )
            {
                float3 dir = normalize(_SweepDirection.xyz);
                float proj = dot(positionWS, dir);
                float range = max(0.001, _SweepBounds.y - _SweepBounds.x);
                float sweepGrad = saturate((proj - _SweepBounds.x) / range);
                return saturate(sweepGrad * 0.65 + combined * 0.4);
            }
            else if ( _SweepMode > 1.5 )
            {
                float dist = length(positionOS);
                float radius = max(0.001, _SweepBounds.y);
                float radialGrad = saturate(dist / radius);
                return saturate(radialGrad * 0.65 + combined * 0.4);
            }

            return combined;
        }

        struct DetailedEdge
        {
            float edgeFactor;
            float ashFactor;
            float3 emission;
        };

        DetailedEdge EvaluateEdge(float fieldValue, float progress, float3 positionWS)
        {
            DetailedEdge res;
            float diff = fieldValue - progress;
            clip(diff);

            float edge = saturate(1.0 - (diff / max(0.0001, _EdgeWidth)));
            float core = saturate(1.0 - (diff / max(0.0001, _CoreWidth)));
            float ash = saturate(1.0 - ((diff - _EdgeWidth) / max(0.0001, _AshWidth)));

            float pulse = 1.0 + 0.15 * sin(_Time.y * _PulseSpeed * 3.14159 + fieldValue * 30.0);
            float spark = pow(saturate(SimplexNoise(positionWS * 40.0 + float3(0, _Time.y * 12.0, 0))), 3.2) * _SparkleIntensity;

            float3 edgeEmission = 0;
            if ( _UseRampMap > 0.5 )
            {
                float rampU = saturate(1.0 - edge);
                float4 rampCol = SAMPLE_TEXTURE2D_LOD(_RampMap, sampler_RampMap, float2(rampU, 0.5), 0);
                edgeEmission = rampCol.rgb * _RampScale * edge * pulse;
            }
            else
            {
                float3 outerCol = _EdgeColor.rgb * _EdgeIntensity;
                float3 coreCol = _CoreColor.rgb * (_EdgeIntensity * 2.0);
                edgeEmission = (lerp(outerCol, coreCol, core) * edge * pulse) + (coreCol * spark * edge);
            }

            res.edgeFactor = edge;
            res.ashFactor = ash * (1.0 - edge);
            res.emission = edgeEmission;
            return res;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _SHADOWS_SOFT

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float4 tangentWS    : TEXCOORD3;
                float2 uv           : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 worldPosEstimate = TransformObjectToWorld(input.positionOS.xyz);
                float fieldEstimate = CalculateCutout(worldPosEstimate, input.positionOS.xyz, input.uv);
                float diffEstimate = fieldEstimate - _DissolveAmount;
                float edgeEstimate = saturate(1.0 - (diffEstimate / max(0.0001, _EdgeWidth)));

                float jitter = SimplexNoise(worldPosEstimate * 14.0 + float3(0, _Time.y * 15.0, 0));
                float3 displacedPosOS = input.positionOS.xyz + input.normalOS * (edgeEstimate * _DisplacementAmount * jitter);

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedPosOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = normInputs.normalWS;
                output.tangentWS = float4(normInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float fieldValue = CalculateCutout(input.positionWS, input.positionOS, input.uv);
                DetailedEdge dEdge = EvaluateEdge(fieldValue, _DissolveAmount, input.positionWS);

                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 albedo = baseTex.rgb * _BaseColor.rgb;
                albedo = lerp(albedo, _AshColor.rgb, dEdge.ashFactor);

                half3 normalWS = normalize(input.normalWS);
                if ( _BumpScale > 0.01 )
                {
                    half4 bumpMap = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv);
                    half3 normalTS = UnpackNormalScale(bumpMap, _BumpScale);
                    half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
                    normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
                }

                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half NdotL = saturate(dot(normalWS, mainLight.direction));
                half3 directLighting = mainLight.color * (NdotL * mainLight.shadowAttenuation);

                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half specPower = exp2(10.0 * _Smoothness + 1.0);
                half3 specular = pow(NdotH, specPower) * _Smoothness * mainLight.color * mainLight.shadowAttenuation * lerp(0.04, albedo, _Metallic);

                half rim = 1.0 - saturate(dot(normalWS, viewDirWS));
                half3 rimGlow = pow(rim, 3.5) * _EdgeColor.rgb * (dEdge.edgeFactor * 1.5);

                half3 ambient = SampleSH(normalWS) * albedo;
                half3 finalColor = albedo * directLighting + specular + ambient + rimGlow;

                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < pixelLightCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    half3 addLighting = addLight.color * (addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation);
                    finalColor += albedo * addLighting;
                }
                #endif

                finalColor += dEdge.emission;
                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float2 uv           : TEXCOORD2;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 worldPosEstimate = TransformObjectToWorld(input.positionOS.xyz);
                float fieldEstimate = CalculateCutout(worldPosEstimate, input.positionOS.xyz, input.uv);
                float diffEstimate = fieldEstimate - _DissolveAmount;
                float edgeEstimate = saturate(1.0 - (diffEstimate / max(0.0001, _EdgeWidth)));

                float jitter = SimplexNoise(worldPosEstimate * 14.0 + float3(0, _Time.y * 15.0, 0));
                float3 displacedPosOS = input.positionOS.xyz + input.normalOS * (edgeEstimate * _DisplacementAmount * jitter);

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedPosOS);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = ComputeShadowPositionHClip(posInputs.positionWS, normInputs.normalWS);
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                float fieldValue = CalculateCutout(input.positionWS, input.positionOS, input.uv);
                clip(fieldValue - _DissolveAmount);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float2 uv           : TEXCOORD2;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                float3 worldPosEstimate = TransformObjectToWorld(input.positionOS.xyz);
                float fieldEstimate = CalculateCutout(worldPosEstimate, input.positionOS.xyz, input.uv);
                float diffEstimate = fieldEstimate - _DissolveAmount;
                float edgeEstimate = saturate(1.0 - (diffEstimate / max(0.0001, _EdgeWidth)));

                float jitter = SimplexNoise(worldPosEstimate * 14.0 + float3(0, _Time.y * 15.0, 0));
                float3 displacedPosOS = input.positionOS.xyz + input.normalOS * (edgeEstimate * _DisplacementAmount * jitter);

                VertexPositionInputs posInputs = GetVertexPositionInputs(displacedPosOS);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                float fieldValue = CalculateCutout(input.positionWS, input.positionOS, input.uv);
                clip(fieldValue - _DissolveAmount);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}