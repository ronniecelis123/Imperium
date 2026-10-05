// Follaje de calidad alta (árboles, arbustos, hierba) con GPU instancing.
// Técnica habitual en juegos: "tarjetas" de hojas con textura recortada por alfa (alpha test), normales
// esféricas para un sombreado suave de la copa, translucidez a contraluz y viento en el vertex shader.
// UV < 0 = geometría sin textura (tronco, núcleo de la copa). Color: rgb = oclusión/tinte, a = peso del viento.
Shader "GrandStrategy/MapFoliage"
{
    Properties
    {
        _MainTex ("Atlas de follaje", 2D) = "white" {}
        _Cutoff ("Recorte alfa", Range(0, 1)) = 0.42
        _FadeStart ("Zoom en que empieza a ocultarse", Float) = 0.22
        _FadeEnd ("Zoom en que desaparece", Float) = 0.32
        _Wind ("Viento", Float) = 0.06
        _ColorVariation ("Variación de color por objeto", Float) = 0.16
        _Translucency ("Translucidez", Float) = 0.45
        _CopyOffsetX ("Desplazamiento de copia (este/oeste)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "Queue" = "AlphaTest" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GSCommon.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Cutoff;
            float _FadeStart;
            float _FadeEnd;
            float _Wind;
            float _ColorVariation;
            float _Translucency;
            float _CopyOffsetX;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

        float3 PlaceInstance(float3 positionOS, float windWeight, out float2 anchorUV, out float random)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            anchorUV = float2(frac(origin.x / _GS_MapWorldSize.x), origin.z / _GS_MapWorldSize.y);
            random = Hash(origin.xz * 7.31);
            float grow = 1.0 - smoothstep(_FadeStart, _FadeEnd, _GS_Zoom);
            float3 p = positionOS * grow;
            float t = _Time.y;
            // Balanceo general (crece con la altura) + aleteo rápido de las hojas.
            float sway = sin(t * 1.5 + random * 6.28) * _Wind * p.y * p.y;
            float flutter = sin(t * 6.5 + dot(positionOS, float3(13.1, 7.7, 11.3))) * _Wind * 0.18 * windWeight;
            p.xz += float2(sway, sway * 0.6) + flutter;
            p.y += flutter * 0.5;
            float3 world = TransformObjectToWorld(p);
            world.y += GS_GroundHeight(anchorUV);
            world.x += _CopyOffsetX;
            return world;
        }

        // Alfa con cobertura conservada a distancia (los mipmaps tienden a "comerse" las hojas).
        float FoliageAlpha(float2 uv)
        {
            float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
            float2 dx = ddx(uv * 1024.0), dy = ddy(uv * 1024.0);
            float mip = 0.5 * log2(max(max(dot(dx, dx), dot(dy, dy)), 1e-6));
            return a * (1.0 + max(mip, 0.0) * 0.28);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float2 anchorUV : TEXCOORD3;
                float3 positionWS : TEXCOORD4;
                float random : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionWS = PlaceInstance(v.positionOS.xyz, v.color.a, o.anchorUV, o.random);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 albedo;
                bool textured = i.uv.x >= 0.0;
                if (textured)
                {
                    float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                    clip(FoliageAlpha(i.uv) - _Cutoff);
                    albedo = tex.rgb * i.color.rgb * 1.25;
                }
                else albedo = i.color.rgb;

                // Variación por árbol: algo más claro/oscuro y, a veces, más amarillento.
                float v = (i.random - 0.5) * 2.0 * _ColorVariation;
                albedo *= 1.0 + v;
                albedo = lerp(albedo, albedo * float3(1.15, 1.05, 0.7), step(0.82, i.random) * 0.5);

                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 L = normalize(light.direction);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float wrap = saturate(dot(n, L) * 0.6 + 0.4);
                float3 ambient = lerp(float3(0.22, 0.24, 0.2), float3(0.38, 0.44, 0.48), n.y * 0.5 + 0.5);
                float3 col = albedo * (light.color * wrap * 0.85 + ambient);
                // Translucidez: las hojas brillan cuando el sol está detrás.
                if (textured) col += albedo * light.color * pow(saturate(dot(-L, V)), 4.0) * _Translucency;

                col = lerp(col, col * float3(0.14, 0.17, 0.28), GS_Night(i.anchorUV, 0.0));
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            struct DVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DVaryings vertDepth(Attributes v)
            {
                DVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float2 a; float r;
                o.positionCS = TransformWorldToHClip(PlaceInstance(v.positionOS.xyz, v.color.a, a, r));
                o.uv = v.uv;
                return o;
            }

            half fragDepth(DVaryings i) : SV_Target
            {
                if (i.uv.x >= 0.0) clip(FoliageAlpha(i.uv) - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing

            struct DNVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            DNVaryings vertDN(Attributes v)
            {
                DNVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float2 a; float r;
                o.positionCS = TransformWorldToHClip(PlaceInstance(v.positionOS.xyz, v.color.a, a, r));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                return o;
            }

            half4 fragDN(DNVaryings i) : SV_Target
            {
                if (i.uv.x >= 0.0) clip(FoliageAlpha(i.uv) - _Cutoff);
                return half4(normalize(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
