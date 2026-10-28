// Cartoon (cel) shading in the style of kart racers:
// banded lighting with tinted shadows, a crisp specular highlight, rim light and an optional outline.
// Two versions: URP (first SubShader) and the built-in pipeline (fallback). Receives and casts shadows, supports fog.
Shader "RacingGame/Toon"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _ShadowColor ("Shadow Tint", Color) = (0.55,0.6,0.9,1)
        _Bands ("Light Bands", Range(1,4)) = 2
        _BandSoftness ("Band Softness", Range(0.001,0.3)) = 0.05
        _SpecSize ("Highlight Size", Range(0,1)) = 0.25
        _SpecStrength ("Highlight Strength", Range(0,1)) = 0.35
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,1)) = 0.3
        _OutlineColor ("Outline Color", Color) = (0.07,0.06,0.12,1)
        _OutlineWidth ("Outline Width", Range(0,5)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        _EmissionMap ("Emission Mask", 2D) = "white" {}
    }

    // ---------- Universal Render Pipeline ----------
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Same constant buffer in every pass (SRP Batcher compatible).
        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _ShadowColor;
            half _Bands;
            half _BandSoftness;
            half _SpecSize;
            half _SpecStrength;
            half4 _RimColor;
            half _RimPower;
            half _RimStrength;
            half4 _OutlineColor;
            half _OutlineWidth;
            half4 _EmissionColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_EmissionMap);

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
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;

                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 l = light.direction;

                float ndl = dot(n, l);
                float lit = saturate(ndl) * light.shadowAttenuation;

                // Quantise the light into soft-edged bands.
                float bands = max(_Bands, 1.0);
                float q = lit * bands;
                float band = floor(q) + smoothstep(0.5 - _BandSoftness, 0.5 + _BandSoftness, frac(q));
                float toon = saturate(band / bands);

                float3 lightCol = lerp(_ShadowColor.rgb, float3(1, 1, 1), toon) * light.color;
                float3 ambient = SampleSH(n) * 0.6;

                // Crisp "plastic toy" highlight.
                float3 h = normalize(l + v);
                float specEdge = 1.0 - _SpecSize * 0.08;
                float spec = smoothstep(specEdge - 0.005, specEdge + 0.005, saturate(dot(n, h))) * _SpecStrength * toon;

                // Rim light, stronger on the lit side.
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength * saturate(ndl * 0.5 + 0.6);

                float3 col = albedo.rgb * (lightCol + ambient) + spec * light.color + rim * _RimColor.rgb;
                // Glow (HDR, picked up by bloom), masked by a texture so only parts of a surface can light up.
                col += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1);
            }
            ENDHLSL
        }

        // Inverted-hull outline. URP draws an "SRPDefaultUnlit" pass in addition to "UniversalForward".
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float4 clipPos = TransformObjectToHClip(v.positionOS.xyz);
                float3 viewNormal = TransformWorldToViewDir(TransformObjectToWorldNormal(v.normalOS));
                float2 offset = mul((float2x2)UNITY_MATRIX_P, viewNormal.xy);
                float len = length(offset);
                offset = len > 0.0001 ? offset / len : float2(0, 0);
                offset.x *= _ScreenParams.y / _ScreenParams.x;   // keep the width even on wide screens
                clipPos.xy += offset * (_OutlineWidth * 0.006) * clipPos.w;
                // Width 0 disables the outline (degenerate triangles are not drawn).
                o.positionCS = clipPos * step(0.0001, _OutlineWidth);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                return half4(MixFog(_OutlineColor.rgb, i.fogFactor), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 vert (Attributes v) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - positionWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4 vert (float4 positionOS : POSITION) : SV_POSITION { return TransformObjectToHClip(positionOS.xyz); }
            half4 frag () : SV_Target { return 0; }
            ENDHLSL
        }
    }

    // ---------- Built-in render pipeline (fallback) ----------
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }

        Pass
        {
            Name "TOON"
            Tags { "LightMode"="ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _ShadowColor;
            half _Bands;
            half _BandSoftness;
            half _SpecSize;
            half _SpecStrength;
            fixed4 _RimColor;
            half _RimPower;
            half _RimStrength;
            sampler2D _EmissionMap;
            fixed4 _EmissionColor;

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
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                SHADOW_COORDS(3)
                UNITY_FOG_COORDS(4)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                float ndl = dot(n, l);
                float shadow = SHADOW_ATTENUATION(i);
                float lit = saturate(ndl) * shadow;

                // Quantise the light into soft-edged bands.
                float bands = max(_Bands, 1.0);
                float q = lit * bands;
                float band = floor(q) + smoothstep(0.5 - _BandSoftness, 0.5 + _BandSoftness, frac(q));
                float toon = saturate(band / bands);

                float3 lightCol = lerp(_ShadowColor.rgb, float3(1, 1, 1), toon) * _LightColor0.rgb;
                float3 ambient = ShadeSH9(float4(n, 1)) * 0.6;

                // Crisp "plastic toy" highlight.
                float3 h = normalize(l + v);
                float specEdge = 1.0 - _SpecSize * 0.08;
                float spec = smoothstep(specEdge - 0.005, specEdge + 0.005, saturate(dot(n, h))) * _SpecStrength * toon;

                // Rim light, stronger on the lit side.
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength * saturate(ndl * 0.5 + 0.6);

                float3 col = albedo.rgb * (lightCol + ambient) + spec * _LightColor0.rgb + rim * _RimColor.rgb;
                col += tex2D(_EmissionMap, i.uv).rgb * _EmissionColor.rgb;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, 1);
            }
            ENDCG
        }

        // Inverted-hull outline: back faces pushed out along the normal in screen space.
        Pass
        {
            Name "OUTLINE"
            Cull Front
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            half _OutlineWidth;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_FOG_COORDS(0)
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 clipPos = UnityObjectToClipPos(v.vertex);
                float3 viewNormal = mul((float3x3)UNITY_MATRIX_IT_MV, v.normal);
                float2 offset = TransformViewToProjection(viewNormal.xy);
                float len = length(offset);
                offset = len > 0.0001 ? offset / len : float2(0, 0);
                offset.x *= _ScreenParams.y / _ScreenParams.x;   // keep the width even on wide screens
                clipPos.xy += offset * (_OutlineWidth * 0.006) * clipPos.w;
                // Width 0 disables the outline (degenerate triangles are not drawn).
                o.pos = clipPos * step(0.0001, _OutlineWidth);
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = _OutlineColor;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }

        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback "Diffuse"
}
