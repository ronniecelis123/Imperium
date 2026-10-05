// Nombres de países sobre el mapa (estilo Paradox): letras repartidas sobre una curva que sigue la forma del
// país, apoyadas en el relieve. Cada letra conoce su altura en el mundo (uv1.x), si es de la versión normal o
// compacta del nombre (uv1.y) y el centro de su nombre (uv2):
// - normal: nunca se agranda; por encima de _SizeKnee px crece cada vez menos (_SizeCompression) y nunca pasa
//   de _MaxPixels (se reduce alrededor de su centro): los grandes siguen mayores, sin diferencias extremas;
// - compacta: se dibuja a un tamaño fijo de pantalla (_CompactPixels), algo más tenue.
// Qué versión se ve (o ninguna) lo decide CountryLabels y llega como opacidad por vértice (color.a).
// Los parámetros los fija CountryLabels desde sus constantes.
Shader "GrandStrategy/MapLabel"
{
    Properties
    {
        _MainTex ("Atlas de la fuente", 2D) = "black" {}
        _TextColor ("Color del texto", Color) = (0.08, 0.07, 0.06, 0.82)
        _HaloColor ("Color del halo", Color) = (0.96, 0.93, 0.85, 0.4)
        _NightTextColor ("Color del texto de noche", Color) = (0.92, 0.88, 0.78, 0.8)
        _NightHaloColor ("Color del halo de noche", Color) = (0, 0, 0, 0.45)
        _HaloRadius ("Radio del halo (texels del atlas)", Float) = 1.8
        _MaxPixels ("Altura máxima de las letras en pantalla (px)", Float) = 120
        _SizeKnee ("Tamaño a partir del que se comprime el crecimiento (px)", Float) = 24
        _SizeCompression ("Exponente de compresión del tamaño", Float) = 0.6
        _DayShadow ("Sombra suave de día", Float) = 0.55
        _CompactPixels ("Altura de las letras compactas en pantalla (px)", Float) = 9
        _CompactAlpha ("Opacidad de las etiquetas compactas", Float) = 0.85
        _HideZoomStart ("Zoom en que empieza a ocultarse", Float) = 0.36
        _HideZoomEnd ("Zoom en que desaparece", Float) = 0.27
        _CopyOffsetX ("Desplazamiento de copia (este/oeste)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "GSCommon.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_TexelSize;
                float4 _TextColor;
                float4 _HaloColor;
                float4 _NightTextColor;
                float4 _NightHaloColor;
                float _HaloRadius;
                float _MaxPixels;
                float _SizeKnee;
                float _SizeCompression;
                float _DayShadow;
                float _CompactPixels;
                float _CompactAlpha;
                float _HideZoomStart;
                float _HideZoomEnd;
                float _CopyOffsetX;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 info : TEXCOORD1; // x = altura de la letra en el mundo, y = 0 normal / 1 compacta
                float2 anchor : TEXCOORD2; // centro del nombre (x, z en espacio objeto)
                float4 color : COLOR;      // a = visibilidad por choques con otros nombres (la calcula CountryLabels)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fade : TEXCOORD1;
                float night : TEXCOORD2;
                float small : TEXCOORD3; // 1 = letras pequeñas en pantalla (compactas): algo más de trazo
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                // Tamaño aproximado de las letras en píxeles de pantalla, medido en el centro del nombre.
                float3 anchorWS = TransformObjectToWorld(float3(v.anchor.x, 0, v.anchor.y));
                anchorWS.x += _CopyOffsetX;
                float dist = max(length(GetCameraPositionWS() - anchorWS), 1e-3);
                // abs: al renderizar a una textura intermedia la proyección se invierte (P[1][1] negativo).
                float px = v.info.x * abs(UNITY_MATRIX_P[1][1]) / dist * _ScreenParams.y * 0.5;

                bool compact = v.info.y > 0.5;
                // Normal: tamaño propio, con tope. Compacta: tamaño fijo de pantalla.
                float shown = px <= _SizeKnee ? px : _SizeKnee * pow(max(px / _SizeKnee, 1.0), _SizeCompression);
                shown = min(shown, _MaxPixels);
                float scale = compact ? _CompactPixels / max(px, 1e-4) : shown / max(px, 1e-4);
                float3 posOS = v.positionOS.xyz;
                posOS.xz = v.anchor + (posOS.xz - v.anchor) * scale;

                float3 world = TransformObjectToWorld(posOS);
                float2 mapUV = float2(frac(world.x / _GS_MapWorldSize.x), saturate(world.z / _GS_MapWorldSize.y));
                world.y += GS_GroundHeight(mapUV) + 0.03;
                world.x += _CopyOffsetX;
                o.positionCS = TransformWorldToHClip(world);
                o.uv = v.uv;

                float zoomFade = smoothstep(_HideZoomEnd, _HideZoomStart, _GS_Zoom);
                o.fade = (compact ? _CompactAlpha : 1.0) * zoomFade * v.color.a;
                o.night = GS_Night(mapUV, 0.0);
                o.small = 1.0 - saturate((px * scale - _CompactPixels) / 8.0);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                if (i.fade < 0.002) discard;
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                // Halo suave en dos anillos (el atlas de la fuente no trae contorno): uno ceñido a la letra y otro
                // más ancho y tenue, para separar el texto de colores, nieve y nubes sin un contorno duro.
                float2 d = _MainTex_TexelSize.xy * _HaloRadius;
                float inner = 0.0, outer = 0.0;
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(d.x, 0)).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(d.x, 0)).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, d.y)).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(0, d.y)).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + d * 0.7).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - d * 0.7).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(d.x, -d.y) * 0.7).a);
                inner = max(inner, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(-d.x, d.y) * 0.7).a);
                float2 d2 = d * 2.0;
                outer = max(outer, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(d2.x, 0)).a);
                outer = max(outer, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(d2.x, 0)).a);
                outer = max(outer, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + float2(0, d2.y)).a);
                outer = max(outer, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(0, d2.y)).a);
                // Sombra suave desplazada abajo a la derecha, solo de día (sobre colores claros, nieve y nubes).
                float shadow = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - float2(d.x, -d.y) * 1.3).a;
                float halo = max(max(inner, outer * 0.4), shadow * _DayShadow * (1.0 - i.night));

                float4 textCol = lerp(_TextColor, _NightTextColor, i.night);
                float4 haloCol = lerp(_HaloColor, _NightHaloColor, i.night);
                // A pocos píxeles las letras se difuminan (mipmaps): se refuerza un poco el trazo y el halo, sin
                // llegar a opacidad total (las letras pequeñas no deben brillar más que las grandes).
                a = saturate(a * (1.0 + 0.5 * i.small));
                halo = saturate(halo * (1.0 + 0.5 * i.small));
                haloCol.a = saturate(haloCol.a * (1.0 + 0.3 * i.small));
                float textA = a * textCol.a;
                float haloA = halo * haloCol.a * (1.0 - a);
                float alpha = textA + haloA;
                float3 rgb = (textCol.rgb * textA + haloCol.rgb * haloA) / max(alpha, 1e-4);
                return half4(rgb, saturate(alpha) * i.fade);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
