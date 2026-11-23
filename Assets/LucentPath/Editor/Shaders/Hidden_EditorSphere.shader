Shader "Hidden/LucentPath/Editor/Sphere"
{
    Properties
    {
        _ShadowColor("Shadow Color", Color) = (0.08, 0.09, 0.11, 1)
        _MidColor("Mid Color", Color) = (0.22, 0.25, 0.31, 1)
        _LightColor("Light Color", Color) = (0.56, 0.63, 0.72, 1)
        _RimColor("Rim Color", Color) = (0.78, 0.84, 0.91, 1)
        _LightDirection("Light Direction", Vector) = (-0.52, 0.64, 0.57, 0)
        _Ambient("Ambient", Range(0, 1)) = 0.16
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.12
        _Gloss("Gloss", Range(1, 128)) = 42
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            ZWrite Off
            ZTest Always
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            #define MAX_EDITOR_LIGHTS 256

            fixed4 _ShadowColor;
            fixed4 _MidColor;
            fixed4 _LightColor;
            fixed4 _RimColor;
            float4 _LightDirection;
            float _Ambient;
            float _SpecularStrength;
            float _Gloss;
            float _FlipY;
            int _EditorLightCount;
            float4 _EditorLightPositions[MAX_EDITOR_LIGHTS];
            float4 _EditorLightColors[MAX_EDITOR_LIGHTS];

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f Vert(appdata v)
            {
                v2f o;
                o.positionCS = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 Frag(v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                p.y = lerp(p.y, -p.y, saturate(_FlipY));
                float r2 = dot(p, p);
                if (r2 >= 1.0)
                    return 0.0;

                float z = sqrt(saturate(1.0 - r2));
                float3 normal = normalize(float3(p.x, p.y, z));
                float3 lightDir = normalize(_LightDirection.xyz);
                float3 viewDir = float3(0.0, 0.0, 1.0);
                float fresnel = pow(1.0 - z, 3.6);
                float lowerShadow = saturate((-normal.y * 0.38) + (normal.x * 0.24));
                float depth = lerp(0.86, 1.0, z);
                float3 color;

                if (_EditorLightCount > 0)
                {
                    float3 surfacePosition = normal;
                    float3 accumulatedLight = 0.0;
                    float accumulatedDiffuse = 0.0;
                    float accumulatedSpecular = 0.0;

                    [loop]
                    for (int lightIndex = 0; lightIndex < MAX_EDITOR_LIGHTS; lightIndex++)
                    {
                        if (lightIndex >= _EditorLightCount)
                            break;

                        float3 lightPosition = _EditorLightPositions[lightIndex].xyz;
                        float lightRadius = max(_EditorLightPositions[lightIndex].w, 0.001);
                        float3 lightToSurface = surfacePosition - lightPosition;
                        float centerDistance = max(length(lightToSurface), 0.001);
                        float3 centerDirection = lightToSurface / centerDistance;
                        float3 closestPointOnLight = lightPosition + centerDirection * lightRadius;
                        float3 lightVector = closestPointOnLight - surfacePosition;
                        float distanceSquared = max(dot(lightVector, lightVector), 0.001);
                        float inverseDistance = rsqrt(distanceSquared);
                        float3 localLightDirection = lightVector * inverseDistance;
                        float apparentSize = saturate(lightRadius / centerDistance);
                        float falloffDistance = max(centerDistance - lightRadius, 0.0);
                        float attenuation = 1.0 / (1.0 + falloffDistance * falloffDistance * 0.42);
                        float wrappedDiffuse = saturate((dot(normal, localLightDirection) + lerp(0.18, 0.44, apparentSize)) / lerp(1.18, 1.44, apparentSize));
                        float3 halfVector = normalize(localLightDirection + viewDir);
                        float specularGloss = lerp(_Gloss, max(4.0, _Gloss * 0.24), apparentSize);
                        float specular = pow(saturate(dot(normal, halfVector)), specularGloss) * _SpecularStrength * lerp(1.0, 1.7, apparentSize);
                        float3 editorLightColor = _EditorLightColors[lightIndex].rgb;
                        float editorLightIntensity = _EditorLightColors[lightIndex].a;

                        accumulatedLight += editorLightColor * wrappedDiffuse * attenuation * editorLightIntensity;
                        accumulatedDiffuse += wrappedDiffuse * attenuation * editorLightIntensity;
                        accumulatedSpecular += specular * attenuation * editorLightIntensity;
                    }

                    color = lerp(_ShadowColor.rgb, _MidColor.rgb, saturate(_Ambient + accumulatedDiffuse * 0.22));
                    color = lerp(color, _ShadowColor.rgb * 0.82, lowerShadow * 0.22);
                    color *= depth;
                    color += accumulatedLight * 1.35;
                    color += _LightColor.rgb * accumulatedSpecular * 0.72;
                    color = lerp(color, _RimColor.rgb, fresnel * 0.06);
                    color = color / (1.0 + color * 0.6);
                }
                else
                {
                    float3 halfVector = normalize(lightDir + viewDir);
                    float ndl = dot(normal, lightDir);
                    float diffuse = saturate(ndl);
                    float wrappedDiffuse = saturate((ndl + 0.3) / 1.3);
                    float specular = pow(saturate(dot(normal, halfVector)), _Gloss) * _SpecularStrength;

                    color = lerp(_ShadowColor.rgb, _LightColor.rgb, _Ambient + wrappedDiffuse * 0.76);
                    color = lerp(color, _MidColor.rgb, (1.0 - diffuse) * 0.24);
                    color = lerp(color, _ShadowColor.rgb * 0.82, lowerShadow * 0.22);
                    color *= depth;
                    color += _LightColor.rgb * specular;
                    color = lerp(color, _RimColor.rgb, fresnel * 0.035 * saturate(diffuse + 0.22));
                }

                float alpha = 1.0 - smoothstep(0.985, 1.0, sqrt(r2));
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
