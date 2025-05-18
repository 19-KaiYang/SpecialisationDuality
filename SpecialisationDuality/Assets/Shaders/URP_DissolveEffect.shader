Shader "Custom/URP_DissolveEffect"
{
    Properties
    {
        _BaseMap("Base Map (RGB)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _Smoothness("Smoothness", Range(0,1)) = 0.5
        _Metallic("Metallic", Range(0,1)) = 0.0
        
        // Dissolve properties
        _Dissolve("Dissolve", Range(0,1)) = 0.0
        _DissolveTexture("Dissolve Texture", 2D) = "white" {}
        _EdgeColor("Edge Color", Color) = (1,1,1,1)
        _EdgeWidth("Edge Width", Range(0,0.1)) = 0.01
        
        // Add this property to control whether to use original color
        [Toggle] _PreserveOriginalColor("Preserve Original Color", Float) = 1
    }
    
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            // Include necessary URP libraries
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
                float3 positionWS : TEXCOORD2;
            };
            
            // Texture samplers
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DissolveTexture);
            SAMPLER(sampler_DissolveTexture);
            
            // Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _DissolveTexture_ST;
                half4 _BaseColor;
                half _Smoothness;
                half _Metallic;
                half _Dissolve;
                half4 _EdgeColor;
                half _EdgeWidth;
                half _PreserveOriginalColor;
            CBUFFER_END
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                // Standard transformations
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);
                
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                
                // UV coordinates
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Sample the dissolve texture
                float2 dissolveUV = TRANSFORM_TEX(input.uv, _DissolveTexture);
                half dissolveValue = SAMPLE_TEXTURE2D(_DissolveTexture, sampler_DissolveTexture, dissolveUV).r;
                
                // Apply dissolve clip
                clip(dissolveValue - _Dissolve);
                
                // Calculate edge effect
                half edge = 1 - smoothstep(_Dissolve, _Dissolve + _EdgeWidth, dissolveValue);
                
                // Sample the base texture
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                
                // Apply color only if not preserving original
                if (_PreserveOriginalColor < 0.5) {
                    baseColor *= _BaseColor;
                }
                
                // Apply edge color
                half3 finalColor = lerp(baseColor.rgb, _EdgeColor.rgb, edge);
                
                // Setup Lighting Data
                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = normalize(input.normalWS);
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord = float4(0, 0, 0, 0); // Calculate shadows if needed
                
                // Setup Surface Data
                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = finalColor;
                surfaceData.metallic = _Metallic;
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = float3(0, 0, 1);
                surfaceData.occlusion = 1;
                
                // Apply standard lighting
                half4 finalRGBA = UniversalFragmentPBR(lightingInput, surfaceData);
                
                return finalRGBA;
            }
            ENDHLSL
        }
        
        // Shadow casting pass
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            
            // Include necessary URP libraries
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            
            // Texture samplers
            TEXTURE2D(_DissolveTexture);
            SAMPLER(sampler_DissolveTexture);
            
            // Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _DissolveTexture_ST;
                half _Dissolve;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };
            
            float3 _LightDirection;
            
            float4 GetShadowPositionHClip(Attributes input)
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                
                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                return positionCS;
            }
            
            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                output.positionCS = GetShadowPositionHClip(input);
                output.uv = TRANSFORM_TEX(input.texcoord, _DissolveTexture);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                // Sample the dissolve texture for clipping
                half dissolveValue = SAMPLE_TEXTURE2D(_DissolveTexture, sampler_DissolveTexture, input.uv).r;
                clip(dissolveValue - _Dissolve);
                return 0;
            }
            ENDHLSL
        }
    }
}