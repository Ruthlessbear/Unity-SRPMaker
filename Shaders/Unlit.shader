// 这定义一个与自定义可编程渲染管线兼容的简单无光照 Shader 对象。
// 它应用硬编码颜色，并演示 LightMode 通道标签的使用。
// 它不与 SRP Batcher 兼容。

Shader "Examples/SimpleUnlitColor"
{
    Properties
    {
        _Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
    }
    SubShader
    {
        Pass
        {
            // LightMode 通道标签的值必须与 ScriptableRenderContext.DrawRenderers 中的 ShaderTagId 匹配
            Tags { "LightMode" = "ExampleLightModeTag"}

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)

            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(PerInstancing)
                UNITY_DEFINE_INSTANCED_PROP(float4, _Color)
            UNITY_INSTANCING_BUFFER_END(PerInstancing)

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                float4 worldPos = mul(UNITY_MATRIX_M, IN.positionOS);
                OUT.positionCS = mul(unity_MatrixVP, worldPos);
                return OUT;
            }

            float4 frag (Varyings IN) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float4 result_color = UNITY_ACCESS_INSTANCED_PROP(PerInstancing, _Color);
                return result_color;
            }
            ENDHLSL
        }
    }
}
