// Funciones compartidas por las decoraciones del mapa (ríos, árboles, edificios, nubes).
// Las variables _GS_* son globales y las publica MapRenderer / MapDecorations.
#ifndef GS_COMMON_INCLUDED
#define GS_COMMON_INCLUDED

TEXTURE2D(_GS_HeightTex);  SAMPLER(sampler_GS_HeightTex);
TEXTURE2D(_GS_DetailTex);  SAMPLER(sampler_GS_DetailTex);
float _GS_SeaLevel;
float _GS_HeightScale;
float _GS_VertexLod;
float _GS_Zoom;
float4 _GS_MapWorldSize;
float4 _GS_SunInfo;      // x lon subsolar (rad), y declinación (rad), z intensidad de la noche
float4 _GS_Projection;   // yTop, yBottom, latTop, latBottom
float _GS_ProjectionMode;
float4 _GS_Clouds;       // x visibilidad, y altura, z fuerza de sombra
float _GS_CloudTime;     // tiempo de las nubes: avanza con el reloj del juego (se para en pausa)
TEXTURE2D(_GS_WaterNormalTex); SAMPLER(sampler_GS_WaterNormalTex);

// Altura del suelo (unidades de mundo) igual que la malla del terreno.
float GS_GroundHeight(float2 mapUV)
{
    float h = SAMPLE_TEXTURE2D_LOD(_GS_HeightTex, sampler_GS_HeightTex, mapUV, _GS_VertexLod).r;
    return saturate((h - _GS_SeaLevel) / (1.0 - _GS_SeaLevel)) * _GS_HeightScale;
}

// 0 = día, 1 = noche (con transición suave en el crepúsculo).
float GS_Night(float2 mapUV, float soft)
{
    float lat;
    if (_GS_ProjectionMode > 0.5)
    {
        float y = lerp(_GS_Projection.y, _GS_Projection.x, mapUV.y);
        lat = 2.5 * atan(exp(0.8 * y)) - 0.625 * PI;
    }
    else lat = lerp(_GS_Projection.w, _GS_Projection.z, mapUV.y);
    float lon = (mapUV.x * 2.0 - 1.0) * PI;
    float cosZ = sin(lat) * sin(_GS_SunInfo.y) + cos(lat) * cos(_GS_SunInfo.y) * cos(lon - _GS_SunInfo.x);
    return (1.0 - smoothstep(-0.10 - soft, 0.06 + soft, cosZ)) * _GS_SunInfo.z;
}

// Densidad de nubes 0..1 en una posición del mundo (cíclica de este a oeste).
// scale: tamaño del patrón (1 = cúmulos normales; >1 = nubes altas más finas). Las escalas son enteras
// por vuelta al mundo para que el patrón no tenga costura en el antimeridiano.
float GS_CloudDensity(float2 worldXZ, float scale, float coverage)
{
    float t = _GS_CloudTime;
    float2 cp = worldXZ / _GS_MapWorldSize.x;
    float n = SAMPLE_TEXTURE2D(_GS_DetailTex, sampler_GS_DetailTex, cp * (3.0 * scale) + float2(t * 0.0021, t * 0.0007)).r * 0.55
            + SAMPLE_TEXTURE2D(_GS_DetailTex, sampler_GS_DetailTex, cp * (8.0 * scale) + float2(t * 0.0035, -t * 0.0012)).b * 0.3
            + SAMPLE_TEXTURE2D(_GS_DetailTex, sampler_GS_DetailTex, cp * (21.0 * scale) + float2(t * 0.005, t * 0.002)).g * 0.15; // bordes finos
    return smoothstep(0.52 - coverage, 0.82 - coverage, n);
}

#endif
