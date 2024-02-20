/*******************************************************************/
/*****************Custom RenderPipeline Base Lit********************/

Shader "Examples/Lit"
{
    Properties
    {
        _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        [KeywordEnum(On, Clip, Dither, Off)]_Shadows("Shadow", float) = 0
    }
    SubShader
    {
        Pass
        {
            Tags { "LightMode" = "ShadowCaster"}

            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/PipelineMaker/ShaderLibrary/ShadowMap.hlsl"

            ENDHLSL
        }

        Pass
        {
            
            Tags { "LightMode" = "ExampleLightModeTag"}

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _LIGHTS_PER_OBJECT
            #pragma shader_feature _ _SHADOWS_CLIP _SHADOWS_DITHER

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/PipelineMaker/ShaderLibrary/Light.hlsl"
            #include "Assets/PipelineMaker/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normal : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 DiffuseLight(int index, float3 positionWS, float3 normalWS, float shadow_attenuation)
            {
                float3 lightAdd = GetLithting(index, positionWS, normalWS);

                return lightAdd  * shadow_attenuation;
            }

            UNITY_INSTANCING_BUFFER_START(PerInstancing)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(PerInstancing)


            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float4 worldPos = mul(UNITY_MATRIX_M, IN.positionOS);
                OUT.positionWS = worldPos.xyz;
                OUT.positionCS = mul(unity_MatrixVP, worldPos);
                OUT.normal = mul((float3x3)UNITY_MATRIX_M, IN.normal);
                return OUT;
            }

            float4 frag (Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                //#if defined(_SHADOWS_CLIP)
                //    clip(UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
                //#endif

                IN.normal = normalize(IN.normal);
                float3 diffuseLight = 0;
                
#ifdef _LIGHTS_PER_OBJECT
                for(int i = 0; i < unity_LightData.y; i++)
                {
                    int index = unity_LightIndices[i / 4][i % 4];
                    float shadow = GetDirectionalShadowAttenuation(IN.positionCS, IN.positionWS, IN.normal, index);
                    diffuseLight += DiffuseLight(index, IN.positionWS, IN.normal, shadow);
                }
#else
                for(int i = 0; i < MAX_VISIBLE_LIGHTS; i++)
                {
                    //float shadow_normal = GetDirectionalShadowAttenuation(IN.positionWS, IN.normal, i);
                    diffuseLight += DiffuseLight(i, IN.positionWS, IN.normal, 1.0f);
                }
#endif
                float3 albedo = UNITY_ACCESS_INSTANCED_PROP(PerInstancing, _Color).rgb;
                float3 color = diffuseLight * albedo;
                return float4(color, 1.0);
            }
            ENDHLSL
        }

        
    }
}
