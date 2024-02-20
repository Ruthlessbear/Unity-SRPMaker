#ifndef CUSTOM_SHADOWS_INCLUDE
#define CUSTOM_SHADOWS_INCLUDE

#include "Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Shadow/ShadowSamplingTent.hlsl"

#define MAX_DIRECTIONAL_LIGHT_COUNT 4
#define MAX_CASCADE_COUNT 4

#pragma multi_compile _ _CASCADE_BLEND_SOFT _CASCADE_BLEND_DITHER
#pragma multi_compile _ _DIRECTIONAL_PCF3 _DIRECTIONAL_PCF5 _DIRECTIONAL_PCF7
#if defined(_DIRECTIONAL_PCF3)
#define DIRECTIONAL_FILTER_SAMPLES 4
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_3x3
#elif defined(_DIRECTIONAL_PCF5)
#define DIRECTIONAL_FILTER_SAMPLES 9
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_5x5
#elif defined(_DIRECTIONAL_PCF7)
#define DIRECTIONAL_FILTER_SAMPLES 16
#define DIRECTIONAL_FILTER_SETUP SampleShadow_ComputeSamples_Tent_7x7
#endif

TEXTURE2D_SHADOW(_DirectionalShadowAtlas);
#define SHADOW_SAMPLER sampler_linear_clamp_compare
SAMPLER_CMP(SHADOW_SAMPLER);
                
CBUFFER_START(_CustomShadows)
int _CascadeCount;
float4 _CascadeCullingSpheres[MAX_CASCADE_COUNT];
float4 _DirectionLightShadowData[MAX_DIRECTIONAL_LIGHT_COUNT];
float4x4 _DirectionalShadowMatrices[MAX_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];
float4 _ShadowDistance;
float4 _CascadeData[MAX_CASCADE_COUNT];
float4 _ShadowAtlasSize;
CBUFFER_END

struct DirectionalShadowData
{
    float strength;
    int titleIndex;
    float normalBias;
};

struct ShadowCascadeData
{
    int cascadeIndex;
    float strength;
    float cascadeBlend;
};

/**************************************************************/
/*********************Cascade Shadows**************************/
float FadeShadowStrength(float distance, float scale, float fade)
{
    return saturate((1 - distance * scale) * fade);
}

ShadowCascadeData GetShadowCascadeData(Surface surface)
{
    ShadowCascadeData data;
    data.cascadeBlend = 1.0f;
    float surface_depth = TransformWorldToView(surface.position).z;
    //Non cascading shadow
    data.strength = FadeShadowStrength(surface_depth, _ShadowDistance.x, _ShadowDistance.y);
    for (int i = 0; i < _CascadeCount; ++i)
    {
        float4 sphere = _CascadeCullingSpheres[i];
        float distanceSqr = DistanceSquared(sphere.xyz, surface.position);
        float fade = FadeShadowStrength(distanceSqr, _CascadeData[i].x, _ShadowDistance.z);
        if (distanceSqr < sphere.w)
        {
            if (i == _CascadeCount - 1)
            {
                //Cascading shadow transition
                data.strength *= fade;
            }
            else
            {
                data.cascadeBlend = fade;
            }
            break;
        }
    }

    if (i == MAX_CASCADE_COUNT)
    {
        data.strength = 0.0f;
    }
    
#if defined(_CASCADE_BLEND_DITHER)
    if(data.cascadeBlend < surface.dither)
        i += 1;
#endif
#if !defined(_CASCADE_BLEND_SOFT)
    data.cascadeBlend = 1.0f;
#endif

    data.cascadeIndex = i;
    return data;
}
/****************************END*******************************/



/**************************************************************/
/*********************Shadows Compute**************************/

///Sampler Shadow Map
float SampleDirectionalShadowAtlas(float3 positionST)
{
    return SAMPLE_TEXTURE2D_SHADOW(_DirectionalShadowAtlas, SHADOW_SAMPLER, positionST);
}

///PCF
float FilterDirectionalShadow(float3 positionSTS)
{
#if defined(DIRECTIONAL_FILTER_SETUP)
    //权重样本
    float weights[DIRECTIONAL_FILTER_SAMPLES];
    //样本位置
    float2 positions[DIRECTIONAL_FILTER_SAMPLES];
    float4 size = _ShadowAtlasSize.yyxx;
    DIRECTIONAL_FILTER_SETUP(size, positionSTS.xy, weights, positions);
    float shadow = 0.0f;
    for(int i = 0; i < DIRECTIONAL_FILTER_SAMPLES; i++)
    {
        shadow += weights[i] * SampleDirectionalShadowAtlas(float3(positions[i].xy, positionSTS.z));
    }

    return shadow;
#else
    return SampleDirectionalShadowAtlas(positionSTS);
#endif
}
/****************************END*******************************/



/**************************************************************/
/*********************Directional Shadows**********************/

///Get Directional Shadows Data
///get form input data
DirectionalShadowData GetDirectionalShadowData(int lightIndex, ShadowCascadeData cascadeData)
{
    DirectionalShadowData data;
    data.strength = _DirectionLightShadowData[lightIndex].x * cascadeData.strength;
    data.titleIndex = _DirectionLightShadowData[lightIndex].y + cascadeData.cascadeIndex;
    data.normalBias = _DirectionLightShadowData[lightIndex].z;

    return data;
}

///Compute Directional Shadow Attenuation Fade
///Surface data structure from [Common]
float GetDirectionalShadowAttenuation(DirectionalShadowData data, ShadowCascadeData cascadeData, Surface surfaceData)
{
    if (data.strength <= 0.0f)
        return 1.0f;

    float3 normal_bais = surfaceData.normal * data.normalBias * _CascadeData[cascadeData.cascadeIndex].y;
    float3 positionST = mul(_DirectionalShadowMatrices[data.titleIndex], float4(surfaceData.position + normal_bais, 1.0f)).xyz;
    float shadow = FilterDirectionalShadow(positionST);
    
    //sampler next cascading shadow
    if (cascadeData.cascadeBlend < 1)
    {
        float3 normal_bais_next = surfaceData.normal * data.normalBias * _CascadeData[cascadeData.cascadeIndex + 1].y;
        float3 positionST_next = mul(_DirectionalShadowMatrices[data.titleIndex + 1], float4(surfaceData.position + normal_bais_next, 1.0f)).xyz;
        float shadow_next = FilterDirectionalShadow(positionST_next);
        shadow = lerp(shadow_next, shadow, cascadeData.cascadeBlend);
    }
    
    return lerp(1.0f, shadow, data.strength);
}

float GetDirectionalShadowAttenuation(float3 positionCS, float3 positionWS, float3 normalWS, int lightIndex)
{
    Surface surface;
    surface.position = positionWS;
    surface.normal = normalWS;
    surface.dither = InterleavedGradientNoise(positionCS.xy, 0.0f);
    ShadowCascadeData cascade_data = GetShadowCascadeData(surface);
    DirectionalShadowData directional_data = GetDirectionalShadowData(lightIndex, cascade_data);
    float shadow_attenuation = GetDirectionalShadowAttenuation(directional_data, cascade_data, surface);
    return shadow_attenuation;
}
/****************************END*******************************/
#endif