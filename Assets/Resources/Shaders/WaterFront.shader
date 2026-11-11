// The streams seen from the side (CbWorld): their front, a see-through body of water drawn in world space. Its surface
// is a line of waves rolling along with a white line of foam on it; below, it deepens from light turquoise to deep blue,
// a net of light ripples (caustics) plays in its upper part and soft rays slant down through it. Everything moves with time.
// (The painted cross-section stood still like a picture: the user.)
// Two versions: URP (first SubShader) and the built-in pipeline (fallback).
Shader "Cloudborne/WaterFront"
{
    Properties
    {
        // not drawn: there so code that asks any material for its texture (mainTexture) gets none, not an error
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        _Shallow ("Shallow", Color) = (0.32, 0.82, 0.95, 0.66)
        _Deep ("Deep", Color) = (0.04, 0.3, 0.56, 0.95)
        _Foam ("Foam", Color) = (1, 1, 1, 0.96)
        _Level ("Surface height (world)", Float) = -0.45
        _Amp ("Wave height", Float) = 0.05
        _DepthFade ("Depth to deep (m)", Float) = 2.6
    }

    // ---------- Universal Render Pipeline ----------
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow;
                half4 _Deep;
                half4 _Foam;
                float _Level;
                float _Amp;
                float _DepthFade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 world : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y;
                float x = i.world.x, y = i.world.y;
                // the waves along the top: three of them, rolling at their own speeds, never below the still surface
                // (in a trough the channel behind would show as a dark line)
                float surface = _Level + _Amp * (1.85 + sin(x * 1.3 + t * 1.6) + 0.55 * sin(x * 2.7 - t * 2.2) + 0.3 * sin(x * 5.3 + t * 3.4));
                float d = surface - y;
                clip(d);
                half4 c = lerp(_Shallow, _Deep, saturate(d / _DepthFade));
                // light ripples near the top, rays slanting down
                // (three waves 120 degrees apart over a warped plane: where their sum crosses zero, a wavy net of light)
                float wx = x + 0.35 * sin(y * 3.1 + t * 0.9);
                float wy = y + 0.35 * sin(x * 2.7 - t * 0.8);
                float s3 = sin(6.5 * wx + t * 1.1) + sin(6.5 * (-0.5 * wx + 0.866 * wy) - t * 0.9) + sin(6.5 * (-0.5 * wx - 0.866 * wy) + t * 0.7);
                float cau = pow(saturate(1.0 - abs(s3) / 0.9), 4.0) * exp(-d * 1.3);
                float ray = pow(saturate(sin(x * 0.85 + y * 0.32 + t * 0.2) * 0.5 + 0.5), 7.0) * exp(-d * 0.45) * 0.16;
                c.rgb += (half)(cau * 0.35 + ray) * half3(0.8, 1.0, 1.0);
                // the foam line along the top, a glow just under it, a soft edge
                float foam = 1.0 - smoothstep(0.035, 0.075, d);
                float glow = (1.0 - smoothstep(0.08, 0.35, d)) * 0.22;
                c.rgb = lerp(c.rgb + (half)glow, _Foam.rgb, (half)foam);
                c.a = max(c.a, (half)(foam * _Foam.a)) * (half)saturate(d / 0.012);
                return c;
            }
            ENDHLSL
        }
    }

    // ---------- Built-in render pipeline (fallback) ----------
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Shallow;
            fixed4 _Deep;
            fixed4 _Foam;
            float _Level;
            float _Amp;
            float _DepthFade;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 world : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float x = i.world.x, y = i.world.y;
                float surface = _Level + _Amp * (1.85 + sin(x * 1.3 + t * 1.6) + 0.55 * sin(x * 2.7 - t * 2.2) + 0.3 * sin(x * 5.3 + t * 3.4));
                float d = surface - y;
                clip(d);
                fixed4 c = lerp(_Shallow, _Deep, saturate(d / _DepthFade));
                // (three waves 120 degrees apart over a warped plane: where their sum crosses zero, a wavy net of light)
                float wx = x + 0.35 * sin(y * 3.1 + t * 0.9);
                float wy = y + 0.35 * sin(x * 2.7 - t * 0.8);
                float s3 = sin(6.5 * wx + t * 1.1) + sin(6.5 * (-0.5 * wx + 0.866 * wy) - t * 0.9) + sin(6.5 * (-0.5 * wx - 0.866 * wy) + t * 0.7);
                float cau = pow(saturate(1.0 - abs(s3) / 0.9), 4.0) * exp(-d * 1.3);
                float ray = pow(saturate(sin(x * 0.85 + y * 0.32 + t * 0.2) * 0.5 + 0.5), 7.0) * exp(-d * 0.45) * 0.16;
                c.rgb += (cau * 0.35 + ray) * fixed3(0.8, 1.0, 1.0);
                float foam = 1.0 - smoothstep(0.035, 0.075, d);
                float glow = (1.0 - smoothstep(0.08, 0.35, d)) * 0.22;
                c.rgb = lerp(c.rgb + glow, _Foam.rgb, foam);
                c.a = max(c.a, foam * _Foam.a) * saturate(d / 0.012);
                return c;
            }
            ENDCG
        }
    }
}
