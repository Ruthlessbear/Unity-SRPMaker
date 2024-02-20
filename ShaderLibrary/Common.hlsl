#ifndef CUSTOM_COMMON_INCLUDE
#define CUSTOM_COMMON_INCLUDE

struct Surface
{
    float3 position;
    float3 normal;
    float dither;
};

float DistanceSquared(float3 pA, float3 pB)
{
    return dot(pA - pB, pA - pB);
}
#endif