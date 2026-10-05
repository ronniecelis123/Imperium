// Capa de nubes: un plano por encima del mapa con densidad procedural (cíclica de este a oeste).
// Se mueve con el reloj del juego (_GS_CloudTime). Calidad alta: sombreado interno (la parte de las nubes
// contraria al sol más oscura) y borde iluminado; se pueden apilar dos capas con escalas distintas.
Shader "GrandStrategy/MapClouds"
{
    Properties
    {
        _CloudColor ("Color", Color) = (0.96, 0.97, 1.0, 1)
        _Opacity ("Opacidad máxima", Range(0, 1)) = 0.45
        _Scale ("Escala del patrón (entera)", Float) = 1
        _Coverage ("Cobertura extra", Range(-0.2, 0.2)) = 0
        _Detail ("Calidad (0 baja, 1 alta)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+20" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "GSCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _CloudColor;
                float _Opacity;
                float _Scale;
                float _Coverage;
                float _Detail;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float d = GS_CloudDensity(i.positionWS.xz, _Scale, _Coverage);
                float2 mapUV = float2(frac(i.positionWS.x / _GS_MapWorldSize.x), saturate(i.positionWS.z / _GS_MapWorldSize.y));
                Light light = GetMainLight();
                float3 col = _CloudColor.rgb * (0.65 + 0.35 * light.color);
                if (_Detail > 0.5)
                {
                    // Sombreado interno: densidad un poco hacia el sol → si hay más nube allí, este punto queda en sombra.
                    float toSun = GS_CloudDensity(i.positionWS.xz + normalize(light.direction.xz + 1e-4) * 0.6, _Scale, _Coverage);
                    col *= lerp(1.0, 0.72, saturate(toSun - d * 0.5));
                    col += (1.0 - d) * 0.12 * light.color; // bordes finos más iluminados
                }
                else col *= lerp(1.0, 0.85, d);
                col = lerp(col, col * float3(0.12, 0.14, 0.22), GS_Night(mapUV, 0.05));
                float edges = smoothstep(0.0, 0.06, mapUV.y) * smoothstep(1.0, 0.94, mapUV.y);
                return half4(col, d * _Opacity * _GS_Clouds.x * edges);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
