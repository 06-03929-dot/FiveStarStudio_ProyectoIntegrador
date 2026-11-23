// Shader de Murther: lo integra al estilo de tinta de los fondos dibujados a mano.
//  - Sombreado por bandas (luz / sombra) con un color de sombra morado-gris como el del escenario,
//    en vez del degradado suave y "plástico" de URP/Lit.
//  - Trama de líneas de tinta en la sombra (y trama cruzada en la sombra profunda), como el rayado
//    de las columnas y muros del fondo. Las líneas siguen al personaje (no se quedan fijas en pantalla).
//  - Contorno negro de grosor constante (casco invertido). Usa las normales suavizadas que
//    NormalesSuavesContorno.cs guarda en los tangentes: así no se abre en las esquinas duras del cuerpo.
//  - Borde de luz (rim) y brillo duro de tinta húmeda: separan el cuerpo negro del fondo negro.
// Pases de profundidad, normales y sombras incluidos para SSAO, el prepass y luces con sombra.
Shader "Five Star/Murther Tinta"
{
    Properties
    {
        [Header(Color)]
        _BaseColor("Color base", Color) = (0.86, 0.85, 0.82, 1)
        _ColorSombra("Color de la sombra (multiplica)", Color) = (0.5, 0.42, 0.48, 1)
        _Umbral("Umbral luz / sombra", Range(0, 1)) = 0.5
        _Suavidad("Suavidad del corte", Range(0.001, 0.3)) = 0.03
        _Emision("Emision", Color) = (0, 0, 0, 1)

        [Header(Trama de tinta en la sombra)]
        _Trama("Intensidad", Range(0, 1)) = 0
        _TramaEscala("Lineas por metro", Range(2, 60)) = 7
        _TramaGrosor("Grosor de linea", Range(0.02, 0.8)) = 0.28
        _TramaCruzada("Trama cruzada (sombra profunda)", Range(0, 1)) = 0.6
        _UmbralProfundo("Umbral de sombra profunda", Range(0, 1)) = 0.3

        [Header(Borde de luz)]
        _ColorBorde("Color", Color) = (0, 0, 0, 1)
        _AnchoBorde("Ancho", Range(0, 1)) = 0

        [Header(Brillo duro)]
        _ColorBrillo("Color", Color) = (1, 1, 1, 1)
        _TamBrillo("Tamano (0 = sin brillo)", Range(0, 0.3)) = 0

        [Header(Contorno)]
        _ColorContorno("Color (tambien el de la trama)", Color) = (0.02, 0.02, 0.03, 1)
        _GrosorContorno("Grosor en metros (0 = sin contorno)", Range(0, 0.08)) = 0.025
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ColorSombra;
            half _Umbral;
            half _Suavidad;
            half4 _Emision;
            half _Trama;
            float _TramaEscala;
            half _TramaGrosor;
            half _TramaCruzada;
            half _UmbralProfundo;
            half4 _ColorBorde;
            half _AnchoBorde;
            half4 _ColorBrillo;
            half _TamBrillo;
            half4 _ColorContorno;
            float _GrosorContorno;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS   : NORMAL;
            float4 tangentOS  : TANGENT;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        ENDHLSL

        // ─────────────── Color ───────────────
        Pass
        {
            Name "ForwardTinta"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // Luz de personaje que MurtherController actualiza cada cuadro (gira con Murther).
            // Si w = 0 (sin MurtherController en la escena) se usa la luz principal.
            float4 _LuzTintaDir;

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float2 tramaUV    : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                // Coordenadas de la trama en metros, relativas al origen del objeto: las líneas
                // viajan con Murther en vez de quedarse pegadas a la pantalla.
                float3 origen = TransformObjectToWorld(float3(0, 0, 0));
                o.tramaUV = (p.positionWS.xy - origen.xy) * _TramaEscala;
                return o;
            }

            // Línea de tinta antialiasada: 1 sobre la línea, 0 entre líneas.
            half Linea(float u, half grosor)
            {
                float dist = 1.0 - abs(frac(u) - 0.5) * 2.0;  // 0 en el centro de la línea, 1 a media distancia
                float aa = max(fwidth(u) * 2.0, 1e-4);
                return 1.0 - smoothstep(grosor - aa, grosor + aa, dist);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light luzP = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                if (_LuzTintaDir.w > 0.5) luzP.direction = normalize(_LuzTintaDir.xyz);

                // Bandas: medio Lambert con un corte casi duro, como el sombreado posterizado del fondo.
                half medio = dot(n, luzP.direction) * 0.5h + 0.5h;
                half luz = smoothstep(_Umbral - _Suavidad, _Umbral + _Suavidad, medio) * luzP.shadowAttenuation;
                half profunda = 1.0h - smoothstep(_UmbralProfundo - _Suavidad, _UmbralProfundo + _Suavidad, medio);

                half3 col = _BaseColor.rgb * lerp(_ColorSombra.rgb, luzP.color, luz);

                // Trama: diagonal en toda la sombra, cruzada en la sombra profunda. Un temblor leve
                // a lo largo de cada línea la hace parecer trazada a mano en vez de un patrón mecánico.
                float2 t = i.tramaUV;
                half l1 = Linea(t.x + t.y + 0.05 * sin((t.x - t.y) * 1.3), _TramaGrosor);
                half l2 = Linea(t.x - t.y + 0.05 * sin((t.x + t.y) * 1.7 + 1.7), _TramaGrosor);
                half trama = max(l1 * (1.0h - luz), l2 * profunda * _TramaCruzada) * _Trama;
                col = lerp(col, _ColorContorno.rgb, trama);

                // Borde de luz: recorta la silueta contra el fondo (sobre todo el cuerpo negro).
                half fres = 1.0h - saturate(dot(n, v));
                half borde = smoothstep(1.0h - _AnchoBorde, 1.0h - _AnchoBorde + 0.06h, fres) * step(0.001h, _AnchoBorde);
                col += _ColorBorde.rgb * borde * lerp(0.45h, 1.0h, luz);

                // Brillo duro de tinta húmeda.
                float3 h = normalize(luzP.direction + v);
                half nh = saturate(dot(n, h));
                half brillo = smoothstep(1.0h - _TamBrillo - 0.004h, 1.0h - _TamBrillo + 0.004h, nh) * step(0.0001h, _TamBrillo);
                col += _ColorBrillo.rgb * brillo * luzP.color * luzP.shadowAttenuation;

                col += _Emision.rgb;
                return half4(col, 1.0h);
            }
            ENDHLSL
        }

        // ─────────────── Contorno (casco invertido) ───────────────
        Pass
        {
            Name "ContornoTinta"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                if (_GrosorContorno <= 0.0) return float4(0, 0, 0, 0);   // sin contorno: triángulo degenerado
                // NormalesSuavesContorno guarda la normal promediada en tangent.xyz con w = 2.
                // Los tangentes se deforman con los huesos, así el contorno sigue a la animación.
                float3 nOS = input.tangentOS.w > 1.5 ? input.tangentOS.xyz : input.normalOS;
                float3 nWS = normalize(TransformObjectToWorldNormal(nOS));
                float3 pWS = TransformObjectToWorld(input.positionOS.xyz) + nWS * _GrosorContorno;
                return TransformWorldToHClip(pWS);
            }

            half4 Frag() : SV_Target { return half4(_ColorContorno.rgb, 1.0h); }
            ENDHLSL
        }

        // ─────────────── Sombras ───────────────
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 pWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 dirLuz = normalize(_LightPosition - pWS);
            #else
                float3 dirLuz = _LightDirection;
            #endif
                float4 pCS = TransformWorldToHClip(ApplyShadowBias(pWS, nWS, dirLuz));
            #if UNITY_REVERSED_Z
                pCS.z = min(pCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                pCS.z = max(pCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return pCS;
            }

            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        // ─────────────── Profundidad ───────────────
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            float4 Vert(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half Frag(float4 positionCS : SV_POSITION) : SV_Target { return positionCS.z; }
            ENDHLSL
        }

        // ─────────────── Profundidad y normales (SSAO) ───────────────
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = NormalizeNormalPerPixel(i.normalWS);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 oct = PackNormalOctQuadEncode(n);
                return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0.0);
            #else
                return half4(n, 0.0);
            #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
