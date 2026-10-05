// Shader del mapa estratégico (URP).
// - Relieve: la malla se desplaza poco y de forma suave (suelo apto para unidades); la sensación de
//   montaña la dan las normales por píxel, una oclusión de valles y el detalle de terreno.
// - Agua ligera: dos capas de normales precalculadas que se desplazan, reflejo, brillo del sol y espuma.
// - Capa política, fronteras suaves de grosor constante, día/noche con luces de ciudad (núcleo + halo).
Shader "GrandStrategy/MapTerrain"
{
    Properties
    {
        _HeightTex ("Height", 2D) = "black" {}
        _ColorMap ("Color Map", 2D) = "white" {}
        _ProvinceIdTex ("Province Ids", 2D) = "black" {}
        _ProvinceDataTex ("Province Data", 2D) = "black" {}
        _ProvinceOwnerTex ("Province Owners", 2D) = "black" {}
        _CityLightsTex ("City Lights", 2D) = "black" {}
        _DetailTex ("Detail Noise", 2D) = "gray" {}
        _WaterNormalTex ("Water Normals", 2D) = "bump" {}

        _MapWorldSize ("Map World Size", Vector) = (200, 100, 0, 0)
        _HeightScale ("Height Scale (geometría)", Float) = 1.4
        _VertexHeightLod ("Vertex Height Mip", Float) = 2
        _ReliefHeight ("Relief Height (sombreado)", Float) = 4.5
        _SeaLevel ("Sea Level", Range(0, 1)) = 0.4
        _NormalStrength ("Normal Strength", Float) = 2.5
        _Zoom ("Zoom (0 cerca, 1 lejos)", Range(0, 1)) = 0
        _OverlayStrength ("Overlay Strength", Range(0, 1)) = 1

        _ShallowWater ("Shallow Water", Color) = (0.16, 0.40, 0.46, 1)
        _DeepWater ("Deep Water", Color) = (0.03, 0.10, 0.19, 1)
        _HighlightColor ("Highlight", Color) = (1, 0.85, 0.45, 1)
        _CountryBorderWidth ("Country Border Width (px)", Float) = 2.4
        _ProvinceBorderWidth ("Province Border Width (px)", Float) = 1
        _SunInfo ("Sun (lon rad, decl rad, night strength)", Vector) = (0, 0, 0, 0)
        _Projection ("Projection (yTop, yBottom, latTop, latBottom)", Vector) = (0, 0, 1.5708, -1.5708)
        _ProjectionMode ("Projection Mode (0 lineal, 1 Miller)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        // Flags del canal alfa de _ProvinceDataTex (ver MapRenderer.cs).
        #define FLAG_HOVER_PROVINCE    1u
        #define FLAG_HOVER_COUNTRY     2u
        #define FLAG_SELECTED_COUNTRY  4u
        #define FLAG_SELECTED_PROVINCE 8u
        #define FLAG_PLAYER_COUNTRY    16u

        TEXTURE2D(_HeightTex);       SAMPLER(sampler_HeightTex);
        TEXTURE2D(_ColorMap);        SAMPLER(sampler_ColorMap);
        TEXTURE2D(_ProvinceIdTex);
        TEXTURE2D(_ProvinceDataTex);
        TEXTURE2D(_ProvinceOwnerTex);
        TEXTURE2D(_CityLightsTex);   SAMPLER(sampler_CityLightsTex);
        TEXTURE2D(_DetailTex);       SAMPLER(sampler_DetailTex);
        TEXTURE2D(_WaterNormalTex);  SAMPLER(sampler_WaterNormalTex);

        // Globales de la capa de nubes (MapDecorations): x = visibilidad, y = altura, z = fuerza de sombra.
        float4 _GS_Clouds;
        // Calidad del terreno (1 bajo, 2 medio, 3 alto) y texturas de detalle / uso del suelo (MapDecorations).
        float _GS_TerrainQuality;
        TEXTURE2D(_GS_GroundDetailTex); SAMPLER(sampler_GS_GroundDetailTex);
        TEXTURE2D(_GS_LandUseTex);      SAMPLER(sampler_GS_LandUseTex);

        float GSHash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }
        float _GS_CloudTime;

        CBUFFER_START(UnityPerMaterial)
            float4 _HeightTex_TexelSize;
            float4 _ColorMap_TexelSize;
            float4 _ProvinceIdTex_TexelSize;
            float4 _ProvinceDataTex_TexelSize;
            float4 _ProvinceOwnerTex_TexelSize;
            float4 _CityLightsTex_TexelSize;
            float4 _DetailTex_TexelSize;
            float4 _WaterNormalTex_TexelSize;
            float4 _MapWorldSize;
            float _HeightScale;
            float _VertexHeightLod;
            float _ReliefHeight;
            float _SeaLevel;
            float _NormalStrength;
            float _Zoom;
            float _OverlayStrength;
            float4 _ShallowWater;
            float4 _DeepWater;
            float4 _HighlightColor;
            float _CountryBorderWidth;
            float _ProvinceBorderWidth;
            float4 _SunInfo;
            float4 _Projection;
            float _ProjectionMode;
        CBUFFER_END

        float LandHeight(float h)
        {
            return saturate((h - _SeaLevel) / (1.0 - _SeaLevel));
        }

        // Altura de la malla: se lee de un mip suavizado para que no haya picos afilados
        // (igual que MapRenderer.GetTerrainHeight en C#).
        float3 DisplaceVertex(float3 positionOS, float2 uv)
        {
            float h = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, _VertexHeightLod).r;
            positionOS.y += LandHeight(h) * _HeightScale;
            return positionOS;
        }

        // Filtrado bicúbico (B-spline) con 4 lecturas bilineales: de cerca cada texel del mapa ocupa decenas
        // de píxeles y el filtrado bilineal deja "rombos" y escalones visibles; la B-spline es continua.
        void CubicWeights(float2 uv, float4 texelSize, out float2 p0, out float2 p1, out float2 g0, out float2 g1)
        {
            float2 st = uv * texelSize.zw - 0.5;
            float2 i = floor(st);
            float2 f = st - i;
            float2 f2 = f * f, f3 = f2 * f;
            float2 w0 = (-f3 + 3.0 * f2 - 3.0 * f + 1.0) / 6.0;
            float2 w1 = (3.0 * f3 - 6.0 * f2 + 4.0) / 6.0;
            float2 w2 = (-3.0 * f3 + 3.0 * f2 + 3.0 * f + 1.0) / 6.0;
            float2 w3 = f3 / 6.0;
            g0 = w0 + w1;
            g1 = w2 + w3;
            p0 = (i - 0.5 + w1 / g0) * texelSize.xy;
            p1 = (i + 1.5 + w3 / g1) * texelSize.xy;
        }

        float HeightCubic(float2 uv)
        {
            float2 p0, p1, g0, g1;
            CubicWeights(uv, _HeightTex_TexelSize, p0, p1, g0, g1);
            return g0.y * (g0.x * SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, p0, 0).r
                         + g1.x * SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, float2(p1.x, p0.y), 0).r)
                 + g1.y * (g0.x * SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, float2(p0.x, p1.y), 0).r
                         + g1.x * SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, p1, 0).r);
        }

        float3 ColorCubic(float2 uv)
        {
            float2 p0, p1, g0, g1;
            CubicWeights(uv, _ColorMap_TexelSize, p0, p1, g0, g1);
            return g0.y * (g0.x * SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_ColorMap, p0, 0).rgb
                         + g1.x * SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_ColorMap, float2(p1.x, p0.y), 0).rgb)
                 + g1.y * (g0.x * SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_ColorMap, float2(p0.x, p1.y), 0).rgb
                         + g1.x * SAMPLE_TEXTURE2D_LOD(_ColorMap, sampler_ColorMap, p1, 0).rgb);
        }

        // ¿Filtrado bicúbico? Solo de cerca y con calidad media o alta (cuesta 4 veces más lecturas).
        bool UseCubic() { return _Zoom < 0.3 && _GS_TerrainQuality > 1.5; }

        float HeightAt(float2 uv)
        {
            if (UseCubic()) return HeightCubic(uv);
            return SAMPLE_TEXTURE2D(_HeightTex, sampler_HeightTex, uv).r;
        }

        // Normal del relieve por píxel; usa una altura "virtual" (_ReliefHeight) independiente de la
        // geometría, así las montañas se leen bien aunque la malla sea casi plana.
        float3 TerrainNormal(float2 uv, out float hC)
        {
            float2 ts = _HeightTex_TexelSize.xy;
            hC = HeightAt(uv);
            float hL = HeightAt(uv - float2(ts.x, 0));
            float hR = HeightAt(uv + float2(ts.x, 0));
            float hD = HeightAt(uv - float2(0, ts.y));
            float hU = HeightAt(uv + float2(0, ts.y));
            float2 texelWorld = _MapWorldSize.xy * ts;
            float k = _ReliefHeight * _NormalStrength;
            return normalize(float3(
                -(LandHeight(hR) - LandHeight(hL)) * k / (2.0 * texelWorld.x),
                1.0,
                -(LandHeight(hU) - LandHeight(hD)) * k / (2.0 * texelWorld.y)));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vpi = GetVertexPositionInputs(DisplaceVertex(input.positionOS.xyz, input.uv));
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(vpi.positionCS.z);
                return o;
            }

            // ---------------------------------------------------------------- Provincias
            struct Corner
            {
                uint id;
                uint owner;
                float water;
                float4 data;
            };

            Corner FetchCorner(int2 texel)
            {
                int2 size = int2(_ProvinceIdTex_TexelSize.zw);
                texel.x = (texel.x % size.x + size.x) % size.x;   // el mapa da la vuelta de este a oeste
                texel.y = clamp(texel.y, 0, size.y - 1);
                float2 rg = LOAD_TEXTURE2D(_ProvinceIdTex, texel).rg;
                Corner c;
                c.id = (uint)(rg.r * 255.0 + 0.5) + ((uint)(rg.g * 255.0 + 0.5) << 8);
                int2 lookup = int2(c.id & 255u, c.id >> 8);
                c.data = LOAD_TEXTURE2D(_ProvinceDataTex, lookup);
                float4 o = LOAD_TEXTURE2D(_ProvinceOwnerTex, lookup);
                c.owner = (uint)(o.r * 255.0 + 0.5) + ((uint)(o.g * 255.0 + 0.5) << 8);
                c.water = o.b;
                return c;
            }

            // Latitud (radianes) a partir de la coordenada v del mapa.
            float LatitudeFromV(float v)
            {
                if (_ProjectionMode > 0.5)
                {
                    float y = lerp(_Projection.y, _Projection.x, v);
                    return 2.5 * atan(exp(0.8 * y)) - 0.625 * PI;
                }
                return lerp(_Projection.w, _Projection.z, v);
            }

            // Anchura de línea en píxeles de pantalla a partir de una "distancia" d (>0 dentro de la región).
            float BorderAlpha(float W, float widthPx)
            {
                float d = W - 0.5;
                float px = d / max(fwidth(d), 1e-4);
                return saturate(widthPx * 0.5 - px + 0.5);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 uv = i.uv;
                // Coordenadas de mundo a partir de la UV: idénticas en las copias este/oeste del mapa, así las
                // texturas de detalle no "saltan" cuando la cámara da la vuelta al mundo.
                float2 wp = uv * _MapWorldSize.xy;
                float t = fmod(_Time.y, 1000.0); // acotado: evita pérdida de precisión en sesiones largas

                // ------------------------------------------------------------ Relieve y luz
                float hC;
                float3 n = TerrainNormal(uv, hC);
                Light mainLight = GetMainLight();
                float3 L = normalize(mainLight.direction);
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);
                float3 H = normalize(L + V);
                float ndl = saturate(dot(n, L));

                // Oclusión de valles: compara la altura con su entorno suavizado (mip alto).
                float hBlur = SAMPLE_TEXTURE2D_LOD(_HeightTex, sampler_HeightTex, uv, 4).r;
                float cavity = saturate(1.0 + (LandHeight(hC) - LandHeight(hBlur)) * 6.0);

                // ------------------------------------------------------------ Terreno
                // Calidad baja: sin grano de detalle.
                float detailFade = (1.0 - smoothstep(0.2, 0.65, _Zoom)) * step(1.5, _GS_TerrainQuality);
                float4 d1 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 0.35);
                float4 d2 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 2.1);
                float grain = d1.r * 0.55 + d2.g * 0.45;
                float3 albedo;
                if (UseCubic())
                {
                    // Deforma ligeramente la UV (menos de un texel) para que los límites entre colores del
                    // mapa sean orgánicos en lugar de seguir la rejilla de píxeles.
                    float2 warpN = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 6.0).rg
                                 + SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 19.0).gb * 0.5 - 0.75;
                    albedo = ColorCubic(uv + warpN * _ColorMap_TexelSize.xy * 1.4);
                }
                else albedo = SAMPLE_TEXTURE2D(_ColorMap, sampler_ColorMap, uv).rgb;
                albedo *= lerp(1.0, 0.8 + 0.4 * grain, detailFade);
                // Lagos pequeños del mapa de color (no son provincias de agua): azul oscuro con reflejo.
                float brightC = dot(albedo, float3(0.333, 0.333, 0.333));
                // (valores lineales: los lagos del mapa son casi negros, la taiga más oscura ronda 0.009)
                float lake = saturate((0.0065 - brightC) * 300.0) * step(_SeaLevel, hC);
                albedo = lerp(albedo, float3(0.05, 0.12, 0.17), lake);
                // Laderas empinadas: tono rocoso.
                float slope = saturate((1.0 - n.y) * 5.0);
                albedo = lerp(albedo, albedo * float3(0.86, 0.83, 0.8) + 0.035, slope * 0.55);

                float sparkle = 0.0;
                // ---------------------------------------------------------- Detalle de cerca (calidad alta)
                if (_GS_TerrainQuality > 2.5)
                {
                    float near = (1.0 - smoothstep(0.0, 0.25, _Zoom)) * (1.0 - lake);
                    if (near > 0.001)
                    {
                        // Frecuencias enteras por vuelta al mundo (sin costura en el antimeridiano).
                        float2 gp = wp * 12.0;
                        float4 g1 = SAMPLE_TEXTURE2D(_GS_GroundDetailTex, sampler_GS_GroundDetailTex, gp);
                        float4 g2 = SAMPLE_TEXTURE2D(_GS_GroundDetailTex, sampler_GS_GroundDetailTex, gp * 3.0 + 0.37);
                        float4 g = g1 * 0.75 + g2 * 0.25;
                        float landE = LandHeight(hC);
                        float meters = pow(landE, 1.0 / 0.85) * 6375.0;          // ver build_world_map.py
                        float latDeg = abs(LatitudeFromV(uv.y)) * 57.2958;
                        float4 n1 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 1.2);
                        float4 n2 = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 4.0 + 0.31);

                        // Material del suelo deducido del color: hierba, roca, arena, tierra.
                        float bright = dot(albedo, float3(0.333, 0.333, 0.333));
                        float green = saturate((albedo.g - max(albedo.r, albedo.b)) * 10.0);
                        float sand = saturate((albedo.r - albedo.b) * 3.0 - green) * saturate(bright * 4.0 - 0.3);
                        float rock = saturate(smoothstep(0.25, 0.65, slope) + saturate((meters - 2200.0) / 1800.0) * 0.6);

                        // Nieve: línea de nieves según la latitud (≈4900 m en el ecuador, ≈1500 m a 60°), con ruido
                        // para un borde irregular; las paredes muy empinadas no retienen nieve. También el hielo
                        // que ya trae el mapa (Groenlandia, Antártida, glaciares).
                        // El relieve del mapa está suavizado (las cumbres reales son más altas), así que las
                        // cimas que sobresalen de su entorno bajan su línea de nieves; las mesetas no.
                        float snowLine = max(5000.0 - max(latDeg - 25.0, 0.0) * 92.0, 250.0);
                        float metersAround = pow(LandHeight(hBlur), 1.0 / 0.85) * 6375.0;
                        float peak = max(meters - metersAround, 0.0) * 2.5;
                        // Ruido "ridged": la nieve se alarga en lenguas siguiendo crestas en vez de formar charcos.
                        float streak = 1.0 - abs(n2.g * 2.0 - 1.0);
                        float snowNoise = (n1.r - 0.5) * 350.0 + (streak - 0.5) * 500.0;
                        float snowAlt = saturate((meters + peak - snowLine + snowNoise) / 700.0 + 0.5) * step(0.02, landE);
                        float chroma = max(albedo.r, max(albedo.g, albedo.b)) - min(albedo.r, min(albedo.g, albedo.b));
                        float iceMap = saturate((bright - 0.42) * 2.2) * saturate(1.0 - chroma * 6.0)
                                     * saturate(max((latDeg - 55.0) / 12.0, (meters - 1400.0) / 900.0)); // solo frío o alto
                        float steep = smoothstep(0.55, 0.9, slope);
                        // Cantidad de nieve S (altitud + hielo del mapa); se reparte sobre un campo de crestas N
                        // multiescala: con poca nieve solo se cubren las crestas, con mucha solo asoma roca en
                        // los barrancos. Así la forma sigue un "relieve" en vez de ser una mancha lisa.
                        float snowBase = saturate(max(snowAlt, iceMap * 0.88)) * (1.0 - steep * 0.8);
                        float fineRidge = 1.0 - abs(SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 13.0 + 0.71).r * 2.0 - 1.0);
                        float ridgeBig = 1.0 - abs(n1.r * 2.0 - 1.0);
                        float ridgeField = ridgeBig * 0.55 + streak * 0.3 + fineRidge * 0.15;
                        float snowT = 1.0 - snowBase;
                        float snow = smoothstep(snowT - 0.05, snowT + 0.05, ridgeField) * step(0.02, snowBase);
                        rock *= 1.0 - snow * 0.7;

                        float4 w = float4(green, rock, sand, saturate(1.0 - green - sand - rock));
                        w /= max(dot(w, float4(1, 1, 1, 1)), 1e-3);
                        float detail = dot(w, g);

                        // Variación de color a media escala (manchas de vegetación más seca o más húmeda).
                        float patchV = n1.g * 0.6 + n2.r * 0.4;
                        albedo *= lerp(1.0, 0.82 + 0.36 * patchV, near * 0.8);
                        albedo = lerp(albedo, albedo * float3(1.12, 1.05, 0.8), saturate((patchV - 0.55) * 3.0) * green * near * 0.6);
                        albedo *= lerp(1.0, 0.92 + 0.16 * detail, near);

                        // Roca: estratos inclinados y fracturas en las laderas.
                        float strata = sin(meters * 0.012 + n2.b * 9.0 + n1.r * 4.0) * 0.5 + 0.5;
                        float3 rockCol = lerp(float3(0.30, 0.28, 0.25), float3(0.46, 0.43, 0.38), strata * 0.75 + g1.g * 0.25);
                        rockCol = lerp(rockCol, rockCol * albedo / max(bright, 0.05) * 0.33 + rockCol * 0.67, 0.5);
                        albedo = lerp(albedo, rockCol, rock * near * 0.75);

                        // El blanco que ya trae el mapa se sustituye por roca donde el borde roto dice "sin nieve"
                        // (si no, quedarían las manchas lisas del mapa de color).
                        float whiteness = saturate((bright - 0.35) * 2.5) * saturate(1.0 - chroma * 6.0);
                        albedo = lerp(albedo, rockCol * 0.9, whiteness * (1.0 - snow) * saturate(snowBase * 3.0) * near);
                        // Nieve con derivas de viento, algo azulada en las zonas en sombra.
                        float3 snowCol = float3(0.86, 0.9, 0.95) * (0.92 + 0.08 * g.b) * (0.95 + 0.05 * n2.r);
                        albedo = lerp(albedo, snowCol, snow * lerp(0.6, 1.0, near));

                        // Microrrelieve: crestas (ruido "ridged") en zonas montañosas + patrón del material.
                        float rugged = saturate(meters / 1500.0) * (0.35 + slope) + rock * 0.4;
                        const float e = 1.0 / 512.0;
                        float4 gx = SAMPLE_TEXTURE2D(_GS_GroundDetailTex, sampler_GS_GroundDetailTex, gp + float2(e, 0));
                        float4 gz = SAMPLE_TEXTURE2D(_GS_GroundDetailTex, sampler_GS_GroundDetailTex, gp + float2(0, e));
                        float3 dn = float3(-dot(w, gx - g1), 0, -dot(w, gz - g1)) * 3.0 * (1.0 - snow * 0.6);
                        const float re = 0.006;
                        float r0 = 1.0 - abs(n1.r * 2.0 - 1.0);
                        float rx = 1.0 - abs(SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, (wp + float2(re, 0)) * 1.2).r * 2.0 - 1.0);
                        float rz = 1.0 - abs(SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, (wp + float2(0, re)) * 1.2).r * 2.0 - 1.0);
                        float q0 = 1.0 - abs(n2.g * 2.0 - 1.0);
                        float qx = 1.0 - abs(SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, (wp + float2(re, 0)) * 4.0 + 0.31).g * 2.0 - 1.0);
                        float qz = 1.0 - abs(SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, (wp + float2(0, re)) * 4.0 + 0.31).g * 2.0 - 1.0);
                        dn += float3(-(rx - r0) - (qx - q0) * 0.5, 0, -(rz - r0) - (qz - q0) * 0.5) / re * 0.012 * rugged;
                        n = normalize(n + dn * near);
                        // Las crestas iluminadas y los barrancos oscuros refuerzan la lectura del relieve.
                        albedo *= lerp(1.0, 0.85 + 0.3 * (r0 * 0.7 + q0 * 0.3), rugged * near * 0.6);
                        float snowSparkle = step(0.992, GSHash(floor(wp * 700.0))) * snow * near;

                        // Campos de cultivo en parcelas (orientación variable por comarca) con setos entre ellas.
                        float2 lu = SAMPLE_TEXTURE2D(_GS_LandUseTex, sampler_GS_LandUseTex, uv).rg;
                        float plains = saturate(1.0 - rock * 1.5) * (1.0 - snow) * (1.0 - sand * 0.6) * saturate(1.2 - bright * 1.5);
                        float farmW = lu.r * plains * (1.0 - smoothstep(0.08, 0.3, _Zoom));
                        if (farmW > 0.01)
                        {
                            // Orientación y tamaño de parcela constantes por comarca, girados alrededor de su
                            // propio centro (girar coordenadas globales grandes produce muaré).
                            float2 region = floor(wp / 0.45);
                            float rh = GSHash(region + 17.3);
                            float ang = (rh - 0.5) * 1.6;
                            float2x2 R = float2x2(cos(ang), -sin(ang), sin(ang), cos(ang));
                            float2 rf = wp / 0.45 - region;
                            float2 local = (rf - 0.5) * 0.45;
                            float2 size = lerp(float2(0.026, 0.017), float2(0.045, 0.028), GSHash(region + 3.1));
                            float2 fp = mul(R, local) / size + rh * 7.0;
                            float2 cell = floor(fp);
                            float2 f = frac(fp);
                            float hsh = GSHash(cell + region * 13.0);
                            // Mayoría de prados y cultivos verdes; algo de trigo y tierra arada.
                            float3 crop = hsh < 0.18 ? float3(0.42, 0.34, 0.14)      // trigo
                                        : hsh < 0.5 ? float3(0.16, 0.24, 0.07)      // cultivo verde
                                        : hsh < 0.62 ? float3(0.24, 0.17, 0.1)      // tierra arada
                                        : float3(0.2, 0.26, 0.1);                    // prado
                            // Conserva el tono local del mapa para que los campos no parezcan un tablero.
                            crop = lerp(albedo, crop, 0.55) * (0.9 + 0.2 * g1.a) * (0.95 + 0.05 * sin(fp.y * 40.0 + hsh * 6.0));
                            float edge = min(min(f.x, 1.0 - f.x), min(f.y, 1.0 - f.y));
                            float hedge = 1.0 - smoothstep(0.015, 0.05, edge);
                            float3 field = lerp(crop, crop * 0.55, hedge * 0.6);
                            // Manchas: no todo el campo está cultivado; los bordes de comarca se difuminan.
                            float patches = smoothstep(0.38, 0.62, SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 0.7).r);
                            float rEdge = min(min(rf.x, 1.0 - rf.x), min(rf.y, 1.0 - rf.y));
                            farmW *= patches * smoothstep(0.0, 0.08, rEdge) * step(0.25, GSHash(cell + 9.1 + region));
                            albedo = lerp(albedo, field, saturate(farmW * 0.65));
                        }
                        // Suelo urbano bajo las ciudades.
                        albedo = lerp(albedo, float3(0.15, 0.14, 0.12) * (0.85 + 0.3 * g1.a), lu.g * 0.55 * (1.0 - smoothstep(0.1, 0.38, _Zoom)));
                        ndl = saturate(dot(n, L));
                        sparkle = snowSparkle * pow(saturate(dot(n, normalize(L + V))), 6.0);
                    }
                }

                float3 skyAmbient = float3(0.36, 0.40, 0.47);
                float3 groundAmbient = float3(0.28, 0.26, 0.22);
                float3 ambient = lerp(groundAmbient, skyAmbient, n.y * 0.5 + 0.5) * lerp(0.75, 1.0, cavity);
                float3 terrain = albedo * (mainLight.color * ndl * 0.9 + ambient);
                terrain += sparkle * mainLight.color * 0.6;
                terrain += lake * pow(saturate(dot(float3(0, 1, 0), H)), 80.0) * mainLight.color * 0.25;

                // ------------------------------------------------------------ Agua
                float depth = saturate((_SeaLevel - hC) / max(_SeaLevel, 1e-3));
                float3 water = lerp(_ShallowWater.rgb, _DeepWater.rgb, sqrt(depth));
                float waveFade = 1.0 - smoothstep(0.05, 0.3, _Zoom); // oleaje solo de cerca
                float2 n1 = SAMPLE_TEXTURE2D(_WaterNormalTex, sampler_WaterNormalTex, wp * 0.9 + float2(t * 0.018, t * 0.011)).rg * 2.0 - 1.0;
                float2 n2 = SAMPLE_TEXTURE2D(_WaterNormalTex, sampler_WaterNormalTex, wp * 2.3 + float2(-t * 0.014, t * 0.024)).rg * 2.0 - 1.0;
                float2 nxy = (n1 + n2 * 0.6) * 0.32 * waveFade;
                float3 wn = normalize(float3(nxy.x, 1.0, nxy.y));
                float fresnel = 0.03 + 0.35 * pow(1.0 - saturate(dot(V, wn)), 5.0);
                float3 skyColor = float3(0.48, 0.58, 0.68) * (0.6 + 0.4 * mainLight.color);
                water *= 0.86 + 0.14 * saturate(dot(wn, L) * 1.5);
                water = lerp(water, skyColor, fresnel * 0.5);
                float glint = pow(saturate(dot(wn, H)), 300.0) * 0.28 + pow(saturate(dot(wn, H)), 48.0) * 0.02;
                water += glint * mainLight.color * waveFade;
                // Aguas someras algo más claras.
                water = lerp(water, water * 1.15 + float3(0.01, 0.02, 0.02), (1.0 - smoothstep(0.0, 0.05, depth)) * 0.18);

                float aa = max(fwidth(hC), 1e-5);
                float landMask = smoothstep(_SeaLevel - aa, _SeaLevel + aa, hC);
                float coastLine = 1.0 - saturate(abs(hC - _SeaLevel) / (aa * 1.5));
                // Espuma: banda fina pegada a la costa (en píxeles de pantalla), animada y solo de cerca.
                float coastPx = (_SeaLevel - hC) / aa;
                float foamBand = (1.0 - smoothstep(1.0, 7.0, coastPx)) * step(0.0, coastPx);
                float foamTex = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 2.2 + float2(t * 0.03, -t * 0.02)).b;
                float foam = saturate((foamTex - 0.42 + 0.25 * sin(t * 1.6 - coastPx * 0.9)) * 2.5) * foamBand * waveFade;
                water = lerp(water, float3(0.8, 0.86, 0.88), foam * 0.32);

                // ------------------------------------------------------------ Provincias (4 muestras)
                float2 p = uv * _ProvinceIdTex_TexelSize.zw;
                float2 fw = fwidth(p);
                float s = max(1.0, max(fw.x, fw.y) * 0.75); // huella de muestreo al alejar
                float2 q = p / s - 0.5;
                float2 b = floor(q);
                float2 f = q - b;

                Corner c[4];
                c[0] = FetchCorner(int2((b + float2(0.5, 0.5)) * s));
                c[1] = FetchCorner(int2((b + float2(1.5, 0.5)) * s));
                c[2] = FetchCorner(int2((b + float2(0.5, 1.5)) * s));
                c[3] = FetchCorner(int2((b + float2(1.5, 1.5)) * s));
                float wgt[4];
                wgt[0] = (1.0 - f.x) * (1.0 - f.y);
                wgt[1] = f.x * (1.0 - f.y);
                wgt[2] = (1.0 - f.x) * f.y;
                wgt[3] = f.x * f.y;

                // Región dominante: la provincia con más peso bilineal. Da contornos suavizados.
                int R = 0;
                float bestW = -1.0;
                [unroll] for (int a = 0; a < 4; a++)
                {
                    float sum = 0.0;
                    [unroll] for (int j = 0; j < 4; j++) sum += (c[j].id == c[a].id) ? wgt[j] : 0.0;
                    if (sum > bestW) { bestW = sum; R = a; }
                }
                Corner r = c[R];
                bool regionWater = r.water > 0.5;

                // Pesos para fronteras: la costa no cuenta como frontera (ya se dibuja con el relieve).
                float wProv = 0.0, wCountry = 0.0;
                [unroll] for (int m = 0; m < 4; m++)
                {
                    bool otherWater = c[m].water > 0.5;
                    bool sameProv = c[m].id == r.id || (otherWater != regionWater);
                    bool sameCountry = c[m].owner == r.owner || otherWater || regionWater;
                    wProv += sameProv ? wgt[m] : 0.0;
                    wCountry += sameCountry ? wgt[m] : 0.0;
                }

                uint flags = (uint)(r.data.a * 255.0 + 0.5);

                // ------------------------------------------------------------ Capa política
                float3 land = terrain;
                if (!regionWater)
                {
                    // De cerca predomina el terreno (como en HOI4); el color del país se intensifica al alejar.
                    float overlay = _OverlayStrength * lerp(0.16, 0.88, smoothstep(0.04, 0.75, _Zoom));
                    float3 modeColor = r.data.rgb * (0.6 + 0.4 * ndl) * lerp(0.85, 1.0, cavity)
                                       * lerp(1.0, 0.9 + 0.2 * grain, detailFade);
                    land = lerp(terrain, modeColor, overlay);

                    float pulse = 0.5 + 0.5 * sin(t * 3.0);
                    if ((flags & FLAG_SELECTED_COUNTRY) != 0u) land = lerp(land, _HighlightColor.rgb, 0.16 + 0.08 * pulse);
                    if ((flags & FLAG_HOVER_COUNTRY) != 0u) land = lerp(land, float3(1, 1, 1), 0.12);
                    // Provincia bajo el cursor: relleno tenue + borde claro (se ve sin parecer otro modo de mapa).
                    if ((flags & FLAG_HOVER_PROVINCE) != 0u) land = lerp(land, float3(1, 1, 1), 0.09);
                    if ((flags & FLAG_SELECTED_PROVINCE) != 0u) land = lerp(land, _HighlightColor.rgb, 0.25);

                    // Fronteras de provincia: finas, desaparecen al alejar.
                    float provAlpha = BorderAlpha(wProv, _ProvinceBorderWidth) * (1.0 - smoothstep(0.35, 0.65, _Zoom)) * 0.45;
                    land = lerp(land, land * 0.35, provAlpha);

                    // Fronteras nacionales: color oscuro del país; dorado si está seleccionado.
                    float countryAlpha = BorderAlpha(wCountry, _CountryBorderWidth);
                    float3 borderCol = ((flags & FLAG_SELECTED_COUNTRY) != 0u) ? _HighlightColor.rgb : r.data.rgb * 0.22;
                    land = lerp(land, borderCol, countryAlpha * 0.9);

                    // Borde interior de la provincia bajo el cursor: visible a cualquier zoom, por encima de las fronteras.
                    if ((flags & FLAG_HOVER_PROVINCE) != 0u)
                        land = lerp(land, float3(1.0, 0.95, 0.82), BorderAlpha(wProv, 2.6) * 0.85);
                }
                else
                {
                    // Fronteras entre zonas marinas, muy suaves y solo de cerca.
                    float seaAlpha = BorderAlpha(wProv, 1.0) * (1.0 - smoothstep(0.2, 0.5, _Zoom)) * 0.18;
                    water = lerp(water, float3(0.75, 0.85, 0.9), seaAlpha);
                    if ((flags & FLAG_HOVER_PROVINCE) != 0u) water = lerp(water, float3(0.6, 0.8, 0.9), 0.12);
                }

                // ------------------------------------------------------------ Composición
                float3 col = lerp(water, land, landMask);
                col = lerp(col, float3(0.08, 0.09, 0.1), coastLine * 0.7);

                // Sombras de las nubes (mismo patrón que la capa de nubes, proyectado según el sol).
                if (_GS_Clouds.x * _GS_Clouds.z > 0.001)
                {
                    float2 shadowPos = wp + L.xz / max(L.y, 0.25) * _GS_Clouds.y;
                    float2 cp = shadowPos / _MapWorldSize.x;
                    float ct = _GS_CloudTime;
                    float cn = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, cp * 3.0 + float2(ct * 0.0021, ct * 0.0007)).r * 0.55
                             + SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, cp * 8.0 + float2(ct * 0.0035, -ct * 0.0012)).b * 0.3
                             + SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, cp * 21.0 + float2(ct * 0.005, ct * 0.002)).g * 0.15;
                    col *= 1.0 - smoothstep(0.52, 0.82, cn) * _GS_Clouds.z * _GS_Clouds.x;
                }

                // Bruma atmosférica hacia el horizonte (solo se nota con la cámara inclinada).
                float dist = length(GetCameraPositionWS() - i.positionWS);
                float haze = saturate((dist - 20.0) / 260.0) * 0.3 * (1.0 - _Zoom * 0.7);
                col = lerp(col, float3(0.52, 0.6, 0.7) * (0.55 + 0.45 * mainLight.color), haze);

                // Textura "papel" muy sutil al alejar (estilo mapa estratégico).
                float paper = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, uv * 160.0).r;
                col *= lerp(1.0, 0.93 + 0.1 * paper, smoothstep(0.6, 1.0, _Zoom));

                // ------------------------------------------------------------ Día y noche
                float latR = LatitudeFromV(uv.y);
                float lonR = (uv.x * 2.0 - 1.0) * PI;
                float cosZ = sin(latR) * sin(_SunInfo.y) + cos(latR) * cos(_SunInfo.y) * cos(lonR - _SunInfo.x);
                float night = (1.0 - smoothstep(-0.10, 0.06, cosZ)) * _SunInfo.z;
                col = lerp(col, col * float3(0.14, 0.17, 0.28) + float3(0.004, 0.007, 0.018), night);
                // Luces: se encienden poco a poco desde el atardecer; núcleo nítido + halo (mip alto).
                float dusk = (1.0 - smoothstep(-0.04, 0.12, cosZ)) * _SunInfo.z;
                float core = SAMPLE_TEXTURE2D_LOD(_CityLightsTex, sampler_CityLightsTex, uv, 0).r;
                float glow = SAMPLE_TEXTURE2D_LOD(_CityLightsTex, sampler_CityLightsTex, uv, 2).r;
                // De cerca la ciudad se ve como un cúmulo de puntos de luz; de lejos, como una mancha cálida.
                float speck = SAMPLE_TEXTURE2D(_DetailTex, sampler_DetailTex, wp * 16.0).g;
                float specks = lerp(1.0, 0.65 + smoothstep(0.45, 0.62, speck) * 0.7, detailFade);
                // De muy cerca se reduce el halo (ya se ven los edificios y sus ventanas).
                float lights = (pow(core, 1.6) * 1.4 * specks + glow * 0.22 * (1.0 - 0.75 * detailFade)) * landMask * dusk;
                col += lights * float3(1.0, 0.68, 0.32);
                col += float3(0.08, 0.035, 0.0) * exp(-cosZ * cosZ * 4000.0) * _SunInfo.z * 0.15 * landMask; // crepúsculo

                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        // Profundidad (necesaria para el depth texture, SSAO, etc.).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(DisplaceVertex(input.positionOS.xyz, input.uv));
                return o;
            }

            half DepthFrag(DepthVaryings input) : SV_Target
            {
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // Profundidad + normales (la usa el SSAO del renderer).
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            struct DNAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct DNVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            DNVaryings DepthNormalsVert(DNAttributes input)
            {
                DNVaryings o;
                o.positionCS = TransformObjectToHClip(DisplaceVertex(input.positionOS.xyz, input.uv));
                o.uv = input.uv;
                return o;
            }

            half4 DepthNormalsFrag(DNVaryings input) : SV_Target
            {
                float hC;
                float3 normalWS = TerrainNormal(input.uv, hC);
                if (hC < _SeaLevel) normalWS = float3(0, 1, 0);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                return half4(PackFloat2To888(remapped), 0.0);
            #else
                return half4(normalWS, 0.0);
            #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
