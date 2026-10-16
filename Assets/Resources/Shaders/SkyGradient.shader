// Bright cartoon sky: three-colour gradient, glowing sun and drifting puffy clouds.
// Used as a procedural skybox material (RenderSettings.skybox).
Shader "RacingGame/SkyGradient"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.25,0.55,1,1)
        _HorizonColor ("Horizon", Color) = (0.75,0.9,1,1)
        _BottomColor ("Bottom", Color) = (0.55,0.75,0.9,1)
        _SunColor ("Sun", Color) = (1,0.95,0.75,1)
        _SunSize ("Sun Size", Range(0.001,0.2)) = 0.04
        _CloudColor ("Cloud", Color) = (1,1,1,1)
        _CloudShade ("Cloud Shade", Color) = (0.78,0.85,0.97,1)
        _CloudCover ("Cloud Cover", Range(0,1)) = 0.5
        _CloudScale ("Cloud Scale", Float) = 1.6
        _CloudSpeed ("Cloud Speed", Float) = 0.01
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _TopColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor, _CloudShade;
            half _SunSize, _CloudCover, _CloudScale, _CloudSpeed;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash (float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float noise (float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float fbm (float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 4; k++)
                {
                    v += a * noise(p);
                    p *= 2.03;
                    a *= 0.5;
                }
                return v;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                float3 col = h >= 0.0
                    ? lerp(_HorizonColor.rgb, _TopColor.rgb, pow(saturate(h), 0.55))
                    : lerp(_HorizonColor.rgb, _BottomColor.rgb, pow(saturate(-h), 0.4));

                // Sun disc with a soft glow.
                float3 sunDir = normalize(_WorldSpaceLightPos0.xyz);
                float s = dot(d, sunDir);
                float disc = smoothstep(1.0 - _SunSize, 1.0 - _SunSize * 0.85, s);
                float glow = pow(saturate(s), 64.0) * 0.6;
                col += _SunColor.rgb * (disc + glow);

                // Cartoon clouds: noise projected onto a dome, thresholded for crisp edges.
                if (h > 0.0)
                {
                    float2 uv = d.xz / (h + 0.18) * _CloudScale;
                    uv += _Time.y * _CloudSpeed * float2(1.0, 0.4);
                    float n = fbm(uv);
                    float edge = 1.0 - _CloudCover;
                    float cloud = smoothstep(edge, edge + 0.06, n);
                    float shade = smoothstep(edge + 0.04, edge + 0.2, fbm(uv + 0.15));
                    float3 cloudCol = lerp(_CloudShade.rgb, _CloudColor.rgb, shade);
                    float fade = saturate(h * 6.0);
                    col = lerp(col, cloudCol, cloud * fade);
                }
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
