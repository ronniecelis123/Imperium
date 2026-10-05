// Ríos: cintas que siguen el relieve. Calidad alta: orillas con vegetación, centro más profundo,
// corriente con normales animadas y reflejo del sol. Calidad baja: color plano con bordes suaves.
// UV0 = posición en el mapa, UV1 = (lado -1..1, distancia a lo largo), UV2.x = semiancho,
// NORMAL = dirección perpendicular al cauce (para ensanchar la cinta al alejar la cámara).
Shader "GrandStrategy/MapRiver"
{
    Properties
    {
        _RiverColor ("Color", Color) = (0.24, 0.47, 0.57, 1)
        _DeepColor ("Color del centro", Color) = (0.12, 0.3, 0.4, 1)
        _BankColor ("Color de la orilla", Color) = (0.26, 0.3, 0.18, 1)
        _FadeStart ("Zoom en que empieza a ocultarse", Float) = 0.5
        _FadeEnd ("Zoom en que desaparece", Float) = 0.78
        _Detail ("Calidad (0 baja, 1 alta)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GSCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _RiverColor;
                float4 _DeepColor;
                float4 _BankColor;
                float _FadeStart;
                float _FadeEnd;
                float _Detail;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 mapUV : TEXCOORD0;
                float2 river : TEXCOORD1;
                float2 width : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 mapUV : TEXCOORD0;
                float2 river : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Más ancho al alejar para que siga viéndose (hasta que se desvanece). En calidad alta la cinta
                // incluye la orilla, así que es algo más ancha.
                float widen = lerp(1.0, 3.5, smoothstep(0.05, 0.6, _GS_Zoom)) * lerp(1.0, 1.35, _Detail);
                float3 p = v.positionOS.xyz + v.normalOS * v.river.x * v.width.x * widen;
                p.y = GS_GroundHeight(v.mapUV) + 0.012;
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.mapUV = v.mapUV;
                o.river = v.river;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = fmod(_Time.y, 1000.0);
                float side = abs(i.river.x);
                Light light = GetMainLight();
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, _GS_Zoom);
                float night = GS_Night(i.mapUV, 0.0);
                float3 col;
                float alpha;

                if (_Detail > 0.5)
                {
                    // Corriente: normales de agua desplazándose a lo largo del cauce.
                    float2 flowUV = float2(i.river.x * 0.08, i.river.y * 2.5 - t * 0.12);
                    float2 n1 = SAMPLE_TEXTURE2D(_GS_WaterNormalTex, sampler_GS_WaterNormalTex, flowUV).rg * 2.0 - 1.0;
                    float2 n2 = SAMPLE_TEXTURE2D(_GS_WaterNormalTex, sampler_GS_WaterNormalTex, flowUV * 2.3 + 0.37).rg * 2.0 - 1.0;
                    float3 n = normalize(float3((n1.x + n2.x) * 0.15, 1.0, (n1.y + n2.y) * 0.15));
                    float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                    float3 H = normalize(normalize(light.direction) + V);
                    float water = 1.0 - smoothstep(0.62, 0.78, side);          // ancho del agua dentro de la cinta
                    float3 waterCol = lerp(_DeepColor.rgb, _RiverColor.rgb, smoothstep(0.0, 0.7, side));
                    waterCol *= 0.8 + 0.2 * saturate(dot(n, normalize(light.direction)) * 1.5);
                    waterCol += pow(saturate(dot(n, H)), 160.0) * 0.25 * saturate(light.color) * (1.0 - smoothstep(0.0, 0.3, _GS_Zoom));
                    float3 bank = _BankColor.rgb * (0.6 + 0.4 * light.color);
                    col = lerp(bank, waterCol * (0.6 + 0.4 * light.color), water);
                    alpha = (water * 0.92 + (1.0 - water) * 0.55) * (1.0 - smoothstep(0.85, 1.0, side));
                }
                else
                {
                    col = _RiverColor.rgb * (0.55 + 0.45 * light.color);
                    alpha = (1.0 - smoothstep(0.55, 1.0, side)) * 0.85;
                }
                col = lerp(col, col * float3(0.14, 0.17, 0.28), night);
                // Valores acotados: un color fuera de rango se descontrola al mezclarse por transparencia.
                return half4(saturate(col), saturate(alpha * fade));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
