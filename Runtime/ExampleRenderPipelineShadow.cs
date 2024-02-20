using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using System;

public partial class ExampleRenderPipelineInstance : RenderPipeline
{
    #region Shadow Struct
    struct ShadowDirectionLight
    {
        public int visibleLightIndex;

        public float slopeScaleBias;

        public float nearPlaneOffset;
    }
    #endregion

    /// <summary>
    /// Directional Light Max Count
    /// </summary>
    const int MAX_SHADOW_DIRECTION_LIGHT_COUNT = 4;
    /// <summary>
    /// Cascade Shadow Max Count
    /// </summary>
    const int MAX_CASCADES = 4;

    ///Directional Shadows
    /// <summary>
    /// Directional Shadows Input Property
    /// </summary>
    static int dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas");
    static int dirShadowMatrix = Shader.PropertyToID("_DirectionalShadowMatrices");
    static int dirShadowData = Shader.PropertyToID("_DirectionLightShadowData");
    static int shadowDistanceId = Shader.PropertyToID("_ShadowDistance");
    static Matrix4x4[] DirShadowMatrixs = new Matrix4x4[MAX_SHADOW_DIRECTION_LIGHT_COUNT * MAX_CASCADES];
    static Vector4[] DirLightShadowData = new Vector4[MAX_SHADOW_DIRECTION_LIGHT_COUNT];

    ///Cascading Shadows
    /// <summary>
    /// Cascading Shadows Input Property
    /// </summary>
    static int cascadeCountId = Shader.PropertyToID("_CascadeCount");
    static int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
    static int cascadeDataId = Shader.PropertyToID("_CascadeData");
    static Vector4[] cascadeData = new Vector4[MAX_CASCADES];
    static Vector4[] CascadeCullingSpheres = new Vector4[MAX_CASCADES];
    /// <summary>
    /// Current number of directional lights
    /// </summary>
    private int _shadowDirectionLightsCount = 0;
    /// <summary>
    /// Current data of directional lights
    /// </summary>
    private ShadowDirectionLight[] _shadowDirectionLights = new ShadowDirectionLight[MAX_SHADOW_DIRECTION_LIGHT_COUNT];

    /// <summary>
    /// PCF Filter
    /// </summary>
    static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");

    static RenderTexture _shadowMap;
    private CommandBuffer _shadowBuffer = new CommandBuffer() { 
        name = "Render Shadows"
    };

    #region Keywords
    static string[] directionalFilterKeywords = {
        "_DIRECTIONAL_PCF3",
        "_DIRECTIONAL_PCF5",
        "_DIRECTIONAL_PCF7"
    };

    static string[] cascadeBlendKeywords = {
        "_CASCADE_BLEND_SOFT",
        "_CASCADE_BLEND_DITHER"
    };
    #endregion

    private void ShadowsSetting()
    {
        _shadowDirectionLightsCount = 0;

        for (int i = 0; i < _cullResults.visibleLights.Length; i++)
        {
            if (_cullResults.visibleLights[i].lightType == LightType.Directional)
            {
                var visibleLight = _cullResults.visibleLights[i];
                SetupDirectionalLight(i, ref visibleLight);
            }
        }
    }

    private void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
    {
        Vector3 shadowData = ReserveDirectionalShadows(visibleLight.light, index);
        DirLightShadowData[index] = shadowData;
    }

    private Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
    {
        if (_shadowDirectionLightsCount < MAX_SHADOW_DIRECTION_LIGHT_COUNT && 
            light.shadows != LightShadows.None && 
            light.shadowStrength > 0f &&
            _cullResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds))
        {
            _shadowDirectionLights[_shadowDirectionLightsCount] = new ShadowDirectionLight() {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                nearPlaneOffset = light.shadowNearPlane
            };
            int cascadeCount = _asset.m_shadowSettings.directional.cascadeCount;
            return new Vector3(light.shadowStrength, cascadeCount * _shadowDirectionLightsCount++, light.shadowNormalBias);
        }
        return Vector3.zero;
    }

    #region Render
    private void RenderShadows()
    {
        if (_shadowDirectionLightsCount > 0)
        {
            RenderDirectionalShadows();
        }
    }

    private void ReleseShadows()
    {
        RenderTexture.ReleaseTemporary(_shadowMap);
        ExecutedBuffer();
    }

    private void RenderDirectionalShadows()
    {
        int atlasSize = (int)_asset.m_shadowSettings.directional.atlasSize;
        _shadowMap = RenderTexture.GetTemporary(atlasSize, atlasSize, 32, RenderTextureFormat.Depth);
        _shadowBuffer.SetRenderTarget(_shadowMap, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
        _shadowBuffer.ClearRenderTarget(true, false, Color.clear);
        ExecutedBuffer();

        int tile = _shadowDirectionLightsCount * _asset.m_shadowSettings.directional.cascadeCount;
        int split = tile <= 1 ? 1 : tile <= 4 ? 2 : 4;
        int titleSize = atlasSize / split;

        for (int i = 0; i < _shadowDirectionLightsCount; i++)
        {
            RenderDirectionalShadows(_shadowDirectionLights[i].visibleLightIndex, split, titleSize);
        }

        //set Base
        _shadowBuffer.SetGlobalMatrixArray(dirShadowMatrix, DirShadowMatrixs);
        _shadowBuffer.SetGlobalVectorArray(dirShadowData, DirLightShadowData);
        _shadowBuffer.SetGlobalTexture(dirShadowAtlasId, _shadowMap);
        //set Cascade
        _shadowBuffer.SetGlobalInt(cascadeCountId, _asset.m_shadowSettings.directional.cascadeCount);
        _shadowBuffer.SetGlobalVectorArray(cascadeCullingSpheresId, CascadeCullingSpheres);
        float f = 1.0f - _asset.m_shadowSettings.directional.cascadeFade;
        _shadowBuffer.SetGlobalVector(shadowDistanceId, new Vector4(1.0f / _asset.m_shadowSettings.maxDistance,
            1.0f / _asset.m_shadowSettings.distanceFade,
            1.0f / (1.0f - f * f), 0.0f));
        _shadowBuffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
        SetKeywords(cascadeBlendKeywords, (int)_asset.m_shadowSettings.directional.cascadeBlendMode - 1);
        //set PCF
        SetKeywords(directionalFilterKeywords, (int)_asset.m_shadowSettings.directional.filterMode - 1);
        _shadowBuffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
        ExecutedBuffer();
    }


    private void RenderDirectionalShadows(int index, int split, int atlasSize)
    {
        ShadowDirectionLight light = _shadowDirectionLights[index];
        var shadowDrawSettings = new ShadowDrawingSettings(_cullResults, light.visibleLightIndex);
        int cascadeCount = _asset.m_shadowSettings.directional.cascadeCount;
        int tileOffset = index * cascadeCount;
        Vector3 ratios = _asset.m_shadowSettings.CacadeRatios;
        float cullingFactor = Mathf.Max(0f, 0.8f - _asset.m_shadowSettings.directional.cascadeFade);
        for (int i = 0; i < cascadeCount; i++)
        {
            _cullResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios, atlasSize, light.nearPlaneOffset,
                out var viewMatrix, out var projMatrix, out var splitData);

            if (index == 0)
            {
                SetCascadeData(i, splitData, atlasSize);
            }

            splitData.shadowCascadeBlendCullingFactor = cullingFactor;
            shadowDrawSettings.splitData = splitData;
            int tileIndex = tileOffset + i;
            Vector2 offset = SetTileViewport(tileIndex, split, atlasSize);
            DirShadowMatrixs[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, offset, split);
            _shadowBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            _shadowBuffer.SetGlobalDepthBias(0, light.slopeScaleBias);
            ExecutedBuffer();
            pipelineContext.DrawShadows(ref shadowDrawSettings);
            _shadowBuffer.SetGlobalDepthBias(0, 0);
        }
    }   

    private void SetCascadeData(int cascade_index, ShadowSplitData splitData, int tileSize)
    {
        Vector4 cullingSphere = splitData.cullingSphere;
        float texelSize = 2.0f * cullingSphere.w / tileSize;

        float filterSize = texelSize * ((float)_asset.m_shadowSettings.directional.filterMode + 1.0f);
        cullingSphere.w -= filterSize;//包围盒进行过滤核大小对应的偏移，防止超出采样边界

        //计算球体包围盒半径平方
        cullingSphere.w *= cullingSphere.w;
        CascadeCullingSpheres[cascade_index] = cullingSphere;
        float sqr2 = 1.4142136f;
        cascadeData[cascade_index] = new Vector4(1.0f / cullingSphere.w, filterSize * sqr2, 0.0f, 0.0f);
    }
    #endregion

    #region View Matrix
    private Vector2 SetTileViewport(int index, int split, float tileSize)
    {
        Vector2 offset = new Vector2(index % split, index / split);
        _shadowBuffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
        return offset;
    }

    Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
    {
        if (SystemInfo.usesReversedZBuffer)
        {
            m.m20 = -m.m20;
            m.m21 = -m.m21;
            m.m22 = -m.m22;
            m.m23 = -m.m23;
        }

        float scale = 1f / split;
        m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
        m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
        m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
        m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
        m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
        m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
        m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
        m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
        m.m20 = 0.5f * (m.m20 + m.m30);
        m.m21 = 0.5f * (m.m21 + m.m31);
        m.m22 = 0.5f * (m.m22 + m.m32);
        m.m23 = 0.5f * (m.m23 + m.m33);

        return m;
    }
    #endregion

    void SetKeywords(string[] keywords, int enabledIndex)
    {
        for (int i = 0; i < keywords.Length; i++)
        {
            if (i == enabledIndex)
            {
                _shadowBuffer.EnableShaderKeyword(keywords[i]);
            }
            else
            {
                _shadowBuffer.DisableShaderKeyword(keywords[i]);
            }
        }
    }

    private void ExecutedBuffer()
    {
        pipelineContext.ExecuteCommandBuffer(_shadowBuffer);
        _shadowBuffer.Clear();
    }
}
