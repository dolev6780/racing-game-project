// Rainbow, semi-transparent "?" item box with a shimmering fresnel edge.
Shader "RacingGame/ItemBox"
{
    Properties
    {
        _Saturation ("Saturation", Range(0,1)) = 0.75
        _HueSpeed ("Hue Speed", Float) = 0.35
        _MinAlpha ("Center Alpha", Range(0,1)) = 0.25
        _EdgePower ("Edge Power", Range(0.5,6)) = 2
    }

    // ---------- Universal Render Pipeline ----------
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Saturation, _HueSpeed, _MinAlpha, _EdgePower;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            float3 hsv2rgb (float3 c)
            {
                float3 p = abs(frac(c.xxx + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                return c.z * lerp(float3(1, 1, 1), saturate(p - 1.0), c.y);
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float fresnel = pow(1.0 - saturate(dot(n, v)), _EdgePower);

                float hue = frac(i.positionWS.y * 0.25 + (i.positionWS.x + i.positionWS.z) * 0.08 + _Time.y * _HueSpeed + fresnel * 0.3);
                float3 col = hsv2rgb(float3(hue, _Saturation, 1.0));
                col = lerp(col, float3(1, 1, 1), fresnel * 0.6);
                float alpha = lerp(_MinAlpha, 0.95, fresnel);
                return half4(MixFog(col, i.fogFactor), alpha);
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
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            half _Saturation, _HueSpeed, _MinAlpha, _EdgePower;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float3 hsv2rgb (float3 c)
            {
                float3 p = abs(frac(c.xxx + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
                return c.z * lerp(float3(1, 1, 1), saturate(p - 1.0), c.y);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float fresnel = pow(1.0 - saturate(dot(n, v)), _EdgePower);

                float hue = frac(i.worldPos.y * 0.25 + (i.worldPos.x + i.worldPos.z) * 0.08 + _Time.y * _HueSpeed + fresnel * 0.3);
                float3 col = hsv2rgb(float3(hue, _Saturation, 1.0));
                col = lerp(col, float3(1, 1, 1), fresnel * 0.6);
                float alpha = lerp(_MinAlpha, 0.95, fresnel);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return fixed4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback "Transparent/Diffuse"
}
