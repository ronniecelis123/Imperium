// Caminos, ferrocarriles y calles (cintas sobre el relieve).
// _Kind: 0 = camino de tierra/macadán, 1 = ferrocarril (balasto, traviesas y raíles), 2 = calle pavimentada
// (con farolas que se encienden de noche). Mismos datos de malla que los ríos (ver RibbonBuilder).
Shader "GrandStrategy/MapRoad"
{
    Properties
    {
        _Kind ("Tipo (0 camino, 1 tren, 2 calle)", Float) = 0
        _RoadColor ("Color", Color) = (0.55, 0.48, 0.38, 1)
        _FadeStart ("Zoom en que empieza a ocultarse", Float) = 0.3
        _FadeEnd ("Zoom en que desaparece", Float) = 0.5
        _Widen ("Ensanchado al alejar", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-12" }

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
                float _Kind;
                float4 _RoadColor;
                float _FadeStart;
                float _FadeEnd;
                float _Widen;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 mapUV : TEXCOORD0;
                float2 road : TEXCOORD1;
                float2 width : TEXCOORD2;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 mapUV : TEXCOORD0;
                float2 road : TEXCOORD1;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float widen = lerp(1.0, _Widen, smoothstep(0.03, 0.45, _GS_Zoom));
                float3 p = v.positionOS.xyz + v.normalOS * v.road.x * v.width.x * widen;
                p.y = GS_GroundHeight(v.mapUV) + 0.009;
                o.positionCS = TransformObjectToHClip(p);
                o.mapUV = v.mapUV;
                o.road = v.road;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float side = abs(i.road.x);
                float along = i.road.y;
                Light light = GetMainLight();
                float night = GS_Night(i.mapUV, 0.0);
                float3 col;
                float alpha = 1.0 - smoothstep(0.7, 1.0, side);
                float glow = 0.0;

                if (_Kind < 0.5)
                {
                    // Camino: centro claro (rodadas) y bordes más oscuros.
                    float noise = SAMPLE_TEXTURE2D(_GS_DetailTex, sampler_GS_DetailTex, float2(along * 40.0, i.road.x * 0.3)).r;
                    col = _RoadColor.rgb * (0.85 + 0.25 * noise) * lerp(1.05, 0.75, smoothstep(0.3, 0.9, side));
                    alpha *= 0.85;
                }
                else if (_Kind < 1.5)
                {
                    // Ferrocarril: balasto, traviesas periódicas y dos raíles metálicos.
                    float sleeper = step(frac(along * 1100.0), 0.45) * (1.0 - smoothstep(0.62, 0.72, side));
                    float rail = 1.0 - smoothstep(0.03, 0.09, abs(side - 0.4));
                    col = float3(0.36, 0.33, 0.3);
                    col = lerp(col, float3(0.3, 0.22, 0.15), sleeper);
                    col = lerp(col, float3(0.62, 0.62, 0.6), rail);
                    alpha *= 0.95;
                }
                else
                {
                    // Calle: adoquinado gris con aceras más claras; farolas de noche.
                    col = lerp(float3(0.36, 0.35, 0.33), float3(0.56, 0.54, 0.5), smoothstep(0.55, 0.75, side));
                    float lamp = (1.0 - smoothstep(0.0, 0.12, abs(frac(along * 140.0) - 0.5))) * smoothstep(0.55, 0.8, side);
                    glow = lamp * night * 2.5;
                    alpha *= 0.9;
                }

                col *= 0.55 + 0.45 * light.color;
                col = lerp(col, col * float3(0.14, 0.17, 0.28), night);
                col += glow * float3(1.0, 0.75, 0.4);
                float fade = 1.0 - smoothstep(_FadeStart, _FadeEnd, _GS_Zoom);
                return half4(saturate(col) + glow * float3(1.0, 0.75, 0.4) * 0.5, saturate(alpha * fade));
            }
            ENDHLSL
        }
    }
    FallBack Off
}
