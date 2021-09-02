using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GodRayPass : ScriptableRenderPass
{
    static readonly string k_RenderTag = "Render GodRay Effects";
    static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    static readonly int TempTargetId = Shader.PropertyToID("_TempTargetGodRay");
    private static readonly int LightShaftTempId = UnityEngine.Shader.PropertyToID("_LightShaftTempTex");
    private static readonly int TempBlurBuffer1 = UnityEngine.Shader.PropertyToID("_TempBlurBuffer1");
    private static readonly int TempBlurBuffer2 = UnityEngine.Shader.PropertyToID("_TempBlurBuffer2");
    private int blurStep;
    private int blurIterations;
    GodRay GodRay;
    Material GodRayMaterial;
    RenderTargetIdentifier currentTarget;

    public GodRayPass(RenderPassEvent evt, Shader Myshader)
    {
        renderPassEvent = evt;
        var shader = Myshader;
        if (shader == null)
        {
            Debug.LogError("Shader not found.");
            return;
        }
        GodRayMaterial = CoreUtils.CreateEngineMaterial(shader);
    }

    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (GodRayMaterial == null)
        {
            Debug.LogError("Material not created.");
            return;
        }

        if (!renderingData.cameraData.postProcessEnabled) return;

        var stack = VolumeManager.instance.stack;
        GodRay = stack.GetComponent<GodRay>();
        if (GodRay == null) { return; }
        if (!GodRay.IsActive()) { return; }

        var cmd = CommandBufferPool.Get(k_RenderTag);
        Render(cmd, ref renderingData);
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    public void Setup(in RenderTargetIdentifier currentTarget)
    {
        this.currentTarget = currentTarget;
    }

    void Render(CommandBuffer cmd, ref RenderingData renderingData)
    {
        ref var cameraData = ref renderingData.cameraData;
        var camera = cameraData.camera;
        var source = currentTarget;
        int destination = TempTargetId;

        GodRayMaterial.SetMatrix("_CamFrustum", FrustumCorners(camera));
        GodRayMaterial.SetMatrix("_CamToWorld", camera.cameraToWorldMatrix);
        GodRayMaterial.SetVector("_CamWorldSpace", camera.transform.position);
        GodRayMaterial.SetInt("_MaxIterations", GodRay.maxIterations.value);
        GodRayMaterial.SetFloat("_MaxDistance", GodRay.maxDistance.value);
        GodRayMaterial.SetFloat("_MinDistance", GodRay.minDistance.value);
        GodRayMaterial.SetFloat("_Intensity", GodRay.intensity.value);
        GodRayMaterial.SetFloat("_BlurScale", GodRay.blurRange.value);
        GodRayMaterial.SetFloat("_DepthOutsideDecreaseValue", GodRay.DecreaseValue.value);
        GodRayMaterial.SetFloat("_DepthOutsideDecreaseSpeed", GodRay.DecreaseSpeed.value);
        GodRayMaterial.SetFloat("_DepthOutsideDecreasePower", GodRay.DecreasePower.value);
        GodRayMaterial.SetColor("_DayLightColorFix", GodRay.DayColor.value);
        GodRayMaterial.SetColor("_MidNightLightColorFix", GodRay.MidNightColor.value);
        blurStep = GodRay.blurStep.value;
        blurIterations = GodRay.blurIter.value;

        cmd.GetTemporaryRT(LightShaftTempId, cameraData.camera.scaledPixelWidth / 2, cameraData.camera.scaledPixelHeight / 2);
        cmd.GetTemporaryRT(TempBlurBuffer1, cameraData.camera.scaledPixelWidth / 2, cameraData.camera.scaledPixelHeight / 2);
        cmd.GetTemporaryRT(TempBlurBuffer2, cameraData.camera.scaledPixelWidth / 2, cameraData.camera.scaledPixelHeight / 2);

        cmd.SetGlobalTexture(MainTexId, source);
        cmd.GetTemporaryRT(destination, cameraData.camera.scaledPixelWidth, cameraData.camera.scaledPixelHeight, 0, FilterMode.Trilinear, RenderTextureFormat.Default);

        cmd.Blit(source, destination);

        // LightShaft生成
        cmd.Blit(null, TempBlurBuffer1, GodRayMaterial, 0);


        for (int i = 0; i < blurStep; i++)
        {
            for (int j = 0; j < blurIterations; j++)
            {
                cmd.Blit(TempBlurBuffer1, TempBlurBuffer2, GodRayMaterial, 2);
                cmd.ReleaseTemporaryRT(TempBlurBuffer1);
                cmd.GetTemporaryRT(TempBlurBuffer1, cameraData.camera.scaledPixelWidth / (2 * (i + 1)), cameraData.camera.scaledPixelHeight / (2 * (i + 1)));
                cmd.Blit(TempBlurBuffer2, TempBlurBuffer1, GodRayMaterial, 3);
                cmd.ReleaseTemporaryRT(TempBlurBuffer2);
                cmd.GetTemporaryRT(TempBlurBuffer2, cameraData.camera.scaledPixelWidth / (2 * (i + 1)), cameraData.camera.scaledPixelHeight / (2 * (i + 1)));
            }
        }

        for (int i = 0; i < blurStep; i++)
        {
            for (int j = 0; j < blurIterations; j++)
            {
                cmd.Blit(TempBlurBuffer1, TempBlurBuffer2, GodRayMaterial, 2);
                cmd.ReleaseTemporaryRT(TempBlurBuffer1);
                cmd.GetTemporaryRT(TempBlurBuffer1, cameraData.camera.scaledPixelWidth / (2 * (blurStep - i)), cameraData.camera.scaledPixelHeight / (2 * (blurStep - i)));
                cmd.Blit(TempBlurBuffer2, TempBlurBuffer1, GodRayMaterial, 3);
                cmd.ReleaseTemporaryRT(TempBlurBuffer2);
                cmd.GetTemporaryRT(TempBlurBuffer2, cameraData.camera.scaledPixelWidth / (2 * (blurStep - i)), cameraData.camera.scaledPixelHeight / (2 * (blurStep - i)));
            }
        }

        cmd.SetGlobalTexture(LightShaftTempId, new RenderTargetIdentifier(TempBlurBuffer1));

        cmd.Blit(destination, source, GodRayMaterial, 1);

        cmd.ReleaseTemporaryRT(LightShaftTempId);
        cmd.ReleaseTemporaryRT(TempBlurBuffer1);
        cmd.ReleaseTemporaryRT(TempBlurBuffer2);

    }
    private Matrix4x4 FrustumCorners(Camera cam)
    {
        Transform camtr = cam.transform;

        Vector3[] frustumCorners = new Vector3[4];
        cam.CalculateFrustumCorners(new Rect(0, 0, 1, 1),
            cam.farClipPlane, cam.stereoActiveEye, frustumCorners);

        Matrix4x4 frustumVectorsArray = Matrix4x4.identity;

        frustumVectorsArray.SetRow(0, camtr.TransformVector(frustumCorners[0]));
        frustumVectorsArray.SetRow(1, camtr.TransformVector(frustumCorners[3]));
        frustumVectorsArray.SetRow(2, camtr.TransformVector(frustumCorners[1]));
        frustumVectorsArray.SetRow(3, camtr.TransformVector(frustumCorners[2]));

        return frustumVectorsArray;
    }
}