using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public class GodRayRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        public Shader shader;
    }
    
    public Settings settings = new Settings();
    GodRayPass GodRayPass;

    public override void Create()
    {
        this.name = "GodRayPass";
        GodRayPass = new GodRayPass(RenderPassEvent.BeforeRenderingPostProcessing, settings.shader);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        GodRayPass.Setup(renderer.cameraColorTarget);
        renderer.EnqueuePass(GodRayPass);
    }
}
