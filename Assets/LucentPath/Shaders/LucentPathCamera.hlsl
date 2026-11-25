#ifndef LUCENT_PATH_CAMERA_INCLUDED
#define LUCENT_PATH_CAMERA_INCLUDED

#ifndef LUCENT_PATH_CAMERA_LENS_CONSTANTS_DECLARED
#define LUCENT_PATH_CAMERA_LENS_CONSTANTS_DECLARED 1
float _CameraApertureRadius;
float _CameraFocusDistance;
int _CameraBladeCount;
float _CameraApertureRotation;
float _CameraAnamorphism;
#endif

float2 _CameraJitterUv;
float2 _PrevCameraJitterUv;

float2 RemoveCameraJitterFromUv(float2 jitteredUv)
{
    return jitteredUv + _CameraJitterUv;
}

float2 ToPrevJitteredUv(float2 prevUnjitteredUv)
{
    return prevUnjitteredUv - _PrevCameraJitterUv;
}

float2 GetHistoryUv(float2 currentPixelUv, float2 motion)
{
    return ToPrevJitteredUv(RemoveCameraJitterFromUv(currentPixelUv) - motion);
}

#define LUCENT_PATH_PI 3.14159265358979323846
#define LUCENT_PATH_TWO_PI 6.28318530717958647692

float2 RotateApertureSample(float2 p, float rotation)
{
    float s;
    float c;
    sincos(rotation, s, c);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

float2 SampleConcentricDisk(float2 u)
{
    float2 offset = 2.0 * u - 1.0;
    if (abs(offset.x) < 1e-7 && abs(offset.y) < 1e-7)
        return 0.0;

    float r;
    float theta;
    if (abs(offset.x) > abs(offset.y))
    {
        r = offset.x;
        theta = (LUCENT_PATH_PI * 0.25) * (offset.y / offset.x);
    }
    else
    {
        r = offset.y;
        theta = (LUCENT_PATH_PI * 0.5) - (LUCENT_PATH_PI * 0.25) * (offset.x / offset.y);
    }

    float s;
    float c;
    sincos(theta, s, c);
    return r * float2(c, s);
}

float2 SampleRegularPolygonAperture(float2 u, int bladeCount)
{
    uint blades = (uint)max(bladeCount, 3);
    float bladeFloat = min(u.x * (float)blades, (float)blades - 1e-5);
    uint blade = (uint)bladeFloat;
    float localU = frac(bladeFloat);

    float angle0 = ((float)blade / (float)blades) * LUCENT_PATH_TWO_PI;
    float angle1 = ((float)(blade + 1u) / (float)blades) * LUCENT_PATH_TWO_PI;

    float2 v0;
    float2 v1;
    sincos(angle0, v0.y, v0.x);
    sincos(angle1, v1.y, v1.x);

    float radius = sqrt(localU);
    return radius * lerp(v0, v1, u.y);
}

float2 SamplePhysicalAperture(float2 lensUv)
{
    float2 apertureSample = _CameraBladeCount >= 3 && _CameraBladeCount < 32
        ? SampleRegularPolygonAperture(lensUv, _CameraBladeCount)
        : SampleConcentricDisk(lensUv);

    apertureSample = RotateApertureSample(apertureSample, _CameraApertureRotation);

    float anamorphism = clamp(_CameraAnamorphism, -0.99, 0.99);
    if (anamorphism >= 0.0)
        apertureSample.y *= 1.0 + anamorphism;
    else
        apertureSample.x *= 1.0 - anamorphism;

    return apertureSample * _CameraApertureRadius;
}

Ray GenerateCameraRayFromUv(float2 uv, float4x4 invViewProj)
{
    float2 ndcXY = uv * 2.0 - 1.0;

    float4 clipNear = float4(ndcXY.x, ndcXY.y, _NearClipNdc, 1.0);
    float4 clipFar = float4(ndcXY.x, ndcXY.y, _FarClipNdc, 1.0);

    float4 worldNearH = mul(invViewProj, clipNear);
    float4 worldFarH = mul(invViewProj, clipFar);

    float3 worldNear = worldNearH.xyz / max(abs(worldNearH.w), 1e-6);
    float3 worldFar = worldFarH.xyz / max(abs(worldFarH.w), 1e-6);

    Ray ray;
    ray.origin = _CameraWorldPos.xyz;
    ray.direction = float3(0.0, 0.0, 1.0);
    ray.maxDistance = EPSILON;

    if (_IsOrthographic != 0)
    {
        float3 viewSegment = worldFar - worldNear;
        ray.origin = worldNear;
        ray.direction = SafeNormalize(viewSegment);
        ray.maxDistance = max(length(viewSegment), EPSILON);
    }
    else
    {
        float3 camSegment = worldFar - _CameraWorldPos.xyz;
        ray.origin = _CameraWorldPos.xyz;
        ray.direction = SafeNormalize(camSegment);
        ray.maxDistance = max(length(camSegment), EPSILON);
    }

    return ray;
}

Ray ApplyThinLensToCameraRay(Ray pinholeRay, float2 lensUv)
{
    if (_IsOrthographic != 0 || _CameraApertureRadius <= 0.0 || _CameraFocusDistance <= 0.0)
        return pinholeRay;

    float3 viewDirection = mul((float3x3)_ViewMatrix, pinholeRay.direction);
    float focusT = _CameraFocusDistance / max(abs(viewDirection.z), 1e-6);
    float3 focusPoint = pinholeRay.origin + pinholeRay.direction * focusT;

    float2 aperturePoint = SamplePhysicalAperture(lensUv);
    float3 cameraRight = normalize(float3(_ViewMatrix._m00, _ViewMatrix._m01, _ViewMatrix._m02));
    float3 cameraUp = normalize(float3(_ViewMatrix._m10, _ViewMatrix._m11, _ViewMatrix._m12));
    float3 lensOrigin = _CameraWorldPos.xyz + cameraRight * aperturePoint.x + cameraUp * aperturePoint.y;

    Ray ray;
    ray.origin = lensOrigin;
    ray.direction = SafeNormalize(focusPoint - lensOrigin);
    ray.maxDistance = max(pinholeRay.maxDistance + _CameraApertureRadius, EPSILON);
    return ray;
}

Ray GenerateCameraRayFromUv(float2 uv)
{
    return GenerateCameraRayFromUv(uv, _InvViewProjMatrix);
}

Ray GenerateGuideCameraRay(uint2 pixel)
{
    float2 uv = (float2(pixel) + 0.5) / float2(_Width, _Height);
    return GenerateCameraRayFromUv(uv);
}

Ray GenerateCameraRay(uint2 pixel, PathSampler pathSample)
{
    float2 uv = (float2(pixel) + GetCameraJitterSample(pathSample)) / float2(_Width, _Height);
    return ApplyThinLensToCameraRay(GenerateCameraRayFromUv(uv), GetLensUvSample(pathSample));
}

#endif
