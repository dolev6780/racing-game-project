// The painted far backdrop of a stage: its texture exactly as painted, with no lighting and no fog
// (the painting already has its own haze). It writes depth, so the sky is not drawn over it.
// _NextTex is the next part of the stage's painting, cross-faded in by _Blend (0 to 1).
Shader "Cloudborne/Backdrop"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _NextTex ("Next", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 0
    }

    // ---------- Universal Render Pipeline ----------
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Blend;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_NextTex);
            SAMPLER(sampler_NextTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb;
                half3 b = SAMPLE_TEXTURE2D(_NextTex, sampler_NextTex, i.uv).rgb;
                return half4(lerp(a, b, _Blend), 1);
            }
            ENDHLSL
        }
    }

    // ---------- Built-in render pipeline (fallback) ----------
    SubShader
    {
        Tags { "Queue"="Geometry+10" "RenderType"="Opaque" "IgnoreProjector"="True" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex, _NextTex;
            float4 _MainTex_ST;
            half _Blend;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(lerp(tex2D(_MainTex, i.uv).rgb, tex2D(_NextTex, i.uv).rgb, _Blend), 1);
            }
            ENDCG
        }
    }
}
