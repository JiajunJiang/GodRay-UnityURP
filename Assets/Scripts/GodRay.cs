using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class GodRay : VolumeComponent , IPostProcessComponent
{
    //【光线追踪部分】
    public IntParameter maxIterations = new IntParameter(64);
    public FloatParameter maxDistance = new FloatParameter(12f);
    public FloatParameter minDistance = new FloatParameter(0.4f);
    public FloatParameter DecreaseValue = new FloatParameter(1f);
    public FloatParameter DecreaseSpeed = new FloatParameter(1f);
    public FloatParameter DecreasePower = new FloatParameter(1f);
    public ClampedFloatParameter intensity = new ClampedFloatParameter(0f, 0f, 1f);
    //【白天与黄昏颜色校正】
    public ColorParameter DayColor = new ColorParameter(UnityEngine.Color.white);
    public ColorParameter MidNightColor = new ColorParameter(UnityEngine.Color.white);
    
    public IntParameter blurStep = new IntParameter(5);
    public IntParameter blurIter = new IntParameter(5);
    public FloatParameter blurRange = new FloatParameter(0.5f);
    
    public bool IsActive()
    {
        return intensity.value > 0f;
    }

    public bool IsTileCompatible()
    {
        return false;
    }
}
