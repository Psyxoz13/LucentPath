#ifndef LUCENT_PATH_ENV_SAMPLER_INCLUDED
#define LUCENT_PATH_ENV_SAMPLER_INCLUDED

#define LUCENT_ENV_MODE_GRADIENT 0
#define LUCENT_ENV_MODE_HDRI_CUBEMAP 1

#ifndef LUCENT_PATH_ENVIRONMENT_HDRI_DECLARED
#define LUCENT_PATH_ENVIRONMENT_HDRI_DECLARED
TextureCube<float4> _EnvironmentCubemap;
SamplerState sampler_EnvironmentCubemap;
int _EnvironmentMode;
int _EnvironmentMaxMip;
float _EnvironmentRotationYawCos;
float _EnvironmentRotationYawSin;
#endif

float3 RotateDirectionAroundY(float3 d, float cosA, float sinA)
{
    float3 r;
    r.x = d.x * cosA + d.z * sinA;
    r.y = d.y;
    r.z = -d.x * sinA + d.z * cosA;
    return r;
}

float3 EvaluateGradientSky(float3 direction)
{
    float t = saturate(direction.y * 0.5 + 0.5);
    t = pow(t, _EnvironmentExponent);
    return lerp(_EnvironmentBottomColor.rgb, _EnvironmentTopColor.rgb, t) * _EnvironmentIntensity;
}

float3 EvaluateHdriCubemap(float3 direction)
{
    float3 sampleDir = RotateDirectionAroundY(direction, _EnvironmentRotationYawCos, _EnvironmentRotationYawSin);
    float4 sample = _EnvironmentCubemap.SampleLevel(sampler_EnvironmentCubemap, sampleDir, 0);
    return max(sample.rgb, 0.0) * _EnvironmentIntensity;
}

float3 EvaluateEnvironmentRadiance(float3 direction)
{
    if (_EnvironmentMode == LUCENT_ENV_MODE_HDRI_CUBEMAP)
        return EvaluateHdriCubemap(direction);
    return EvaluateGradientSky(direction);
}

float3 EvaluateHdriCubemapLod(float3 direction, float lod)
{
    float3 sampleDir = RotateDirectionAroundY(direction, _EnvironmentRotationYawCos, _EnvironmentRotationYawSin);
    float4 sample = _EnvironmentCubemap.SampleLevel(sampler_EnvironmentCubemap, sampleDir, lod);
    return max(sample.rgb, 0.0) * _EnvironmentIntensity;
}

float3 EvaluateEnvironmentRadianceLod(float3 direction, float lod)
{
    if (_EnvironmentMode == LUCENT_ENV_MODE_HDRI_CUBEMAP)
        return EvaluateHdriCubemapLod(direction, lod);
    return EvaluateGradientSky(direction);
}

#endif
