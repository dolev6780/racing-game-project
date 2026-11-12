// The waterfall (CbLevelWays): a sheet of water drawn in world space between _X0 and _X1, falling from _Top to _Bottom.
// Streaks of light water stream down it at their own speeds over a deeper blue, its sides ragged and wavering, a white
// curl of foam where it pours over the lip and a churn of white foam where it lands. Light and see-through enough to
// hint at the cave behind it. (It was a flat panel of the river's texture scrolling down: the user.)
// Two versions: URP (first SubShader) and the built-in pipeline (fallback).
Shader "Cloudborne/Waterfall"
{
    Properties
    {
        // not drawn: there so code that asks any material for its texture (mainTexture) gets none, not an error
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        _Light ("Light water", Color) = (0.62, 0.9, 1, 0.8)
        _Deep ("Deep water", Color) = (0.12, 0.46, 0.78, 0.72)
        _Foam ("Foam", Color) = (1, 1, 1, 0.95)
        _X0 ("Left edge (world)", Float) = 0
        _X1 ("Right edge (world)", Float) = 1
        _Top ("Lip height (world)", Float) = 5
        _Bottom ("Foot height (world)", Float) = 0
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
                half4 _Light;
                half4 _Deep;
                half4 _Foam;
                float _X0;
                float _X1;
                float _Top;
                float _Bottom;
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
                float w = max(0.01, _X1 - _X0);
                float u = (x - _X0) / w;
                float fall = _Top - y;            // metres below the lip
                float rise = y - _Bottom;         // metres above the foot
                // ragged sides that waver as it falls
                float side = 0.07 + 0.035 * sin(y * 3.1 + t * 5.0) + 0.02 * sin(y * 7.3 - t * 8.0);
                float edge = smoothstep(0.0, side, u) * smoothstep(0.0, side, 1.0 - u);
                clip(edge - 0.02);
                clip(fall + 0.15);
                // streaks: columns of light water, each streaming down at its own speed
                float col = sin(x * 29.0 + sin(x * 6.1) * 2.3) * 0.5 + 0.5;
                float s1 = sin((y + t * 6.0) * 2.4 + x * 11.0) * 0.5 + 0.5;
                float s2 = sin((y + t * 8.5) * 5.3 - x * 23.0) * 0.5 + 0.5;
                float streak = pow(col, 2.0) * (0.55 * s1 + 0.45 * s2);
                streak = smoothstep(0.35, 0.85, streak);
                half4 c = lerp(_Light, _Deep, (half)saturate(fall / 4.0) * 0.6h + (half)(1.0 - col) * 0.3h);
                c.rgb = lerp(c.rgb, _Light.rgb * 1.15h, (half)streak * 0.8h);
                // the lip: a white curl where it pours over
                float lip = 1.0 - smoothstep(0.0, 0.3 + 0.08 * sin(x * 13.0 + t * 4.0), fall);
                // the foot: a white churn where it lands, bubbling
                float churn = sin(x * 17.0 + t * 9.0) * sin(x * 7.0 - t * 6.0 + y * 9.0) * 0.5 + 0.5;
                float foot = (1.0 - smoothstep(0.0, 0.55 + 0.25 * churn, rise));
                float foam = saturate(max(lip, foot) + streak * 0.15);
                c.rgb = lerp(c.rgb, _Foam.rgb, (half)foam);
                c.a = lerp(c.a, _Foam.a, (half)foam) * (half)edge * (half)saturate((fall + 0.15) / 0.15);
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

            fixed4 _Light;
            fixed4 _Deep;
            fixed4 _Foam;
            float _X0;
            float _X1;
            float _Top;
            float _Bottom;

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
                float w = max(0.01, _X1 - _X0);
                float u = (x - _X0) / w;
                float fall = _Top - y;
                float rise = y - _Bottom;
                float side = 0.07 + 0.035 * sin(y * 3.1 + t * 5.0) + 0.02 * sin(y * 7.3 - t * 8.0);
                float edge = smoothstep(0.0, side, u) * smoothstep(0.0, side, 1.0 - u);
                clip(edge - 0.02);
                clip(fall + 0.15);
                float col = sin(x * 29.0 + sin(x * 6.1) * 2.3) * 0.5 + 0.5;
                float s1 = sin((y + t * 6.0) * 2.4 + x * 11.0) * 0.5 + 0.5;
                float s2 = sin((y + t * 8.5) * 5.3 - x * 23.0) * 0.5 + 0.5;
                float streak = pow(col, 2.0) * (0.55 * s1 + 0.45 * s2);
                streak = smoothstep(0.35, 0.85, streak);
                fixed4 c = lerp(_Light, _Deep, saturate(fall / 4.0) * 0.6 + (1.0 - col) * 0.3);
                c.rgb = lerp(c.rgb, _Light.rgb * 1.15, streak * 0.8);
                float lip = 1.0 - smoothstep(0.0, 0.3 + 0.08 * sin(x * 13.0 + t * 4.0), fall);
                float churn = sin(x * 17.0 + t * 9.0) * sin(x * 7.0 - t * 6.0 + y * 9.0) * 0.5 + 0.5;
                float foot = (1.0 - smoothstep(0.0, 0.55 + 0.25 * churn, rise));
                float foam = saturate(max(lip, foot) + streak * 0.15);
                c.rgb = lerp(c.rgb, _Foam.rgb, foam);
                c.a = lerp(c.a, _Foam.a, foam) * edge * saturate((fall + 0.15) / 0.15);
                return c;
            }
            ENDCG
        }
    }
}
