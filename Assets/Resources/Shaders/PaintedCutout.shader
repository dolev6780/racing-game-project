// A painting with its background cut away (grass and flower cards, the painted jungle layers behind the path): the
// texture as painted, unlit, the pixels whose alpha is below _Cutoff not drawn at all. _Color tints it; _Haze blends
// it towards _HazeColor, the farther the layer the more, so the layers recede into the backdrop's haze. _Sway (metres)
// sways the top of the painting in a breeze, its foot still (the grass and flower cards: the jungle stood still); the
// breeze rolls along the stage, so neighbours sway a little out of step.
Shader "Cloudborne/PaintedCutout"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Haze", Color) = (0.5, 0.75, 0.8, 1)
        _Haze ("Haze amount", Range(0, 1)) = 0
        _Cutoff ("Cutoff", Range(0, 1)) = 0.5
        _Sway ("Sway (m)", Float) = 0
    }

    // ---------- Universal Render Pipeline ----------
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half4 _HazeColor;
                half _Haze;
                half _Cutoff;
                float _Sway;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y;
                world.x += _Sway * v.uv.y * v.uv.y * (0.7 * sin(t * 1.6 + world.x * 0.55 + world.z * 0.3) + 0.3 * sin(t * 2.7 + world.x * 1.3));
                o.positionCS = TransformWorldToHClip(world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff);
                return half4(lerp(c.rgb, _HazeColor.rgb, _Haze), 1);
            }
            ENDHLSL
        }
    }

    // ---------- Built-in render pipeline (fallback) ----------
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color, _HazeColor;
            half _Haze, _Cutoff;
            float _Sway;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                world.x += _Sway * v.uv.y * v.uv.y * (0.7 * sin(t * 1.6 + world.x * 0.55 + world.z * 0.3) + 0.3 * sin(t * 2.7 + world.x * 1.3));
                o.pos = UnityWorldToClipPos(world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv) * _Color;
                clip(c.a - _Cutoff);
                return fixed4(lerp(c.rgb, _HazeColor.rgb, _Haze), 1);
            }
            ENDCG
        }
    }
}
