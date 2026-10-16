// Glowing boost pad with chevrons that scroll in the driving direction (+V of the quad).
Shader "RacingGame/BoostPad"
{
    Properties
    {
        _BaseColor ("Base", Color) = (0.95,0.35,0.05,1)
        _ArrowColor ("Arrows", Color) = (1,0.9,0.2,1)
        _EdgeColor ("Edge", Color) = (1,1,1,1)
        _Count ("Arrow Count", Float) = 3
        _Speed ("Scroll Speed", Float) = 1.6
        _Glow ("Glow", Range(1,3)) = 1.6
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+1" }
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _BaseColor, _ArrowColor, _EdgeColor;
            half _Count, _Speed, _Glow;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // Chevron: a "V" shape repeated along V, scrolling forward.
                float chevron = frac(uv.y * _Count - abs(uv.x - 0.5) * 1.2 - _Time.y * _Speed);
                float arrow = smoothstep(0.42, 0.47, chevron) * (1.0 - smoothstep(0.78, 0.83, chevron));
                float inside = step(0.12, uv.x) * step(uv.x, 0.88);
                arrow *= inside;

                // Bright border around the pad.
                float2 e = min(uv, 1.0 - uv);
                float edge = 1.0 - smoothstep(0.03, 0.05, min(e.x, e.y));

                float pulse = 0.85 + 0.15 * sin(_Time.y * 8.0);
                float3 col = _BaseColor.rgb;
                col = lerp(col, _ArrowColor.rgb * _Glow * pulse, arrow);
                col = lerp(col, _EdgeColor.rgb, edge);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}
