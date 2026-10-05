// Árboles, edificios y monumentos dibujados con GPU instancing (una malla, miles de copias).
// El origen de cada instancia es su "ancla" en el mapa: se apoya en el relieve igual que el terreno y
// crece/encoge suavemente según el zoom. Color por vértice; alfa = fachada con ventanas (luces de noche).
Shader "GrandStrategy/MapProps"
{
    Properties
    {
        _FadeStart ("Zoom en que empieza a ocultarse", Float) = 0.22
        _FadeEnd ("Zoom en que desaparece", Float) = 0.32
        _WindowGlow ("Brillo de ventanas", Float) = 2.4
        _WindowDensity ("Ventanas por unidad", Float) = 6
        _Wind ("Balanceo por viento", Float) = 0
        _ColorVariation ("Variación de color por objeto", Float) = 0.12
        _FacadeDetail ("Detalle de fachadas (0/1)", Float) = 0
        _CopyOffsetX ("Desplazamiento de copia (este/oeste)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "GSCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float _FadeStart;
            float _FadeEnd;
            float _WindowGlow;
            float _WindowDensity;
            float _Wind;
            float _ColorVariation;
            float _CopyOffsetX;
            float _FacadeDetail;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        float Hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

        // Devuelve la posición en mundo apoyada en el suelo; anchorUV/random se sacan del origen de la instancia.
        float3 PlaceInstance(float3 positionOS, out float2 anchorUV, out float random)
        {
            float3 origin = TransformObjectToWorld(float3(0, 0, 0));
            anchorUV = float2(frac(origin.x / _GS_MapWorldSize.x), origin.z / _GS_MapWorldSize.y);
            random = Hash(origin.xz * 7.31);
            float grow = 1.0 - smoothstep(_FadeStart, _FadeEnd, _GS_Zoom);
            float3 p = positionOS * grow;
            // Viento: la copa se mece; el tronco (abajo) apenas se mueve.
            float sway = sin(_Time.y * 1.6 + random * 6.28) * _Wind * p.y * p.y;
            p.xz += float2(sway, sway * 0.6);
            float3 world = TransformObjectToWorld(p);
            world.y += GS_GroundHeight(anchorUV);
            world.x += _CopyOffsetX;
            return world;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back

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
                float2 anchorUV : TEXCOORD2;
                float3 positionOS : TEXCOORD3;
                float3 normalOS : TEXCOORD4;
                float random : TEXCOORD5;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 world = PlaceInstance(v.positionOS.xyz, o.anchorUV, o.random);
                o.positionCS = TransformWorldToHClip(world);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.positionOS = v.positionOS.xyz;
                o.normalOS = v.normalOS;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                Light light = GetMainLight();
                float3 n = normalize(i.normalWS);
                float3 L = normalize(light.direction);
                float ndl = saturate(dot(n, L) * 0.85 + 0.15);
                float3 ambient = lerp(float3(0.26, 0.25, 0.22), float3(0.40, 0.44, 0.50), n.y * 0.5 + 0.5);
                float3 albedo = i.color.rgb * (1.0 + (i.random - 0.5) * 2.0 * _ColorVariation);
                float3 col = albedo * (light.color * ndl * 0.9 + ambient);

                float night = GS_Night(i.anchorUV, 0.0);
                col = lerp(col, col * float3(0.14, 0.17, 0.28), night);

                // Sombreado al pie de los muros (contacto con el suelo).
                col *= lerp(0.7, 1.0, saturate(i.positionOS.y * 4.0));

                // Ventanas en las fachadas (alfa): rejilla en coordenadas locales, algunas encendidas al azar.
                if (i.color.a > 0.5 && abs(i.normalOS.y) < 0.5)
                {
                    float u = abs(i.normalOS.z) > abs(i.normalOS.x) ? i.positionOS.x : i.positionOS.z;
                    float2 g = float2(u, i.positionOS.y) * _WindowDensity;
                    float2 cell = floor(g);
                    float2 f = frac(g);
                    float window = step(0.28, f.x) * step(f.x, 0.72) * step(0.3, f.y) * step(f.y, 0.78);
                    // Detalle de fachada: marco claro alrededor de la ventana, línea de forjado por planta y
                    // puertas oscuras en la planta baja.
                    float frame = step(0.22, f.x) * step(f.x, 0.78) * step(0.24, f.y) * step(f.y, 0.84) * (1.0 - window);
                    float floorLine = 1.0 - smoothstep(0.0, 0.06, f.y);
                    float door = step(cell.y, 0.5) * step(0.38, f.x) * step(f.x, 0.62) * step(f.y, 0.7) * step(0.5, Hash(cell + 3.7));
                    col = lerp(col, col * 1.18 + 0.02, frame * _FacadeDetail * 0.8);
                    col *= 1.0 - floorLine * 0.18 * _FacadeDetail;
                    col *= 1.0 - door * 0.55 * _FacadeDetail;
                    window *= 1.0 - door;
                    // De día: ventanas oscuras con un reflejo; de noche: algunas encendidas.
                    col *= 1.0 - window * 0.38 * (1.0 - night);
                    col += window * (1.0 - night) * 0.05 * step(0.6, f.y) * _FacadeDetail;
                    float lit = step(0.5, Hash(cell + i.random * 91.0));
                    col += window * lit * night * float3(1.0, 0.72, 0.36) * _WindowGlow;
                }
                // Tejas en los tejados inclinados.
                else if (i.color.a <= 0.5 && i.normalOS.y > 0.3 && i.normalOS.y < 0.97 && _FacadeDetail > 0.5)
                {
                    float rows = frac(i.positionOS.y * 26.0);
                    col *= 0.88 + 0.12 * smoothstep(0.0, 0.25, rows);
                }
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ColorMask R

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertDepth
            #pragma fragment fragDepth
            #pragma multi_compile_instancing

            float4 vertDepth(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float2 uv; float r;
                return TransformWorldToHClip(PlaceInstance(v.positionOS.xyz, uv, r));
            }

            half fragDepth(float4 pos : SV_POSITION) : SV_Target { return pos.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vertDN
            #pragma fragment fragDN
            #pragma multi_compile_instancing

            struct DNVaryings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNVaryings vertDN(Attributes v)
            {
                DNVaryings o;
                UNITY_SETUP_INSTANCE_ID(v);
                float2 uv; float r;
                o.positionCS = TransformWorldToHClip(PlaceInstance(v.positionOS.xyz, uv, r));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 fragDN(DNVaryings i) : SV_Target { return half4(normalize(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
    FallBack Off
}
