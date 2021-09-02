Shader "God/GodRay"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" { }
    }
    
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
    #include "Packages/com.unity.render-pipelines.universal/Shaders/PostProcessing/Common.hlsl"

    TEXTURE2D_X(_MainTex);
    TEXTURE2D_X_FLOAT(_CameraDepthTexture);
    SAMPLER(sampler_CameraDepthTexture);

    TEXTURE2D_X(_LightShaftTempTex);

    half4 _MainTex_ST;
    float4 _MainTex_TexelSize;

    float4 _CamWorldSpace;
    float4x4 _CamFrustum, _CamToWorld;
    int _MaxIterations;
    float _MaxDistance;
    float _MinDistance;
    float _BlurScale;
    float _DepthOutsideDecreaseValue;
    float _DepthOutsideDecreaseSpeed;
    float _DepthOutsideDecreasePower;
    float4 _DayLightColorFix;
    float4 _MidNightLightColorFix;

    float _Intensity;

    struct RayVaryings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        float4 ray : TEXCOORD1;
    };

    RayVaryings Vert_Ray(Attributes input)
    {
        RayVaryings output;
        output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
        output.uv = input.uv;

        int index = output.uv.x + 2 * output.uv.y;
        output.ray = _CamFrustum[index];
        
        return output;
    }

    float GetRandomNumber(float2 texCoord, int Seed)
    {
        return frac(sin(dot(texCoord.xy, float2(12.9898, 78.233)) + Seed) * 43758.5453);
    }

    half4 SimpleRaymarching(float3 rayOrigin, float3 rayDirection, float depth)
    {
        //【初始化我们的颜色结果】
        half4 result = float4(_MainLightColor.xyz, 1) * _Intensity;
        
        //【定义光追步长】
        float step = _MaxDistance / _MaxIterations;
        //【定义光追step】
        float t = _MinDistance + step * GetRandomNumber(rayDirection, _Time.y * 100);
        // float t = _MinDistance;
        float alpha = 0;

        for (int i = 0; i < _MaxIterations; i++)
        {
            //【超出最大范围】
            if (t > _MaxDistance || t >= depth)
            {
                break;
            }
            //【当前点与物体的距离】
            float3 p = rayOrigin + rayDirection * t;
            //【阴影没有体积光】
            float4 shadowCoord = TransformWorldToShadowCoord(p);
            float shadow = SAMPLE_TEXTURE2D_SHADOW(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture, shadowCoord);
            if (shadow >= 1)
            {
                //【无阴影加入结果】
                alpha += step * 0.2;
            }
            //【继续移动】
            t += step;
        }

        result.a *= saturate(alpha);

        return result;
    }

    half4 Frag(RayVaryings input) : SV_Target
    {
        float depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_LinearClamp, input.uv).r;
        depth = Linear01Depth(depth, _ZBufferParams);
        if (depth > _DepthOutsideDecreaseValue / 100.0f)
        {
            depth = saturate(depth - (depth - _DepthOutsideDecreaseValue / 100.0f) * _DepthOutsideDecreaseSpeed);
        }
        depth *= length(input.ray);

        float3 rayOrigin = _CamWorldSpace;
        float3 rayDir = normalize(input.ray);
        float4 result = SimpleRaymarching(rayOrigin, rayDir, depth);
        
        return result;
    }

    half4 Frag_Combine(Varyings input) : SV_Target
    {
        half4 color = SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, input.uv);
        half4 shaft = SAMPLE_TEXTURE2D_X(_LightShaftTempTex, sampler_LinearClamp, input.uv);
        Light mainLight = GetMainLight();
        half4 LightColorFix = lerp(_MidNightLightColorFix, _DayLightColorFix, saturate(mainLight.direction.y * 3));
        LightColorFix = lerp(float4(0.0, 0.0, 0.0, 0.0), LightColorFix, saturate(mainLight.direction.y * 6 + 1));

        color.rgb = lerp(color.rgb, shaft.rgb * LightColorFix.rgb, shaft.a * LightColorFix.a);
        
        return color;
    }

    float4 Frag_Blur_Line(Varyings input) : SV_Target
    {
        float2 uv = input.uv;

        half2 uv1 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(1, 0) * - 2.0;
        half2 uv2 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(1, 0) * - 1.0;
        half2 uv3 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(1, 0) * 0.0;
        half2 uv4 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(1, 0) * 1.0;
        half2 uv5 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(1, 0) * 2.0;
        half4 s = 0;

        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv1) * 0.0545;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv2) * 0.2442;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv3) * 0.4026;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv4) * 0.2442;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv5) * 0.0545;

        return s;
    }

    float4 Frag_Blur_Colomn(Varyings input) : SV_Target
    {
        float2 uv = input.uv;

        half2 uv1 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(0, 1) * - 2.0;
        half2 uv2 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(0, 1) * - 1.0;
        half2 uv3 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(0, 1) * 0.0;
        half2 uv4 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(0, 1) * 1.0;
        half2 uv5 = uv + half2(_BlurScale / _ScreenParams.x, _BlurScale / _ScreenParams.y) * half2(0, 1) * 2.0;
        half4 s = 0;

        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv1) * 0.0545;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv2) * 0.2442;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv3) * 0.4026;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv4) * 0.2442;
        s += SAMPLE_TEXTURE2D_X(_MainTex, sampler_LinearClamp, uv5) * 0.0545;

        return s;
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "GradientFog"

            HLSLPROGRAM

            #pragma vertex Vert_Ray
            #pragma fragment Frag
            ENDHLSL

        }
        
        Pass
        {
            Name "Combine"
            
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag_Combine
            ENDHLSL

        }
        Pass
        {
            Name "Blur_Line"
            
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag_Blur_Line
            ENDHLSL

        }
        Pass
        {
            Name "Blur_Colomn"
            
            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag_Blur_Colomn
            ENDHLSL

        }
    }
}