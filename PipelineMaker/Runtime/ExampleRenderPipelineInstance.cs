using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using Conditional = System.Diagnostics.ConditionalAttribute;
using System.Collections;
using System.Collections.Generic;

public partial class ExampleRenderPipelineInstance : RenderPipeline
{
    private ExampleRenderPipelineAsset _asset;
    private CullingResults _cullResults;
    private CommandBuffer cameraBuffer = new CommandBuffer() { 
        name = "Render Buffer"
    };
    private Material _errorMaterial;

    static ScriptableRenderContext pipelineContext;

    //---------------------
    //-----Light Data------
    //---------------------
    const int maxVisibleLights = 8;
    static int visibleLightColorId = Shader.PropertyToID("_VisibleLightColors");
    static int visibleLightDirId = Shader.PropertyToID("_VisibleLightDirectionsOrPosition");
    static int visibleLightAttenuationsId = Shader.PropertyToID("_VisibleLightAttenuations");
    static int visibleLIghtSpotDirectionsId = Shader.PropertyToID("_VisibleLightSpotDirections");
    Vector4[] visibleLightColors = new Vector4[maxVisibleLights];
    Vector4[] visibleLightDirectionsOrPosition = new Vector4[maxVisibleLights];
    Vector4[] visibleLightAttenuations = new Vector4[maxVisibleLights];
    Vector4[] visibleLightSpotDirections = new Vector4[maxVisibleLights];

    public ExampleRenderPipelineInstance(ExampleRenderPipelineAsset asset)
    {
        GraphicsSettings.lightsUseLinearIntensity = true;
        _asset = asset;
    }

    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        //--buffer.BeginSample/EndSample 仅能采样到自定义命令 封装在上下文中的无法采样--
        pipelineContext = context;

        //clear
        cameraBuffer.ClearRenderTarget(true, true, Color.clear);
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();

        //camera cull
        foreach (var camera in cameras)
        {
            if (!camera.TryGetCullingParameters(out var cullparameters))
                return;

            
            //设置最大阴影距离
            cullparameters.shadowDistance = Mathf.Min(_asset.m_shadowSettings.maxDistance, camera.farClipPlane);
#if UNITY_EDITOR
            if(camera.cameraType == CameraType.SceneView)
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
            _cullResults = context.Cull(ref cullparameters);
            ShadowsSetting();
            RenderShadows();

            //Lights Data Setting
            PerObjectData default_perObjectData = SetLightsEach();
            if (_cullResults.visibleLights.Count() > 0)
            {
                ConfigureLights();
                SetLightsData(context, cameraBuffer);
            }

            // 基于当前摄像机，更新内置着色器变量的值
            // 例如基于相机设置vp矩阵
            context.SetupCameraProperties(camera);
            // 基于 LightMode 通道标签值，向 Unity 告知要绘制的几何体
            ShaderTagId shaderTagId = new ShaderTagId("SRPDefaultUnlit");
            // 基于当前摄像机，向 Unity 告知如何对几何体进行排序
            var opaque_sortingSetting = new SortingSettings(camera);
            opaque_sortingSetting.criteria = SortingCriteria.CommonOpaque;
            // 创建描述要绘制的几何体以及绘制方式的 DrawingSettings 结构
            DrawingSettings opaque_drawingSettings = new DrawingSettings(shaderTagId, opaque_sortingSetting)
            {
                perObjectData = default_perObjectData
            };
            opaque_drawingSettings.SetShaderPassName(1, new ShaderTagId("ExampleLightModeTag"));
            SettingDrawSettingWiAsset(ref opaque_drawingSettings);
            // 告知 Unity 如何过滤剔除结果，以进一步指定要绘制的几何体
            // 使用 FilteringSettings.defaultValue 可指定不进行过滤
            FilteringSettings filteringSettings = FilteringSettings.defaultValue;
            filteringSettings.renderQueueRange = RenderQueueRange.opaque;
            // 基于定义的设置，调度命令绘制几何体
            context.DrawRenderers(_cullResults, ref opaque_drawingSettings, ref filteringSettings);

            // 在需要时调度命令绘制天空盒
            if (camera.clearFlags == CameraClearFlags.Skybox && RenderSettings.skybox != null)
            {
                context.DrawSkybox(camera);
            }

            var transparent_sortingSetting = new SortingSettings(camera);
            transparent_sortingSetting.criteria = SortingCriteria.CommonTransparent;
            DrawingSettings transparent_drawingSettings = new DrawingSettings(shaderTagId, transparent_sortingSetting)
            {
                perObjectData = default_perObjectData
            };
            transparent_drawingSettings.SetShaderPassName(1, new ShaderTagId("ExampleLightModeTag"));
            SettingDrawSettingWiAsset(ref transparent_drawingSettings);
            //render transparent
            filteringSettings.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(_cullResults, ref transparent_drawingSettings, ref filteringSettings);

            DrawDefaultRenderPipeline(context, camera, _cullResults);

            context.Submit();

            //relese
            ReleseShadows();
        }
    }

    [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
    private void DrawDefaultRenderPipeline(ScriptableRenderContext context, Camera camera, CullingResults cullingResults)
    {
        if (_errorMaterial == null)
        {
            Shader errorShader = Shader.Find("Hidden/InternalErrorShader");
            _errorMaterial = new Material(errorShader);
            _errorMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        ShaderTagId shaderTagId = new ShaderTagId("ForwardBase");
        SortingSettings default_sortingSetting = new SortingSettings(camera);
        DrawingSettings default_drawingSetting = new DrawingSettings(shaderTagId, default_sortingSetting);
        default_drawingSetting.SetShaderPassName(1, new ShaderTagId("PrepassBase"));
        //...

        default_drawingSetting.overrideMaterial = _errorMaterial;
        default_drawingSetting.overrideMaterialPassIndex = 0;
        FilteringSettings default_filterSetting = FilteringSettings.defaultValue;
        context.DrawRenderers(cullingResults, ref default_drawingSetting, ref default_filterSetting);
    }

    private void SettingDrawSettingWiAsset(ref DrawingSettings settings)
    {
        settings.enableDynamicBatching = _asset.DynamicBatcher;
        settings.enableInstancing = _asset.Instancing;
    }

    #region Lighting

    private void ConfigureLights()
    {
        for (int i = 0; i < _cullResults.visibleLights.Count(); i++)
        {
            if (i == maxVisibleLights) break;
            VisibleLight light = _cullResults.visibleLights[i];
            visibleLightColors[i] = light.finalColor;
            Vector4 attenuation = Vector4.zero;
            attenuation.z = 1f;
            attenuation.w = 1f;
            if (light.lightType == LightType.Directional)
            {
                //方向向量w分量为0
                //矩阵3行存储方向 
                Vector4 v = light.localToWorldMatrix.GetColumn(2);
                v.x = -v.x;
                v.y = -v.y;
                v.z = -v.z;
                visibleLightDirectionsOrPosition[i] = v;
            }
            else
            {
                //位置向量w分量为1
                //矩阵4行存储位置
                Vector4 v = light.localToWorldMatrix.GetColumn(3);
                visibleLightDirectionsOrPosition[i] = v;
                //光照范围公式 (1-(d^2/r^2)^2)^2
                attenuation.x = 1 / Mathf.Max(light.range * light.range, 0.00001f);

                //检查聚光灯
                if (light.lightType == LightType.Spot)
                {
                    Vector4 spot_v = light.localToWorldMatrix.GetColumn(2);
                    spot_v.x = -spot_v.x;
                    spot_v.y = -spot_v.y;
                    spot_v.z = -spot_v.z;
                    visibleLightSpotDirections[i] = spot_v;

                    float outerRad = Mathf.Deg2Rad * 0.5f * light.spotAngle;
                    float outerCos = Mathf.Cos(outerRad);
                    float outerTan = Mathf.Tan(outerRad);
                    float innerCos = Mathf.Cos(Mathf.Atan(((64f - 18f) / 64f) * outerTan));
                    //(Ds·Dl)a + b, a=1/(cos(ri) - cos(ro)) b= -cos(ro)a
                    float angleRange = Mathf.Max(innerCos - outerCos, 0.001f);
                    attenuation.z = 1f / angleRange;
                    attenuation.w = -outerCos * attenuation.z;
                }
            }
            visibleLightAttenuations[i] = attenuation;
        }

        if (_cullResults.visibleLights.Count() > maxVisibleLights)
        {
            NativeArray<int> lightIndexsMap = _cullResults.GetLightIndexMap(Allocator.Temp);
            for (int i = maxVisibleLights; i < lightIndexsMap.Length; i++)
            {
                lightIndexsMap[i] = -1;
            }
            _cullResults.SetLightIndexMap(lightIndexsMap);
        }
    }

    private void SetLightsData(ScriptableRenderContext context, CommandBuffer cameraBuffer)
    {
        cameraBuffer.SetGlobalVectorArray(visibleLightColorId, visibleLightColors);
        cameraBuffer.SetGlobalVectorArray(visibleLightDirId, visibleLightDirectionsOrPosition);
        cameraBuffer.SetGlobalVectorArray(visibleLightAttenuationsId, visibleLightAttenuations);
        cameraBuffer.SetGlobalVectorArray(visibleLIghtSpotDirectionsId, visibleLightSpotDirections);
        context.ExecuteCommandBuffer(cameraBuffer);
        cameraBuffer.Clear();
        for (int i = 0; i < maxVisibleLights; i++)
        {
            visibleLightColors[i] = Color.clear;
            visibleLightDirectionsOrPosition[i] = Vector4.zero;
            visibleLightAttenuations[i] = Vector4.zero;
            visibleLightSpotDirections[i] = Vector4.zero;
        }
    }

    private PerObjectData SetLightsEach()
    {
        PerObjectData perObjectData;
        if (_asset.m_lighting.useLightsPerObject)
        {
            perObjectData = PerObjectData.LightData | PerObjectData.LightIndices;
            Shader.EnableKeyword(_asset.m_lighting.lightPerObjectKeyword);
        }
        else
        {
            perObjectData = PerObjectData.None;
            Shader.DisableKeyword(_asset.m_lighting.lightPerObjectKeyword);
        }
            
        return perObjectData;
    }
    #endregion
}