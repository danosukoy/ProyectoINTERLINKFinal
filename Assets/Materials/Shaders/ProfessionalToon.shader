Shader "Custom/ProfessionalToonShader"
{
    Properties
    {
        [Header(Base Properties)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Shadow Options)]
        [Toggle(_RECEIVE_SHADOWS_OFF)] _DisableRealtimeShadows ("Disable Realtime Cast Shadows", Float) = 0
        [Toggle(_CEL_SHADOWS_OFF)] _DisableCelShadows ("Disable Cel Self-Shading (No Dark Sides)", Float) = 0

        [Header(Toon Lighting)]
        _ShadowColor ("Shadow Tint Color", Color) = (0.2, 0.25, 0.45, 1.0)
        _CelRampThreshold ("Cel Threshold", Range(0.0, 1.0)) = 0.45
        _CelRampSmoothness ("Cel Smoothness", Range(0.001, 1.0)) = 0.04

        [Header(Specular Highlight)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular", Float) = 1
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize ("Specular Size", Range(0.001, 0.5)) = 0.08
        _SpecularSmoothness ("Specular Smoothness", Range(0.001, 1.0)) = 0.02

        [Header(Rim Lighting)]
        [Toggle(_RIM_ON)] _EnableRim ("Enable Rim Light", Float) = 1
        _RimColor ("Rim Color", Color) = (0.3, 0.85, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.1, 10.0)) = 3.5
        _RimThreshold ("Rim Threshold", Range(0.0, 1.0)) = 0.55

        [Header(Outline)]
        [Toggle(_OUTLINE_ON)] _EnableOutline ("Enable Outline Pass", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.08, 0.08, 0.15, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.05)) = 0.006
    }

    SubShader
    {
        Tags 
        { 
            "RenderType"="Opaque" 
            "RenderPipeline"="UniversalPipeline" 
            "Queue"="Geometry" 
        }

        // -------------------------------------------------------------
        // PASS 1: OUTLINE PASS (Inverted Hull Vertex Extrusion)
        // -------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _OUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadowColor;
                float _CelRampThreshold;
                float _CelRampSmoothness;
                float _EnableSpecular;
                float4 _SpecularColor;
                float _SpecularSize;
                float _SpecularSmoothness;
                float _EnableRim;
                float4 _RimColor;
                float _RimPower;
                float _RimThreshold;
                float _EnableOutline;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _DisableRealtimeShadows;
                float _DisableCelShadows;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                #if defined(_OUTLINE_ON)
                    float3 normOS = normalize(input.normalOS);
                    float3 posOS = input.positionOS.xyz + normOS * _OutlineWidth;
                    output.positionCS = TransformObjectToHClip(posOS);
                #else
                    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #endif
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                #if defined(_OUTLINE_ON)
                    return _OutlineColor;
                #else
                    discard;
                    return float4(0,0,0,0);
                #endif
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // PASS 2: MAIN TOON LIGHTING PASS
        // -------------------------------------------------------------
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _CEL_SHADOWS_OFF
            #pragma shader_feature_local _SPECULAR_ON
            #pragma shader_feature_local _RIM_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float _CelRampThreshold;
                float _CelRampSmoothness;
                float _EnableSpecular;
                float4 _SpecularColor;
                float _SpecularSize;
                float _SpecularSmoothness;
                float _EnableRim;
                float4 _RimColor;
                float _RimPower;
                float _RimThreshold;
                float _EnableOutline;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _DisableRealtimeShadows;
                float _DisableCelShadows;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.viewDirWS = GetWorldSpaceNormalizeViewDir(posInputs.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 albedo = baseTex.rgb * _BaseColor.rgb;

                float3 N = normalize(input.normalWS);
                float3 V = normalize(input.viewDirWS);

                // Get Main Directional Light & Shadow Attenuation
                #if defined(_RECEIVE_SHADOWS_OFF)
                    Light mainLight = GetMainLight();
                    float shadowAtten = mainLight.distanceAttenuation;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                    Light mainLight = GetMainLight(shadowCoord);
                    float shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                #endif

                float3 L = normalize(mainLight.direction);

                // N dot L Lambertian
                float NdotL = dot(N, L);
                float lightIntensity = NdotL * 0.5 + 0.5; // Half-Lambert
                lightIntensity *= shadowAtten;

                // Cel Shading Ramp
                #if defined(_CEL_SHADOWS_OFF)
                    float celRamp = 1.0;
                #else
                    float celRamp = smoothstep(_CelRampThreshold - _CelRampSmoothness, _CelRampThreshold + _CelRampSmoothness, lightIntensity);
                #endif

                // Blend Albedo with Shadow Tint
                float3 litColor = lerp(_ShadowColor.rgb * albedo, albedo * mainLight.color, celRamp);

                // Toon Specular Highlight
                #if defined(_SPECULAR_ON)
                    float3 H = normalize(L + V);
                    float NdotH = max(0.0, dot(N, H));
                    float specIntensity = pow(NdotH, 1.0 / (_SpecularSize + 0.001));
                    float specRamp = smoothstep(0.5 - _SpecularSmoothness, 0.5 + _SpecularSmoothness, specIntensity);
                    litColor += _SpecularColor.rgb * specRamp * celRamp * mainLight.color;
                #endif

                // Toon Rim Light (Fresnel Glow)
                #if defined(_RIM_ON)
                    float NdotV = max(0.0, dot(N, V));
                    float rim = 1.0 - NdotV;
                    rim = pow(rim, _RimPower);
                    float rimRamp = smoothstep(_RimThreshold - 0.05, _RimThreshold + 0.05, rim);
                    litColor += _RimColor.rgb * rimRamp * celRamp;
                #endif

                return float4(litColor, _BaseColor.a * baseTex.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

