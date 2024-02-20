using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "ExampleRenderPipeline/CreatePipeline")]
public class ExampleRenderPipelineAsset : RenderPipelineAsset
{
    public Color exampleColor;
    public string exampleString;

    [SerializeField]
    public bool DynamicBatcher;
    [SerializeField]
    public bool Instancing;

    /// <summary>
    /// Light Settings
    /// </summary>
    [Serializable]
    public class Lighting
    {
        public bool useLightsPerObject = false;
        [HideInInspector]
        public string lightPerObjectKeyword = "_LIGHTS_PER_OBJECT";
    }
    [SerializeField]
    public Lighting m_lighting;

    /// <summary>
    /// Shadow Settings
    /// </summary>
    /// <returns></returns>
    [SerializeField]
    public ShadowSettings m_shadowSettings = new ShadowSettings();

    protected override RenderPipeline CreatePipeline()
    {
        return new ExampleRenderPipelineInstance(this);
    }
}

/************************Shadows Setting****************************/
[Serializable]
public class ShadowSettings
{
    #region Enum Type
    public enum TextureSize
    {
        _256 = 256, _512 = 512, _1024 = 1024,
        _2048 = 2048, _4096 = 4096
    }
    public enum FilterMode
    {
        PCF2x2, PCF3x3, PCF5x5, PCF7x7
    }

    public enum CascadeBlendMode
    { 
        Hard, Soft, Dither
    }
    #endregion

    [Min(0f)]
    public float maxDistance = 100f;
    [Range(0.001f, 1.0f)]
    public float distanceFade = 0.1f;

    #region Directional Lights
    /// <summary>
    /// Directional Lights Setting
    /// </summary>
    [Serializable]
    public struct Directional
    {
        public TextureSize atlasSize;
        public FilterMode filterMode;
        public CascadeBlendMode cascadeBlendMode;

        //cascade shadow
        [Range(1, 4)]
        public int cascadeCount;
        [Range(0f, 1.0f)]
        public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
        [Range(0.001f, 1.0f)]
        public float cascadeFade;
    }

    public Directional directional = new Directional()
    {
        atlasSize = TextureSize._1024,
        filterMode = FilterMode.PCF3x3,
        cascadeCount = 4,
        cascadeRatio1 = 0.1f,
        cascadeRatio2 = 0.25f,
        cascadeRatio3 = 0.5f,
        cascadeFade = 0.1f,
        cascadeBlendMode = CascadeBlendMode.Hard
    };

    //cascade ratios 
    //max num 4
    public Vector3 CacadeRatios => new Vector3(directional.cascadeRatio1, directional.cascadeRatio2, directional.cascadeRatio3);
    #endregion
}
/****************************END******************************/
