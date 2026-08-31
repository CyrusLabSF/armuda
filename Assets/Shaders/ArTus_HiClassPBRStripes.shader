Shader "HiClass/BlobDynamicSpectrum_Ultra_URP"
{
    Properties
    {
        _EmissionIntensity ("Emission Intensity", Range(0,40)) = 14
        _Transparency ("Base Transparency", Range(0,1)) = 0.65
        _FresnelPower ("Fresnel Power", Range(0.1,8)) = 3.2

        _NoiseSpeed ("Noise Speed", Range(0.1,5)) = 2.5
        _FlowSpeed ("Flow Speed", Range(0.1,5)) = 2.8

        _Glossiness ("Glossiness", Range(0,1)) = 0.7
        _SpecColor ("Specular Color", Color) = (1,1,1,1)

        _Saturation ("Saturation Boost", Range(0,3)) = 1.5
        _MetallicStrength ("Metallic Strength", Range(0,2)) = 0.9

        _DripStrength ("Drip Strength", Range(0,1)) = 0.25
        _DripLength ("Drip Length", Range(1,10)) = 4

        _VeinScale ("Vein Scale", Range(1,30)) = 8
        _VeinIntensity ("Vein Intensity", Range(0,5)) = 1.4

        _DarkMode ("Dark Mode Strength", Range(0,1)) = 0.45

        _EmotionIntensity ("Emotion Intensity", Range(0,2)) = 1
        _Tension ("Tension", Range(0,2)) = 0.2
        _Pulse ("Pulse Speed", Range(0,5)) = 1.2

        _InnerGlow ("Inner Glow", Range(0,3)) = 2.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off

        Pass
        {
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
            float _EmotionIntensity, _Tension, _Pulse, _InnerGlow;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            float hash(float3 p)
            {
                return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453);
            }

            float noise(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);

                float n000 = hash(i);
                float n100 = hash(i + float3(1,0,0));
                float n010 = hash(i + float3(0,1,0));
                float n110 = hash(i + float3(1,1,0));
                float n001 = hash(i + float3(0,0,1));
                float n101 = hash(i + float3(1,0,1));
                float n011 = hash(i + float3(0,1,1));
                float n111 = hash(i + float3(1,1,1));

                float3 u = f*f*(3.0-2.0*f);

                float nx00 = lerp(n000,n100,u.x);
                float nx10 = lerp(n010,n110,u.x);
                float nx01 = lerp(n001,n101,u.x);
                float nx11 = lerp(n011,n111,u.x);

                float nxy0 = lerp(nx00,nx10,u.y);
                float nxy1 = lerp(nx01,nx11,u.y);

                return lerp(nxy0,nxy1,u.z);
            }

            float fbm(float3 p)
            {
                float v = 0.0;
                float a = 0.5;
                for(int i=0;i<4;i++)
                {
                    v += noise(p)*a;
                    p *= 2.0;
                    a *= 0.5;
                }
                return v;
            }

            float3 HueToRGB(float h)
            {
                return float3(
                    0.5 + 0.5*cos(h),
                    0.5 + 0.5*cos(h + 2.094),
                    0.5 + 0.5*cos(h + 4.188)
                );
            }

            Varyings vert (Attributes v)
            {
                Varyings o;

                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);

                o.positionHCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.viewDirWS = SafeNormalize(GetCameraPositionWS() - pos.positionWS);

                return o;
            }

            float4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * (0.5 + _EmotionIntensity);

                float3 flowDir = normalize(float3(0.3,1.0,0.2));
                float3 p = i.positionWS * 0.6;

                float n1 = fbm(p + flowDir * t * _FlowSpeed);
                float n2 = fbm(p * 2.0 + flowDir * t * _NoiseSpeed);
                float n3 = fbm(p * 3.0 - flowDir * t * 0.5);

                float swirl = smoothstep(0.25,0.75,(n1*0.5 + n2*0.3 + n3*0.2));

                float hue = frac(t * 0.1 + swirl * 1.2) * 6.2831;
                float3 rgb = HueToRGB(hue);

                float lum = dot(rgb, float3(0.299,0.587,0.114));
                rgb = lerp(float3(lum,lum,lum), rgb, _Saturation);
                rgb = lerp(rgb, rgb * 0.3, _DarkMode);

                float vein = fbm(p * _VeinScale + t);
                float veins = smoothstep(0.45,0.55,vein);
                rgb *= (1.0 + veins * _VeinIntensity);

                float drip = fbm(float3(p.x, p.y * _DripLength - t*_FlowSpeed*2, p.z));
                rgb *= lerp(1.0,1.4,drip * _DripStrength);

                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                float fresnel = pow(1.0 - saturate(dot(N,V)), _FresnelPower);

                float pulse = sin(_Time.y * (2.0 * _Pulse)) * 0.5 + 0.5;

                float inner = (n1 * 0.5 + n2 * 0.5) * _InnerGlow;

                float3 emission =
                    rgb *
                    (_EmissionIntensity * 0.4) *
                    (0.4 + pulse * 0.6) *
                    (0.3 + fresnel * 0.7 + inner);

                float3 finalColor = emission;

                finalColor = finalColor / (1.0 + finalColor);
                finalColor = max(finalColor, rgb * 0.1);

                float alpha = saturate(_Transparency + fresnel * 0.3);

                return float4(finalColor, alpha);
            }

            ENDHLSL
        }
    }
}