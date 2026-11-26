// The top of the jungle's clear streams (CbWorld), seen from just above: the painted ripples under a body of turquoise
// water that flows towards the camera, rippling as it goes. Seen flatter it mirrors more of the bright sky; near the far
// bank it mirrors the jungle's green; sun glints where the ripples catch the light; white foam along the banks and where
// the stream comes out of the ground at the back; at the front a little see-through, the bed showing under it.
// (The painted surface only slid along, flat: the user.)
// Two versions: URP (first SubShader) and the built-in pipeline (fallback).
Shader "Cloudborne/WaterTop"
{
    Properties
    {
        _MainTex ("Painted ripples", 2D) = "white" {}
        _TexAmount ("Painted ripples' share", Range(0, 1)) = 0.35
        _Body ("Water", Color) = (0.18, 0.72, 0.86, 1)
        _Deep ("Water far off", Color) = (0.06, 0.45, 0.66, 1)
        _Sky ("Sky mirrored", Color) = (0.86, 0.96, 1, 1)
        _Trees ("Jungle mirrored", Color) = (0.16, 0.42, 0.3, 1)
        _Foam ("Foam", Color) = (1, 1, 1, 0.95)
        _FrontAlpha ("See-through at the front", Range(0, 1)) = 0.62
        _Flow ("Flow (m/s towards the camera)", Float) = 0.35
        _X0 ("Left bank (world)", Float) = 0
        _X1 ("Right bank (world)", Float) = 1
        _Z0 ("Front edge (world)", Float) = -1.7
        _Z1 ("Back, where it comes out (world)", Float) = 6.5
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
                float4 _MainTex_ST;
                half _TexAmount;
                half4 _Body;
                half4 _Deep;
                half4 _Sky;
                half4 _Trees;
                half4 _Foam;
                half _FrontAlpha;
                float _Flow;
                float _X0;
                float _X1;
                float _Z0;
                float _Z1;
            CBUFFER_END
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.world = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y;
                float3 p = i.world;
                float3 view = normalize(_WorldSpaceCameraPos - p);
                // seen flat, the water mirrors more
                float fres = pow(1.0 - saturate(view.y), 3.0);
                // ripples carried along with the flow, towards the camera
                float u = p.x, w = p.z + t * _Flow;
                float r = 0.5 * sin(u * 3.1 + sin(w * 1.7) * 1.2 + t * 0.7) + 0.35 * sin(w * 4.3 + u * 1.1) + 0.15 * sin((u + w) * 7.7 - t * 1.9);
                float back = saturate((p.z - _Z0) / max(0.01, _Z1 - _Z0));
                half3 c = lerp(_Body.rgb, _Deep.rgb, (half)back);
                c = lerp(c, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb, _TexAmount);
                // the jungle mirrored near the far bank, broken by the ripples; the sky mirrored where it is seen flat
                c = lerp(c, _Trees.rgb, (half)(smoothstep(0.45, 0.95, back) * (0.45 + 0.2 * r)));
                c = lerp(c, _Sky.rgb, (half)(fres * (0.45 + 0.15 * r)));
                c *= (half)(1.0 + 0.1 * r);
                // sun glints where the ripples catch the light
                c += (half)(pow(saturate(r * 0.5 + 0.5), 16.0) * 0.9);
                // foam along the banks and where it comes out of the ground at the back
                float bank = min(p.x - _X0, _X1 - p.x);
                float foam = 1.0 - smoothstep(0.04, 0.3 + 0.1 * sin(w * 5.0 + t * 2.0), bank);
                foam = max(foam, 1.0 - smoothstep(0.1, 0.7 + 0.15 * sin(u * 4.0 + t * 3.0), _Z1 - p.z));
                foam *= saturate(0.65 + 0.35 * sin(u * 9.0 + w * 7.0 + t * 2.5));
                c = lerp(c, _Foam.rgb, (half)foam);
                half a = (half)lerp(_FrontAlpha, 0.96, saturate((p.z - _Z0) / 2.0));
                a = max(a, (half)(foam * _Foam.a));
                return half4(c, a);
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half _TexAmount, _FrontAlpha;
            fixed4 _Body, _Deep, _Sky, _Trees, _Foam;
            float _Flow, _X0, _X1, _Z0, _Z1;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; float3 world : TEXCOORD1; };

            v2f vert (appdata v)
            {
                v2f o;
                o.world = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y;
                float3 p = i.world;
                float3 view = normalize(_WorldSpaceCameraPos - p);
                float fres = pow(1.0 - saturate(view.y), 3.0);
                float u = p.x, w = p.z + t * _Flow;
                float r = 0.5 * sin(u * 3.1 + sin(w * 1.7) * 1.2 + t * 0.7) + 0.35 * sin(w * 4.3 + u * 1.1) + 0.15 * sin((u + w) * 7.7 - t * 1.9);
                float back = saturate((p.z - _Z0) / max(0.01, _Z1 - _Z0));
                fixed3 c = lerp(_Body.rgb, _Deep.rgb, back);
                c = lerp(c, tex2D(_MainTex, i.uv).rgb, _TexAmount);
                c = lerp(c, _Trees.rgb, smoothstep(0.45, 0.95, back) * (0.45 + 0.2 * r));
                c = lerp(c, _Sky.rgb, fres * (0.45 + 0.15 * r));
                c *= 1.0 + 0.1 * r;
                c += pow(saturate(r * 0.5 + 0.5), 16.0) * 0.9;
                float bank = min(p.x - _X0, _X1 - p.x);
                float foam = 1.0 - smoothstep(0.04, 0.3 + 0.1 * sin(w * 5.0 + t * 2.0), bank);
                foam = max(foam, 1.0 - smoothstep(0.1, 0.7 + 0.15 * sin(u * 4.0 + t * 3.0), _Z1 - p.z));
                foam *= saturate(0.65 + 0.35 * sin(u * 9.0 + w * 7.0 + t * 2.5));
                c = lerp(c, _Foam.rgb, foam);
                fixed a = lerp(_FrontAlpha, 0.96, saturate((p.z - _Z0) / 2.0));
                a = max(a, foam * _Foam.a);
                return fixed4(c, a);
            }
            ENDCG
        }
    }
}
