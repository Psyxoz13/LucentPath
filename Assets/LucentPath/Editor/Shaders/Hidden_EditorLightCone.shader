Shader "Hidden/LucentPath/Editor/LightCone"
{
    Properties
    {
        _ConeColor("Cone Color", Color) = (1, 0.7, 0.3, 0.2)
        _DrawRect("Draw Rect", Vector) = (0, 0, 1, 1)
        _ConeOrigin("Cone Origin", Vector) = (0, 0, 0, 0)
        _ConeDirection("Cone Direction", Vector) = (1, 0, 0, 0)
        _ConeLength("Cone Length", Float) = 1
        _ConeBaseHalfWidth("Cone Base Half Width", Float) = 1
        _ConeSpread("Cone Spread", Float) = 0.2
        _SphereCenter("Sphere Center", Vector) = (0, 0, 0, 0)
        _SphereRadius("Sphere Radius", Float) = 1
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

            fixed4 _ConeColor;
            float4 _DrawRect;
            float4 _ConeOrigin;
            float4 _ConeDirection;
            float _ConeLength;
            float _ConeBaseHalfWidth;
            float _ConeSpread;
            float4 _SphereCenter;
            float _SphereRadius;

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
                float2 pixel = float2(
                    _DrawRect.x + i.uv.x * _DrawRect.z,
                    _DrawRect.y + (1.0 - i.uv.y) * _DrawRect.w);
                float2 direction = normalize(_ConeDirection.xy);
                float2 normal = float2(-direction.y, direction.x);
                float2 offset = pixel - _ConeOrigin.xy;
                float axis = dot(offset, direction);
                float lateral = abs(dot(offset, normal));
                float halfWidth = _ConeBaseHalfWidth + _ConeSpread * max(axis, 0.0);
                const float edgeSoftness = 1.25;
                float2 rayToPixel = pixel - _ConeOrigin.xy;
                float rayLength = length(rayToPixel);
                float2 rayDirection = rayLength > 0.001 ? rayToPixel / rayLength : direction;
                float sphereProjection = clamp(dot(_SphereCenter.xy - _ConeOrigin.xy, rayDirection), 0.0, rayLength);
                float2 closestPointOnRaySegment = _ConeOrigin.xy + rayDirection * sphereProjection;
                float raySphereDistance = distance(closestPointOnRaySegment, _SphereCenter.xy);

                float startMask = smoothstep(-edgeSoftness, edgeSoftness, axis);
                float endMask = 1.0 - smoothstep(_ConeLength - edgeSoftness, _ConeLength + edgeSoftness, axis);
                float coneMask = smoothstep(-edgeSoftness, edgeSoftness, halfWidth - lateral);
                float sphereDistance = distance(pixel, _SphereCenter.xy);
                float sphereMask = smoothstep(_SphereRadius - edgeSoftness, _SphereRadius + edgeSoftness, sphereDistance);
                float occlusionMask = smoothstep(_SphereRadius - edgeSoftness, _SphereRadius + edgeSoftness, raySphereDistance);
                float referenceDistance = max(_ConeLength * 0.18, max(_ConeBaseHalfWidth * 6.0, 14.0));
                float distanceSquared = max(dot(rayToPixel, rayToPixel), 1.0);
                float attenuation = min(1.0, (referenceDistance * referenceDistance) / distanceSquared);
                float alpha = _ConeColor.a * startMask * endMask * coneMask * sphereMask * occlusionMask * attenuation;

                if (alpha <= 0.001)
                    return 0.0;

                return fixed4(_ConeColor.rgb, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
