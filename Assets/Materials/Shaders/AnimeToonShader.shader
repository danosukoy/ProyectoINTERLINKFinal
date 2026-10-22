Shader "Custom/AnimeToonShader"
{
    Properties
    {
        [Header(Base Properties)]
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Anime Cel Shading)]
        _ShadowColor ("Shadow Tint Color", Color) = (0.2, 0.22, 0.4, 1.0)
        _CelThreshold ("Cel Threshold", Range(0.0, 1.0)) = 0.45
        _CelSmoothness ("Cel Smoothness", Range(0.001, 0.2)) = 0.03

        [Header(Anime Specular Highlight)]
        [Toggle(_SPECULAR_ON)] _EnableSpecular ("Enable Specular", Float) = 1
        _SpecularColor ("Specular Color", Color) = (1, 1, 1, 1)
        _SpecularSize ("Specular Size", Range(0.001, 0.5)) = 0.08
        _SpecularSmoothness ("Specular Smoothness", Range(0.001, 0.2)) = 0.02

        [Header(Rim Light Glow)]
        [Toggle(_RIM_ON)] _EnableRim ("Enable Rim Light", Float) = 1
        _RimColor ("Rim Color", Color) = (0.4, 0.85, 1.0, 1.0)
        _RimPower ("Rim Power", Range(0.1, 10.0)) = 3.5
        _RimThreshold ("Rim Threshold", Range(0.0, 1.0)) = 0.55

        [Header(Professional Anime Outline)]
        [Toggle(_OUTLINE_ON)] _EnableOutline ("Enable Outline Pass", Float) = 1
        _OutlineColor ("Outline Color", Color) = (0.1, 0.08, 0.18, 1.0)
        _OutlineColorBlend ("Blend Base Color into Outline", Range(0.0, 1.0)) = 0.4
        _OutlineWidth ("Outline Width (Screen Const)", Range(0.0, 0.03)) = 0.004
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
        // PASS 1: PROFESSIONAL INVERTED HULL OUTLINE PASS
        // -------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature _OUTLINE_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
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
                float4 _ShadowColor;
                float _CelThreshold;
                float _CelSmoothness;
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
                float _OutlineColorBlend;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);

                #if defined(_OUTLINE_ON)
                    // Transform position to Clip Space
                    float4 clipPos = TransformObjectToHClip(input.positionOS.xyz);

                    // Transform normals to Clip Space for screen-constant expansion
                    float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                    float3 normCS = TransformWorldToHClipDir(normWS);

                    // Vertex Color Alpha controls outline thickness mask (0 = no outline, 1 = full outline)
                    float thicknessMask = input.color.a > 0.001 ? input.color.a : 1.0;

                    // Distance-independent Clip Space vertex expansion
                    clipPos.xy += normalize(normCS.xy) * _OutlineWidth * clipPos.w * thicknessMask;
                    output.positionCS = clipPos;
                #else
                    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #endif

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                #if defined(_OUTLINE_ON)
                    float4 baseTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                    float3 characterBase = baseTex.rgb * _BaseColor.rgb;

                    // Blend pure outline color with character base color for anime aesthetics
                    float3 finalOutlineColor = lerp(_OutlineColor.rgb, _OutlineColor.rgb * characterBase, _OutlineColorBlend);
                    return float4(finalOutlineColor, _BaseColor.a);
                #else
                    discard;
                    return float4(0,0,0,0);
                #endif
            }
            ENDHLSL
        }

        // -------------------------------------------------------------
        // PASS 2: MAIN ANIME CEL LIGHTING PASS
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
            #pragma shader_feature _SPECULAR_ON
            #pragma shader_feature _RIM_ON

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
                float _CelThreshold;
                float _CelSmoothness;
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
                float _OutlineColorBlend;
                float _OutlineWidth;
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

                // Get Main Directional Light
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float3 L = normalize(mainLight.direction);
                float shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                // Half-Lambert Light Intensity
                float NdotL = dot(N, L);
                float lightIntensity = (NdotL * 0.5 + 0.5) * shadowAtten;

                // Cel Shading Ramp Threshold
                float celRamp = smoothstep(_CelThreshold - _CelSmoothness, _CelThreshold + _CelSmoothness, lightIntensity);

                // Blend Base Albedo with Stylized Shadow Color
                float3 litColor = lerp(_ShadowColor.rgb * albedo, albedo * mainLight.color, celRamp);

                // Anime Specular Highlight
                #if defined(_SPECULAR_ON)
                    float3 H = normalize(L + V);
                    float NdotH = max(0.0, dot(N, H));
                    float specIntensity = pow(NdotH, 1.0 / (_SpecularSize + 0.001));
                    float specRamp = smoothstep(0.5 - _SpecularSmoothness, 0.5 + _SpecularSmoothness, specIntensity);
                    litColor += _SpecularColor.rgb * specRamp * celRamp * mainLight.color;
                #endif

                // Rim Light Glow
                #if defined(_RIM_ON)
                    float NdotV = max(0.0, dot(N, V));
                    float rim = pow(1.0 - NdotV, _RimPower);
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
