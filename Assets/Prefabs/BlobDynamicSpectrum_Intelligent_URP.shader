Shader "HiClass/BlobDynamicSpectrum_Intelligent_URP"
{
    Properties
    {
        _EmissionIntensity ("Emission Intensity", Range(0,40)) = 8.0
        _Transparency ("Base Transparency", Range(0,1)) = 0.65
        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 3.5

        _NoiseSpeed ("Noise Speed", Range(0.1,8)) = 1.2
        _FlowSpeed ("Flow Speed", Range(0.1,8)) = 1.3

        _Glossiness ("Glossiness", Range(0,1)) = 0.8
        _SpecColor ("Specular Color", Color) = (1,1,1,1)

        _Saturation ("Saturation Boost", Range(0,3)) = 1.4
        _MetallicStrength ("Metallic Strength", Range(0,2)) = 0.8

        _DripStrength ("Drip Strength", Range(0,1)) = 0.35
        _DripLength ("Drip Length", Range(1,12)) = 4.0

        _VeinScale ("Vein Scale", Range(1,30)) = 10.0
        _VeinIntensity ("Vein Intensity", Range(0,5)) = 1.4

        _DarkMode ("Dark Mode Strength", Range(0,1)) = 0.65

        _EmotionIntensity ("Emotion Intensity", Range(0,2)) = 1.0
        _Tension ("Tension", Range(0,2)) = 0.0
        _Pulse ("Pulse Speed", Range(0,5)) = 1.0

        _BlobScale ("Blob Flow Scale", Range(0.1,8)) = 1.25
        _InnerGlow ("Inner Glow Strength", Range(0,4)) = 1.25
        _SurfaceWarp ("Surface Warp Strength", Range(0,2)) = 0.55
        _CoreBias ("Core Bias", Range(0,3)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalRenderPipeline"
            "Queue"="Transparent"
            "RenderType"="Transparent"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
            Name "ForwardLit"

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float _EmissionIntensity, _Transparency, _FresnelPower;
            float _NoiseSpeed, _FlowSpeed, _Glossiness;
            float4 _SpecColor;
            float _Saturation, _MetallicStrength;
            float _DripStrength, _DripLength, _VeinScale, _VeinIntensity;
            float _DarkMode;
            float _EmotionIntensity, _Tension, _Pulse;
            float _BlobScale, _InnerGlow, _SurfaceWarp, _CoreBias;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float3 objectPosOS : TEXCOORD3;
                float2 uv          : TEXCOORD4;
            };

            float hash31(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float noise3d(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                float n000 = hash31(i + float3(0,0,0));
                float n100 = hash31(i + float3(1,0,0));
                float n010 = hash31(i + float3(0,1,0));
                float n110 = hash31(i + float3(1,1,0));
                float n001 = hash31(i + float3(0,0,1));
                float n101 = hash31(i + float3(1,0,1));
                float n011 = hash31(i + float3(0,1,1));
                float n111 = hash31(i + float3(1,1,1));

                float3 u = f * f * (3.0 - 2.0 * f);

                float nx00 = lerp(n000, n100, u.x);
                float nx10 = lerp(n010, n110, u.x);
                float nx01 = lerp(n001, n101, u.x);
                float nx11 = lerp(n011, n111, u.x);

                float nxy0 = lerp(nx00, nx10, u.y);
                float nxy1 = lerp(nx01, nx11, u.y);

                return lerp(nxy0, nxy1, u.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;

                v += noise3d(p) * a; p *= 2.02; a *= 0.5;
                v += noise3d(p) * a; p *= 2.03; a *= 0.5;
                v += noise3d(p) * a; p *= 2.01; a *= 0.5;
                v += noise3d(p) * a;

                return v;
            }

            float3 HueToRGB(float h)
            {
                return float3(
                    0.5 + 0.5 * cos(h),
                    0.5 + 0.5 * cos(h + 2.094),
                    0.5 + 0.5 * cos(h + 4.188)
                );
            }

            Varyings vert(Attributes v)
            {
                Varyings o;

                float t = _Time.y;
                float3 normalOS = normalize(v.normalOS);

                // Very light organic warp so the blob feels alive without fighting blend shapes
                float3 warpSamplePos = v.positionOS.xyz * (_BlobScale * 0.8) + float3(0, t * _NoiseSpeed * 0.35, 0);
                float warpNoise = fbm(warpSamplePos);
                float pulse = sin(t * (1.5 + _Pulse) + length(v.positionOS.xyz) * 3.0) * 0.5 + 0.5;

                float warpAmount =
                    (warpNoise - 0.5) *
                    0.035 *
                    _SurfaceWarp *
                    (0.5 + pulse * 0.5) *
                    (1.0 + _EmotionIntensity * 0.35 + _Tension * 0.25);

                float3 warpedOS = v.positionOS.xyz + normalOS * warpAmount;

                VertexPositionInputs pos = GetVertexPositionInputs(warpedOS);

                o.positionHCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.viewDirWS = SafeNormalize(GetCameraPositionWS() - pos.positionWS);
                o.objectPosOS = warpedOS;
                o.uv = v.uv;

                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y * (0.55 + _EmotionIntensity * 0.65);

                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                // Object-space blob coordinates drive the look instead of torus UV loops
                float3 obj = i.objectPosOS * _BlobScale;

                float pulse = sin(_Time.y * (2.0 * _Pulse)) * 0.5 + 0.5;

                // Multi-layer organic field
                float3 flowA = obj + float3(0.0, t * _FlowSpeed, 0.0);
                float3 flowB = obj * 1.9 + float3(t * _NoiseSpeed, 0.0, -t * 0.5);
                float3 flowC = obj * 3.2 + float3(-t * 0.35, t * 0.4, t * 0.7);

                float n1 = fbm(flowA);
                float n2 = fbm(flowB);
                float n3 = fbm(flowC);

                float swirl = saturate((n1 * 0.45 + n2 * 0.35 + n3 * 0.20) * (1.0 + _Tension * 0.65));

                float hue = frac(t * 0.08 + swirl * 1.35 + n2 * 0.22) * 6.2831853;
                float3 rgb = HueToRGB(hue);

                float secondaryHue = frac(t * 0.035 + n3 * 0.85) * 6.2831853;
                float3 secondaryRGB = HueToRGB(secondaryHue);

                rgb = lerp(rgb, secondaryRGB, 0.28 + _EmotionIntensity * 0.18);

                // Saturation and dark bias
                float lum = dot(rgb, float3(0.299, 0.587, 0.114));
                rgb = lerp(float3(lum, lum, lum), rgb, _Saturation);
                rgb = lerp(rgb, rgb * 0.3, _DarkMode);

                // Cellular vein pattern
                float veinField = fbm(obj * _VeinScale + float3(t * _NoiseSpeed, 0.0, 0.0));
                float veins = smoothstep(0.48, 0.58, veinField);
                rgb *= (1.0 + veins * (_VeinIntensity + _Tension * 0.85));

                // Downward “drip” bias based on object-space Y
                float dripField = fbm(float3(obj.x * 0.9, obj.y * _DripLength - t * _FlowSpeed * 2.0, obj.z * 0.9));
                float dripMask = smoothstep(0.55, 0.82, dripField) * saturate(1.0 - (i.objectPosOS.y + 0.5));
                rgb *= lerp(1.0, 1.55, dripMask * (_DripStrength + _EmotionIntensity * 0.35));

                // Fresnel and core glow
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // Main directional light
                Light mainLight = GetMainLight();
                float3 L = normalize(-mainLight.direction);
                float3 H = normalize(L + V);
                float spec = pow(saturate(dot(N, H)), lerp(8.0, 96.0, _Glossiness));

                // Internal body/core glow: stronger away from silhouette and inside noisy pockets
                float bodyFacing = saturate(dot(N, V));
                float innerField = saturate((n1 * 0.5 + n2 * 0.5) * (1.0 + pulse * 0.35));
                float coreGlow = pow(saturate(bodyFacing), 1.0 + _CoreBias) * innerField * _InnerGlow;

                float3 emission =
                    rgb *
                    (_EmissionIntensity * 0.34) *
                    (0.35 + pulse * 0.65) *
                    (0.25 + fresnel * 0.75 + coreGlow);

                float3 specular =
                    lerp(_SpecColor.rgb, rgb, 0.55) *
                    spec *
                    (_MetallicStrength * 0.45);

                float3 finalColor = emission + specular;

                // Soft tonemap + preserve color identity
                finalColor = finalColor / (1.0 + finalColor);
                finalColor = lerp(finalColor, rgb, 0.22);
                finalColor = max(finalColor, rgb * 0.07);

                float alpha = saturate(_Transparency + fresnel * 0.28 + coreGlow * 0.12);

                return float4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}