// A painting with its background cut away (grass and flower cards, the painted jungle layers behind the path): the
// texture as painted, unlit, the pixels whose alpha is below _Cutoff not drawn at all. _Color tints it; _Haze blends
// it towards _HazeColor, the farther the layer the more, so the layers recede into the backdrop's haze. _Sway (metres)
// sways the top of the painting in a breeze, its foot still (the grass and flower cards: the jungle stood still); the
// breeze rolls along the stage, so neighbours sway a little out of step.
// For the long painted strips (the hedge, the row of trees, the grass lip; a quad's top can't bend): _Wind sways the
// painting itself, its top the most and its foot not at all (in its own texture's width, gusts rolling along the
// stage), and _Dapple lets patches of sunlight drift slowly over its leaves. The ground's painted face darkens with
// depth (_Deep, reached _DeepOver metres under _ShadeTop) and lies in a soft shadow under the grass lip (_Ao), so it
// sits back behind the grass instead of reading as one flat picture.
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
        _Wind ("Wind (texture widths)", Float) = 0
        _Dapple ("Dappled light", Range(0, 0.5)) = 0
        _ShadeTop ("Shade from (world y)", Float) = 0
        _Deep ("Darkest with depth", Range(0, 1)) = 1
        _DeepOver ("Depth to the darkest (m)", Float) = 3
        _Ao ("Shadow under the top", Range(0, 1)) = 0
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
                float _Wind;
                half _Dapple;
                float _ShadeTop;
                half _Deep;
                float _DeepOver;
                half _Ao;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float up : TEXCOORD2; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 world = TransformObjectToWorld(v.positionOS.xyz);
                float t = _Time.y;
                world.x += _Sway * v.uv.y * v.uv.y * (0.7 * sin(t * 1.6 + world.x * 0.55 + world.z * 0.3) + 0.3 * sin(t * 2.7 + world.x * 1.3));
                o.positionCS = TransformWorldToHClip(world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.world = world;
                o.up = v.uv.y;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                // gusts rolling along the stage: the top of the painting sways, its foot stays
                float gust = 0.55 * sin(t * 1.3 + i.world.x * 0.35) + 0.3 * sin(t * 2.3 + i.world.x * 0.9) + 0.25 * sin(t * 0.45 - i.world.x * 0.11);
                uv.x += _Wind * i.up * i.up * gust;
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * _Color;
                clip(c.a - _Cutoff);
                // patches of sun drifting over the leaves, more near the top
                half sun = sin(i.world.x * 0.8 + t * 0.35 + i.world.y * 1.3) * sin(i.world.x * 0.31 - t * 0.22 + i.world.y * 0.7);
                c.rgb *= 1 + _Dapple * sun * (0.4 + 0.6 * i.up);
                // the ground's face: darker with depth, and in shadow just under the grass
                float below = max(0, _ShadeTop - i.world.y);
                c.rgb *= lerp(1, _Deep, saturate(below / _DeepOver)) * (1 - _Ao * exp(-below * 6) * step(0.0001, below));
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
            half _Haze, _Cutoff, _Dapple, _Deep, _Ao;
            float _Sway, _Wind, _ShadeTop, _DeepOver;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; float up : TEXCOORD2; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                world.x += _Sway * v.uv.y * v.uv.y * (0.7 * sin(t * 1.6 + world.x * 0.55 + world.z * 0.3) + 0.3 * sin(t * 2.7 + world.x * 1.3));
                o.pos = UnityWorldToClipPos(world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.world = world;
                o.up = v.uv.y;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float2 uv = i.uv;
                float gust = 0.55 * sin(t * 1.3 + i.world.x * 0.35) + 0.3 * sin(t * 2.3 + i.world.x * 0.9) + 0.25 * sin(t * 0.45 - i.world.x * 0.11);
                uv.x += _Wind * i.up * i.up * gust;
                fixed4 c = tex2D(_MainTex, uv) * _Color;
                clip(c.a - _Cutoff);
                half sun = sin(i.world.x * 0.8 + t * 0.35 + i.world.y * 1.3) * sin(i.world.x * 0.31 - t * 0.22 + i.world.y * 0.7);
                c.rgb *= 1 + _Dapple * sun * (0.4 + 0.6 * i.up);
                float below = max(0, _ShadeTop - i.world.y);
                c.rgb *= lerp(1, _Deep, saturate(below / _DeepOver)) * (1 - _Ao * exp(-below * 6) * step(0.0001, below));
                return fixed4(lerp(c.rgb, _HazeColor.rgb, _Haze), 1);
            }
            ENDCG
        }
    }
}
