Shader "ArTus/HiClassPBRStripes"
{
    Properties
    {
        // --- PBR controls ---
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.6
        _Smoothness ("Smoothness", Range(0,1)) = 0.8

        // --- Emission controls ---
        _BaseEmission ("Base Emission", Float) = 1.0
        _EmissionMultiplier ("Emission Multiplier", Float) = 2.0
        _Transparency ("Transparency", Range(0,1)) = 0.6
        _FresnelPower ("Fresnel Power", Range(0.1,5.0)) = 2.5

        // --- Stripe controls ---
        _StripeScale ("Stripe Scale", Float) = 80.0
        _StripeSpeed ("Stripe Speed", Float) = 0.4
        _StripeStrength ("Stripe Strength", Float) = 1.0

        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _NoiseStrength ("Noise Strength", Float) = 0.1
        _NoiseSpeed ("Noise Speed", Float) = 0.25

        _SpectrumSpeed ("Spectrum Speed", Float) = 0.12

        // --- Bias controls ---
        _WarmBias ("Warm Bias", Range(-1,1)) = 0.0
        _CoolBias ("Cool Bias", Range(-1,1)) = 0.0
        _IntensityBias ("Intensity Bias", Range(0,2)) = 1.0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalRenderPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 300

        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            // --- PBR inputs ---
            float4 _BaseColor;
            float _Metallic, _Smoothness;

            // --- Emission inputs ---
            float _BaseEmission, _EmissionMultiplier, _Transparency, _FresnelPower;

            // --- Stripe inputs ---
            float _StripeScale, _StripeSpeed, _StripeStrength;
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            float _NoiseStrength, _NoiseSpeed;
            float _SpectrumSpeed;

            // --- Bias inputs ---
            float _WarmBias, _CoolBias, _IntensityBias;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float3 viewDirWS    : TEXCOORD1;
                float2 uv           : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionHCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = normalize(TransformObjectToWorldNormal(v.normalOS));
                o.viewDirWS = normalize(GetCameraPositionWS() - o.positionWS);
                o.uv = v.uv;
                return o;
            }

            // --- HSV rainbow helper ---
            float3 HSVtoRGB(float h, float s, float v)
            {
                float3 rgb = saturate(abs(frac(h + float3(0, 2.0/3.0, 1.0/3.0)) * 6.0 - 3.0) - 1.0);
                return v * lerp(float3(1,1,1), rgb, s);
            }

            half4 frag(Varyings i) : SV_Target
            {
                // --- Base shading ---
                float3 N = normalize(i.normalWS);
                float3 V = normalize(i.viewDirWS);

                float metallic = _Metallic;
                float smoothness = _Smoothness;

                // Base color contribution
                float3 albedo = _BaseColor.rgb;
                float3 diffuse = albedo * 0.25;
                float3 specular = pow(saturate(dot(N,V)), smoothness * 64.0) * 0.5;

                // --- Fresnel halo ---
                float fresnel = pow(1.0 - saturate(dot(N, V)), _FresnelPower);

                // --- Stripe mask ---
                float stripe = sin((i.uv.x * _StripeScale) + (_Time.y * _StripeSpeed));
                stripe = stripe * 0.5 + 0.5;

                float2 noiseUV = i.uv + float2(_Time.y * _NoiseSpeed, 0);
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).r;
                stripe = saturate(stripe + (noise - 0.5) * _NoiseStrength);

                float stripeMask = lerp(0.6, 1.0, stripe) * _StripeStrength;

                // --- Spectrum (HSV rainbow) ---
                float hue = frac(_Time.y * _SpectrumSpeed * 0.1);
                float3 spectrum = HSVtoRGB(hue, 1.0, 1.0);

                // Bias tweaks
                spectrum.rg += _WarmBias * 0.3;
                spectrum.b  += _CoolBias * 0.3;
                spectrum = saturate(spectrum);

                // --- Emission layering ---
                float intensity = (_BaseEmission + _EmissionMultiplier) * _IntensityBias;
                float3 emission = (albedo * 0.3 + spectrum) * stripeMask * intensity;
                emission += fresnel * spectrum * 1.5;

                // --- Final combine ---
                float3 finalColor = diffuse + specular + emission;
                float alpha = _Transparency * 0.9;

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
