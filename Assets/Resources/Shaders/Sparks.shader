// Soft additive glow for particles (drift sparks, boost flames). Uses vertex colour from the particle system.
Shader "RacingGame/Sparks"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Range(0.5,4)) = 2
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Intensity;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2.0;
                float glow = saturate(1.0 - d);
                glow = glow * glow;
                float core = smoothstep(0.55, 0.0, d);
                float3 col = lerp(i.color.rgb, float3(1, 1, 1), core * 0.7) * _Intensity;
                return fixed4(col, glow * i.color.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
