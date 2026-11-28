// Procedural anime cel-shaded 3D flame for Raja's campfire (CbCamp): bends and ripples in 3D vertex space while
// upward-licking helical flame waves carve crisp orange, amber, and warm-gold cel bands without whiteout bloom or dark
// texture-cutout fringes. Two versions: URP (first SubShader) and Built-in pipeline (fallback).
Shader "Cloudborne/Campfire"
{
    Properties
    {
        // not drawn: kept so material helpers inspecting mainTexture never trip
        [HideInInspector] _MainTex ("Unused", 2D) = "white" {}
        _Cutoff ("Cutoff", Range(0, 1)) = 0.22
        _Phase ("Phase", Float) = 0
        _Speed ("Speed", Float) = 1.0
        _Lobes ("Tongues", Float) = 3.0
        _Sway ("Sway (m)", Float) = 0.08
        _IsCore ("Inner Core", Range(0, 1)) = 0
        _DeepCol ("Outer Crimson", Color) = (0.86, 0.14, 0.03, 1)
        _MidCol ("Flame Orange", Color) = (0.99, 0.36, 0.05, 1)
        _AmberCol ("Warm Amber", Color) = (1.0, 0.68, 0.10, 1)
        _CoreCol ("Hot Gold", Color) = (1.0, 0.92, 0.46, 1)
        _WhiteCol ("Bright Heart", Color) = (1.0, 0.97, 0.74, 1)
    }

    // ---------- Universal Render Pipeline ----------
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite On

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _DeepCol;
                half4 _MidCol;
                half4 _AmberCol;
                half4 _CoreCol;
                half4 _WhiteCol;
                half _Cutoff;
                float _Phase;
                float _Speed;
                float _Lobes;
                float _Sway;
                half _IsCore;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float vv = v.uv.y;
                float th = v.uv.x * 6.2831853;
                float t = _Time.y * _Speed;
                float w = pow(max(vv, 0.0001), 1.35);

                // Radial ripple keeps even the lower rim organically alive instead of tracing a rigid cone silhouette.
                float ripple = 1.0 + 0.16 * sin(th * _Lobes + vv * 5.0 - t * 5.6 + _Phase) * sin(vv * 3.14159265);
                float breath = 1.0 + 0.11 * sin(t * 7.8 - vv * 4.5 + _Phase);

                float3 pos = v.positionOS.xyz;
                pos.xz *= ripple * breath;
                pos.x += _Sway * w * (0.70 * sin(vv * 6.5 - t * 6.0 + _Phase) + 0.35 * cos(vv * 10.0 - t * 8.4 + _Phase * 1.5));
                pos.z += _Sway * 0.70 * w * (0.70 * cos(vv * 5.8 - t * 5.3 + _Phase * 1.2) + 0.30 * sin(vv * 9.0 - t * 7.1));
                pos.y += pos.y * 0.08 * w * sin(t * 9.0 + _Phase);

                o.world = TransformObjectToWorld(pos);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.world);
                o.uv = v.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float v = i.uv.y;
                float th = i.uv.x * 6.2831853;

                // Seamless circular flame waves climbing upward and splitting into licks and tiny detached embers.
                float w1 = sin(th * _Lobes + v * 5.5 - t * 5.4 + _Phase);
                float w2 = sin(th * (_Lobes + 2.0) - v * 8.5 - t * 8.0 + _Phase * 1.7) * 0.62;
                float w3 = sin(th * 2.0 + v * 13.0 - t * 11.8 - _Phase * 0.9) * 0.40;
                float turb = (w1 + w2 + w3) / 2.02;

                float density = (1.06 - v * 1.18) + turb * (0.14 + 0.44 * v);
                if (v > 0.60)
                {
                    float wisp = sin(th * 3.0 + v * 16.0 - t * 13.8 + _Phase) * cos(th * 2.0 - v * 11.0 + t * 4.2);
                    density += max(0.0, wisp - 0.50) * 0.98 * (1.0 - v * 0.85);
                }
                clip(density - _Cutoff);

                // Facing regions show hotter gold/amber; grazing silhouette rims stay deep red-orange.
                float3 vd = normalize(_WorldSpaceCameraPos.xyz - i.world);
                float edgeFactor = pow(saturate(abs(dot(normalize(i.normalWS), vd))), 0.68);
                float heat = density * (0.55 + 0.45 * edgeFactor) - v * 0.14;

                half3 outerCol = lerp(_DeepCol.rgb, _MidCol.rgb, (half)smoothstep(0.28, 0.33, heat));
                outerCol = lerp(outerCol, _AmberCol.rgb, (half)smoothstep(0.48, 0.53, heat));
                outerCol = lerp(outerCol, _CoreCol.rgb, (half)smoothstep(0.70, 0.75, heat));

                half3 coreCol = lerp(_MidCol.rgb, _AmberCol.rgb, (half)smoothstep(0.26, 0.31, heat));
                coreCol = lerp(coreCol, _CoreCol.rgb, (half)smoothstep(0.42, 0.47, heat));
                coreCol = lerp(coreCol, _WhiteCol.rgb, (half)smoothstep(0.62, 0.67, heat));

                half3 col = lerp(outerCol, coreCol, _IsCore);
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }
    }

    // ---------- Built-in render pipeline (fallback) ----------
    SubShader
    {
        Tags { "Queue"="AlphaTest" "RenderType"="TransparentCutout" "IgnoreProjector"="True" }
        Cull Off
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _DeepCol, _MidCol, _AmberCol, _CoreCol, _WhiteCol;
            half _Cutoff, _IsCore;
            float _Phase, _Speed, _Lobes, _Sway;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 world : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float vv = v.uv.y;
                float th = v.uv.x * 6.2831853;
                float t = _Time.y * _Speed;
                float w = pow(max(vv, 0.0001), 1.35);

                float ripple = 1.0 + 0.16 * sin(th * _Lobes + vv * 5.0 - t * 5.6 + _Phase) * sin(vv * 3.14159265);
                float breath = 1.0 + 0.11 * sin(t * 7.8 - vv * 4.5 + _Phase);

                float3 pos = v.vertex.xyz;
                pos.xz *= ripple * breath;
                pos.x += _Sway * w * (0.70 * sin(vv * 6.5 - t * 6.0 + _Phase) + 0.35 * cos(vv * 10.0 - t * 8.4 + _Phase * 1.5));
                pos.z += _Sway * 0.70 * w * (0.70 * cos(vv * 5.8 - t * 5.3 + _Phase * 1.2) + 0.30 * sin(vv * 9.0 - t * 7.1));
                pos.y += pos.y * 0.08 * w * sin(t * 9.0 + _Phase);

                o.world = mul(unity_ObjectToWorld, float4(pos, 1)).xyz;
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.pos = UnityWorldToClipPos(o.world);
                o.uv = v.uv;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float t = _Time.y * _Speed;
                float v = i.uv.y;
                float th = i.uv.x * 6.2831853;

                float w1 = sin(th * _Lobes + v * 5.5 - t * 5.4 + _Phase);
                float w2 = sin(th * (_Lobes + 2.0) - v * 8.5 - t * 8.0 + _Phase * 1.7) * 0.62;
                float w3 = sin(th * 2.0 + v * 13.0 - t * 11.8 - _Phase * 0.9) * 0.40;
                float turb = (w1 + w2 + w3) / 2.02;

                float density = (1.06 - v * 1.18) + turb * (0.14 + 0.44 * v);
                if (v > 0.60)
                {
                    float wisp = sin(th * 3.0 + v * 16.0 - t * 13.8 + _Phase) * cos(th * 2.0 - v * 11.0 + t * 4.2);
                    density += max(0.0, wisp - 0.50) * 0.98 * (1.0 - v * 0.85);
                }
                clip(density - _Cutoff);

                float3 vd = normalize(_WorldSpaceCameraPos.xyz - i.world);
                float edgeFactor = pow(saturate(abs(dot(normalize(i.normalWS), vd))), 0.68);
                float heat = density * (0.55 + 0.45 * edgeFactor) - v * 0.14;

                fixed3 outerCol = lerp(_DeepCol.rgb, _MidCol.rgb, (fixed)smoothstep(0.28, 0.33, heat));
                outerCol = lerp(outerCol, _AmberCol.rgb, (fixed)smoothstep(0.48, 0.53, heat));
                outerCol = lerp(outerCol, _CoreCol.rgb, (fixed)smoothstep(0.70, 0.75, heat));

                fixed3 coreCol = lerp(_MidCol.rgb, _AmberCol.rgb, (fixed)smoothstep(0.26, 0.31, heat));
                coreCol = lerp(coreCol, _CoreCol.rgb, (fixed)smoothstep(0.42, 0.47, heat));
                coreCol = lerp(coreCol, _WhiteCol.rgb, (fixed)smoothstep(0.62, 0.67, heat));

                fixed3 col = lerp(outerCol, coreCol, _IsCore);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
