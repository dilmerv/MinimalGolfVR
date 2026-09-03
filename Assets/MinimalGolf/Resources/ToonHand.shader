Shader "MinimalGolf/ToonHand"
{
    Properties
    {
        _BaseColor ("Base (lit)", Color) = (0.957, 0.961, 0.969, 1)
        _ShadeColor ("Shade", Color) = (0.788, 0.804, 0.831, 1)
        _OutlineColor ("Outline", Color) = (0.137, 0.149, 0.169, 1)
        _OutlineWidth ("Outline Width (object space)", Range(0.0, 0.01)) = 0.0025
        _StepThreshold ("Shade Threshold", Range(0.0, 1.0)) = 0.5
        _StepSmooth ("Shade Softness", Range(0.0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        // Inverted-hull outline: front faces culled, shell pushed along normals.
        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadeColor;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _StepThreshold;
                float _StepSmooth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 expanded = input.positionOS.xyz + normalize(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformObjectToHClip(expanded);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return half4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }

        // Two-tone stepped toon driven by the main light + spherical-harmonics ambient.
        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex ToonVertex
            #pragma fragment ToonFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShadeColor;
                float4 _OutlineColor;
                float _OutlineWidth;
                float _StepThreshold;
                float _StepSmooth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                return output;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                half ndl = dot(normalWS, mainLight.direction) * 0.5 + 0.5;
                half band = smoothstep(_StepThreshold - _StepSmooth, _StepThreshold + _StepSmooth, ndl);
                half3 albedo = lerp(_ShadeColor.rgb, _BaseColor.rgb, band);
                half3 direct = albedo * mainLight.color * lerp(0.55, 1.05, band);
                half3 ambient = SampleSH(normalWS) * albedo;
                return half4(direct + ambient, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Forward"
}
