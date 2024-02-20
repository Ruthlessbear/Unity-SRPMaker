#ifndef CUSTOM_LIGHTS_INCLUDE
#define CUSTOM_LIGHTS_INCLUDE

#define MAX_VISIBLE_LIGHTS 8
CBUFFER_START(_LightBuffer)
    float4 _VisibleLightColors[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightDirectionsOrPosition[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightAttenuations[MAX_VISIBLE_LIGHTS];
    float4 _VisibleLightSpotDirections[MAX_VISIBLE_LIGHTS];
CBUFFER_END

/**************************************************************/
/***************************Lighting***************************/
float3 GetLithting(int index, float3 positionWS, float3 normalWS)
{
    float3 lightColor = _VisibleLightColors[index].rgb;
    float4 lightDirOrPosition = _VisibleLightDirectionsOrPosition[index];
    float4 lightAttenuation = _VisibleLightAttenuations[index];
    float4 lightSpotDir = _VisibleLightSpotDirections[index];
    
    float3 lightVector = lightDirOrPosition.xyz - positionWS * lightDirOrPosition.w;
    float3 lightDir = normalize(lightVector);
    
    //TODO: compute light model
    float3 diffuseLight = saturate(dot(normalWS, lightDir.xyz)) * 0.5f + 0.5f;
    
    //attenuation
    //Formula (1-(d^2/r^2)^2)^2
    //Parallel light lightAttenuation.x=0
    float rangeFade = dot(lightVector, lightVector) * lightAttenuation.x;
    rangeFade = saturate(1 - rangeFade * rangeFade);
    rangeFade *= rangeFade;

    float spotFade = dot(lightSpotDir.xyz, lightDir);
    spotFade = saturate(spotFade * lightAttenuation.z + lightAttenuation.w);
    spotFade *= spotFade;
    
    float diffuse_attenuation = max(dot(lightDir, lightDir), 0.001f);
    float3 lightAdd = diffuseLight * lightColor;
    float lightFade = spotFade * rangeFade / diffuse_attenuation;
    return lightAdd * lightFade;
}
/**************************************************************/

#endif