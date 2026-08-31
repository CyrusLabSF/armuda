Shader "Unlit/HaloPulseShader"
{
    Properties
    {
        _PulseColor ("Pulse Color", Color) = (10, 4, 0, 1)
        _PulseSpeed ("Pulse Speed", Float) = 3.0
        _NoiseScale ("Noise Scale", Float) = 40.0
        _RingSharpness ("Ring Sharpness", Float) = 8.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _PulseColor;
            float _PulseSpeed;
            float _NoiseScale;
            float _RingSharpness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float a = hash(i);
                float b = hash(i + float2(1.0, 0.0));
                float c = hash(i + float2(0.0, 1.0));
                float d = hash(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.y), u.y);
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = float2(v.vertex.x, v.vertex.z);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float dist = length(uv);
                float ring = exp(-pow(dist * _RingSharpness - 1.0, 2.0)); // Gaussian ring shape

                float t = _Time.y * _PulseSpeed;
                float flicker = noise(uv * _NoiseScale + t);

                float glow = ring * flicker;

                return fixed4(_PulseColor.rgb * glow, glow);
            }
            ENDCG
        }
    }
    FallBack "Unlit/Color"
}
