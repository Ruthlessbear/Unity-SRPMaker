#ifndef CUSTOM_SHADOWMAP_INCLUDE
#define CUSTOM_SHADOWMAP_INCLUDE

struct Attributes
{
    float4 positionOS : POSITION;
    float2 baseUV : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 baseUV : TEXCOORD0;
};

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

Varyings ShadowVert(Attributes IN)
{
    Varyings OUT;
    float4 worldPos = mul(UNITY_MATRIX_M, IN.positionOS);
    OUT.positionCS = mul(unity_MatrixVP, worldPos);
#if UNITY_REVERSED_Z
                    OUT.positionCS.z = min(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    OUT.positionCS.z = max(OUT.positionCS.z, OUT.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif

    OUT.baseUV = IN.baseUV;
    return OUT;
}

float4 ShadowFrag(Varyings IN) : SV_TARGET
{
    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.baseUV);
                
    //temp
    return float4(1.0f, 1.0f, 1.0f, 1.0f);
}

#endif