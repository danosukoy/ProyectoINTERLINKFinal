Shader "Custom/AnimeToonGenshinShader"
{
    Properties
    {
        [Header(Base Color and Texture)]
        _BaseMap ("Base Texture (RGB)", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)

        [Header(Shadow Toggles)]
        [Toggle(_RECEIVE_SHADOWS_OFF)] _DisableRealtimeShadows ("Disable Realtime Cast Shadows", Float) = 0
        [Toggle(_CEL_SHADOWS_OFF)] _DisableCelShadows ("Disable Cel Self-Shading (No Dark Sides)", Float) = 0

        [Header(Genshin Wuthering Toon Ramp)]
        [Toggle(_USE_RAMP_MAP)] _UseRampMap ("Use Custom Ramp Texture", Float) = 0
        _RampMap ("Toon Ramp Texture", 2D) = "white" {}
        
        _ShadowColor ("Shadow Color Tint", Color) = (0.35, 0.38, 0.55, 1.0)
        _MidtoneColor ("Midtone Warmth Color", Color) = (0.85, 0.75, 0.8, 1.0)
        _ShadowThreshold ("Shadow Cutoff Threshold", Range(0.0, 1.0)) = 0.5
        _ShadowFeather ("Shadow Softness / Feathering", Range(0.001, 0.5)) = 0.08
        _MidtoneWidth ("Midtone Warmth Band Width", Range(0.0, 0.3)) = 0.05

        [Header(Anime Specular Highlight)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular Highlight", Float) = 1
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularPower ("Specular Smoothness", Range(1.0, 128.0)) = 32.0
        _SpecularSize ("Specular Threshold Size", Range(0.01, 1.0)) = 0.15
        _SpecularFeather ("Specular Softness", Range(0.001, 0.3)) = 0.05

        [Header(Anime Fresnel Rim Light)]
        [Toggle(_RIM_ON)] _EnableRim ("Enable Anime Rim Light", Float) = 1
        _RimColor ("Rim Light Color", Color) = (0.7, 0.85, 1.0, 1.0)
        _RimPower ("Rim Falloff Exponent", Range(0.5, 10.0)) = 3.0
        _RimThreshold ("Rim Cutoff Threshold", Range(0.0, 1.0)) = 0.5
        _RimFeather ("Rim Softness", Range(0.001, 0.3)) = 0.1

        [Header(Anime Outline)]
        [Toggle(_OUTLINE_ON)] _EnableOutline ("Enable Inverted Hull Outline", Float) = 1
        _OutlineColor ("Outline Tint Color", Color) = (0.15, 0.12, 0.2, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.03)) = 0.003
        [Toggle(_OUTLINE_BLEND)] _BlendOutlineWithBase ("Blend Outline with Base Color", Float) = 1
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
        // PASS 1: ANIME OUTLINE PASS (Inverted Hull Vertex Extrusion)
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
            #pragma shader_feature_local _OUTLINE_BLEND

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

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
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _OutlineColor;
                float _OutlineWidth;
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
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                #if defined(_OUTLINE_ON)
                    float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    float3 baseCol = baseTex.rgb * _BaseColor.rgb;
                    #if defined(_OUTLINE_BLEND)
                        float3 finalOutline = baseCol * _OutlineColor.rgb * 0.6;
                    #else
                        float3 finalOutline = _OutlineColor.rgb;
                    #endif
                    return float4(finalOutline, _BaseColor.a * baseTex.a);
                #else
                    discard;
                    return float4(0,0,0,0);
                #endif
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // PASS 2: MAIN GENSHIN / WUTHERING STYLE TOON PASS
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
            #pragma shader_feature_local _USE_RAMP_MAP
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
            TEXTURE2D(_RampMap);
            SAMPLER(sampler_RampMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float4 _MidtoneColor;
                float _ShadowThreshold;
                float _ShadowFeather;
                float _MidtoneWidth;
                float _EnableSpecular;
                float4 _SpecularColor;
                float _SpecularPower;
                float _SpecularSize;
                float _SpecularFeather;
                float _EnableRim;
                float4 _RimColor;
                float _RimPower;
                float _RimThreshold;
                float _RimFeather;
                float _EnableOutline;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _DisableRealtimeShadows;
                float _DisableCelShadows;
                float _UseRampMap;
                float _BlendOutlineWithBase;
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

                // Half-Lambert Light Intensity (0..1)
                float NdotL = dot(N, L);
                float halfLambert = NdotL * 0.5 + 0.5;
                float lightIntensity = halfLambert * shadowAtten;

                float3 toonShading = albedo;

                #if defined(_CEL_SHADOWS_OFF)
                    toonShading = albedo * mainLight.color;
                #else
                    #if defined(_USE_RAMP_MAP)
                        float2 rampUV = float2(lightIntensity, 0.5);
                        float3 rampColor = SAMPLE_TEXTURE2D(_RampMap, sampler_RampMap, rampUV).rgb;
                        toonShading = albedo * rampColor * mainLight.color;
                    #else
                        // Genshin / Wuthering multi-band ramp: Shadow -> Midtone Warmth -> Full Light
                        float celFactor = smoothstep(_ShadowThreshold - _ShadowFeather, _ShadowThreshold + _ShadowFeather, lightIntensity);
                        
                        // Midtone warmth transition band
                        float midtoneFactor = smoothstep(_ShadowThreshold - _MidtoneWidth - _ShadowFeather, _ShadowThreshold, lightIntensity) 
                                            - smoothstep(_ShadowThreshold, _ShadowThreshold + _MidtoneWidth + _ShadowFeather, lightIntensity);
                        midtoneFactor = saturate(midtoneFactor);

                        float3 shadowColorBlended = lerp(_ShadowColor.rgb * albedo, albedo * _MidtoneColor.rgb, midtoneFactor);
                        toonShading = lerp(shadowColorBlended, albedo * mainLight.color, celFactor);
                    #endif
                #endif

                float3 finalColor = toonShading;

                // Anime Specular Highlight
                #if defined(_SPECULAR_ON)
                    float3 H = normalize(L + V);
                    float NdotH = max(0.0, dot(N, H));
                    float specIntensity = pow(NdotH, _SpecularPower);
                    float specRamp = smoothstep(_SpecularSize - _SpecularFeather, _SpecularSize + _SpecularFeather, specIntensity);
                    finalColor += _SpecularColor.rgb * specRamp * mainLight.color;
                #endif

                // Anime Rim Light (Fresnel Backlight)
                #if defined(_RIM_ON)
                    float NdotV = max(0.0, dot(N, V));
                    float rimTerm = pow(1.0 - NdotV, _RimPower);
                    float rimRamp = smoothstep(_RimThreshold - _RimFeather, _RimThreshold + _RimFeather, rimTerm);
                    finalColor += _RimColor.rgb * rimRamp * mainLight.color;
                #endif

                return float4(finalColor, _BaseColor.a * baseTex.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
