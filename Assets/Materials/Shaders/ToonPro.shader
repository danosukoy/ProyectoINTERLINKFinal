Shader "Custom/ToonPro"
{
    Properties
    {
        [Header(Base)]
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        [Toggle(_ALPHACLIP_ON)] _AlphaClip ("Alpha Clip", Float) = 0
        _Cutoff ("Cutoff", Range(0,1)) = 0.5

        [Header(Ramp Cel Shading)]
        [Toggle(_USE_RAMP_MAP)] _UseRampMap ("Use Ramp Map", Float) = 0
        _RampMap ("Ramp (1D)", 2D) = "white" {}
        _ShadowColor ("Shadow Color", Color) = (0.22, 0.25, 0.45, 1)
        _MidtoneColor ("Midtone Color", Color) = (0.85, 0.78, 0.85, 1)
        _ShadowThreshold ("Shadow Threshold", Range(0,1)) = 0.5
        _ShadowFeather ("Shadow Feather", Range(0.001,0.5)) = 0.06
        _MidtoneWidth ("Midtone Width", Range(0,0.4)) = 0.06
        [Toggle(_CEL_SHADOWS_OFF)] _DisableCelShadows ("Disable Cel Shading", Float) = 0
        [Toggle(_RECEIVE_SHADOWS_OFF)] _DisableReceiveShadows ("Disable Receive Shadows", Float) = 0

        [Header(Specular)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular", Float) = 1
        _SpecularColor ("Specular Color", Color) = (1,1,1,1)
        _SpecularPower ("Specular Power", Range(1,256)) = 64
        _SpecularSize ("Specular Size", Range(0,1)) = 0.15
        _SpecularFeather ("Specular Feather", Range(0.001,0.3)) = 0.05

        [Header(Rim)]
        [Toggle(_RIM_ON)] _EnableRim ("Enable Rim", Float) = 1
        _RimColor ("Rim Color", Color) = (0.4, 0.85, 1, 1)
        _RimPower ("Rim Power", Range(0.5,10)) = 3.5
        _RimThreshold ("Rim Threshold", Range(0,1)) = 0.5
        _RimFeather ("Rim Feather", Range(0.001,0.3)) = 0.08

        [Header(Outline)]
        [Toggle(_OUTLINE_ON)] _EnableOutline ("Enable Outline", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.08, 0.07, 0.15, 1)
        _OutlineWidth ("Outline Width (screen)", Range(0,0.02)) = 0.004
        _OutlineBlend ("Blend BaseColor", Range(0,1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" "UniversalMaterialType"="Lit" }

        // OUTLINE - Inverted Hull clip-space, screen-constant, vertexColor.a mask
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OUTLINE_ON
            #pragma shader_feature_local _ALPHACLIP_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float4 _OutlineColor; float _OutlineWidth; float _OutlineBlend; float _Cutoff;
            CBUFFER_END
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                #if defined(_OUTLINE_ON)
                    float4 clipPos = TransformObjectToHClip(IN.positionOS.xyz);
                    float3 normWS = TransformObjectToWorldNormal(IN.normalOS);
                    float3 normCS = TransformWorldToHClipDir(normWS);
                    float mask = IN.color.a > 0.001 ? IN.color.a : 1.0;
                    clipPos.xy += normalize(normCS.xy) * _OutlineWidth * clipPos.w * mask;
                    OUT.positionCS = clipPos;
                #else
                    OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                #endif
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                #if !defined(_OUTLINE_ON)
                    discard; return 0;
                #endif
                #if defined(_ALPHACLIP_ON)
                    half alpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                    clip(alpha - _Cutoff);
                #endif
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half3 baseCol = baseTex.rgb * _BaseColor.rgb;
                half3 outCol = lerp(_OutlineColor.rgb, _OutlineColor.rgb * baseCol, _OutlineBlend);
                return half4(outCol, 1);
            }
            ENDHLSL
        }

        // FORWARD - Cel + Spec + Rim + RampMap, SRP Batcher compatible
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _ALPHACLIP_ON
            #pragma shader_feature_local _USE_RAMP_MAP
            #pragma shader_feature_local _CEL_SHADOWS_OFF
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _SPECULAR_ON
            #pragma shader_feature_local _RIM_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 viewDirWS : TEXCOORD2; float3 positionWS : TEXCOORD3; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_RampMap); SAMPLER(sampler_RampMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; float4 _BaseColor; float _Cutoff;
                float4 _ShadowColor; float4 _MidtoneColor; float _ShadowThreshold; float _ShadowFeather; float _MidtoneWidth;
                float4 _SpecularColor; float _SpecularPower; float _SpecularSize; float _SpecularFeather;
                float4 _RimColor; float _RimPower; float _RimThreshold; float _RimFeather;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs p = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(IN.normalOS);
                OUT.positionCS = p.positionCS;
                OUT.positionWS = p.positionWS;
                OUT.normalWS = n.normalWS;
                OUT.viewDirWS = GetWorldSpaceNormalizeViewDir(p.positionWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half alpha = baseTex.a * _BaseColor.a;
                #if defined(_ALPHACLIP_ON)
                    clip(alpha - _Cutoff);
                #endif
                half3 albedo = baseTex.rgb * _BaseColor.rgb;

                half3 N = normalize(IN.normalWS);
                half3 V = normalize(IN.viewDirWS);

                // Main light
                #if defined(_RECEIVE_SHADOWS_OFF)
                    Light mainLight = GetMainLight();
                    half shadowAtten = mainLight.distanceAttenuation;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                    Light mainLight = GetMainLight(shadowCoord);
                    half shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                #endif
                half3 L = normalize(mainLight.direction);
                half NdotL = dot(N, L);
                half halfLambert = NdotL * 0.5 + 0.5;
                half lightIntensity = halfLambert * shadowAtten;

                half3 cel = albedo;
                #if !defined(_CEL_SHADOWS_OFF)
                    #if defined(_USE_RAMP_MAP)
                        half3 ramp = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, half2(lightIntensity, 0.5)).rgb;
                        cel = albedo * ramp * mainLight.color;
                    #else
                        half celFactor = smoothstep(_ShadowThreshold - _ShadowFeather, _ShadowThreshold + _ShadowFeather, lightIntensity);
                        half midFactor = smoothstep(_ShadowThreshold - _MidtoneWidth - _ShadowFeather, _ShadowThreshold, lightIntensity)
                                       - smoothstep(_ShadowThreshold, _ShadowThreshold + _MidtoneWidth + _ShadowFeather, lightIntensity);
                        midFactor = saturate(midFactor);
                        half3 shadowBlend = lerp(_ShadowColor.rgb * albedo, albedo * _MidtoneColor.rgb, midFactor);
                        cel = lerp(shadowBlend, albedo * mainLight.color, celFactor);
                    #endif
                #else
                    cel = albedo * mainLight.color;
                #endif

                half3 col = cel;

                #if defined(_SPECULAR_ON)
                    half3 H = normalize(L + V);
                    half spec = pow(saturate(dot(N, H)), _SpecularPower);
                    half specRamp = smoothstep(_SpecularSize - _SpecularFeather, _SpecularSize + _SpecularFeather, spec);
                    col += _SpecularColor.rgb * specRamp * mainLight.color;
                #endif

                #if defined(_RIM_ON)
                    half rim = pow(saturate(1.0 - dot(N, V)), _RimPower);
                    half rimRamp = smoothstep(_RimThreshold - _RimFeather, _RimThreshold + _RimFeather, rim);
                    // mask by cel so rim not in shadow
                    half rimMask = 1;
                    #if !defined(_CEL_SHADOWS_OFF)
                        #if !defined(_USE_RAMP_MAP)
                            rimMask = smoothstep(_ShadowThreshold - _ShadowFeather, _ShadowThreshold + _ShadowFeather, lightIntensity);
                        #endif
                    #endif
                    col += _RimColor.rgb * rimRamp * rimMask * mainLight.color;
                #endif

                // Additional lights (toon: single intensity, no ramp per light for perf)
                #if defined(_ADDITIONAL_LIGHTS)
                    uint count = GetAdditionalLightsCount();
                    for (uint i = 0; i < count; ++i)
                    {
                        Light add = GetAdditionalLight(i, IN.positionWS);
                        half aNdotL = saturate(dot(N, normalize(add.direction)));
                        half aHalf = aNdotL * 0.5 + 0.5;
                        half atten = add.distanceAttenuation * add.shadowAttenuation;
                        half aIntensity = aHalf * atten;
                        half aCel = smoothstep(_ShadowThreshold - _ShadowFeather, _ShadowThreshold + _ShadowFeather, aIntensity);
                        col += albedo * add.color * aCel * 0.5;
                    }
                #endif

                return half4(col, alpha);
            }
            ENDHLSL
        }

        // ShadowCaster - needed for casting
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHACLIP_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; float4 _BaseColor; float _Cutoff; CBUFFER_END
            float3 _LightDirection; float3 _LightPosition;
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionCS = TransformWorldToHClip(ApplyShadowBias(posWS, nWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    OUT.positionCS.z = max(OUT.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return OUT;
            }
            half4 frag(Varyings IN) : SV_Target
            {
                #if defined(_ALPHACLIP_ON)
                    half a = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).a * _BaseColor.a;
                    clip(a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack Off
}
