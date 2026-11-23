// Shader de los fondos dibujados (SpriteRenderer): ilumina la ilustración con luces reales de la escena
// usando un mapa de normales generado a partir del propio dibujo (Juego > Iluminación de fondos).
//  - Luz principal (direccional): marca el relieve de piedras, columnas y tinta desde arriba.
//    El área plana conserva el color del dibujo; solo cambian los bordes y volúmenes.
//  - Luces puntuales (faroles, vitral, luz de Murther): iluminan por zonas y rozan el relieve.
//    Usan el ciclo de luces de Forward+ de URP, así que no hay límite práctico de luces por objeto.
//  - Halo: las luces aclaran un poco la tinta negra a su alrededor (sin esto el negro nunca se ilumina).
//  - Brillo: reflejo húmedo solo en las partes del relieve que miran a la luz (el plano no brilla).
//  - Translucidez (para personajes 2D): una luz que queda detrás del sprite también lo ilumina un poco.
// No usa la iluminación ambiental de la escena: el ambiente se ajusta en el material. Con ambiente bajo
// la escena queda en penumbra y son las luces las que dan forma al dibujo.
Shader "Five Star/Fondo Iluminado"
{
    Properties
    {
        [MainTexture] _MainTex ("Ilustración", 2D) = "white" {}
        [NoScaleOffset][Normal] _NormalMap ("Mapa de normales", 2D) = "bump" {}
        _Relieve ("Relieve", Range(0, 2)) = 1
        _Ambiente ("Luz ambiente", Range(0, 1.5)) = 0.5
        _ColorAmbiente ("Color del ambiente", Color) = (0.86, 0.86, 1, 1)
        _LuzPrincipal ("Fuerza de la luz principal", Range(0, 2)) = 0.6
        _LuzPuntual ("Fuerza de las luces puntuales", Range(0, 3)) = 1
        _Halo ("Halo de las luces sobre la tinta", Range(0, 0.5)) = 0.05
        _Brillo ("Brillo húmedo", Range(0, 1)) = 0.25
        _Dureza ("Dureza del brillo", Range(2, 64)) = 18
        _Lejania ("Tinte de lejanía", Color) = (1, 1, 1, 1)
        _Translucidez ("Luz que atraviesa desde atrás", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Pass
        {
            Name "FondoIluminado"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);  SAMPLER(sampler_NormalMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Relieve;
                half _Ambiente;
                half4 _ColorAmbiente;
                half _LuzPrincipal;
                half _LuzPuntual;
                half _Halo;
                half _Brillo;
                half _Dureza;
                half4 _Lejania;
                half _Translucidez;
            CBUFFER_END

            struct Atributos
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Interpolados
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            Interpolados Vert(Atributos v)
            {
                Interpolados o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            // Reflejo que solo aparece donde el relieve mira más a la luz que el plano del dibujo.
            half BrilloRelieve(float3 n, float3 plano, half3 dirLuz, half3 vista)
            {
                half3 h = normalize(dirLuz + vista);
                return max(0.0h, pow(saturate(dot(n, h)), _Dureza) - pow(saturate(dot(plano, h)), _Dureza));
            }

            half4 Frag(Interpolados i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                half3 albedo = tex.rgb;
                half lum = dot(albedo, half3(0.299h, 0.587h, 0.114h));

                // El sprite mira hacia -Z en su espacio local (hacia la cámara).
                half3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, i.uv), _Relieve);
                float3 n = normalize(TransformObjectToWorldNormal(float3(nTS.x, nTS.y, -nTS.z)));
                float3 plano = normalize(TransformObjectToWorldNormal(float3(0, 0, -1)));
                half3 vista = GetWorldSpaceNormalizeViewDir(i.positionWS);

                // Ambiente: un poco más claro en lo que mira hacia arriba.
                half3 luz = _Ambiente * _ColorAmbiente.rgb * (1.0h + 0.2h * (half)n.y);
                half3 brillo = 0;

                Light principal = GetMainLight();
                luz += _LuzPrincipal * principal.color * saturate(dot(n, principal.direction));
                brillo += principal.color * BrilloRelieve(n, plano, principal.direction, vista);

                half3 halo = 0;
            #if defined(_ADDITIONAL_LIGHTS)
                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                uint cantidad = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(cantidad)
                    Light l = GetAdditionalLight(lightIndex, i.positionWS);
                    half3 c = l.color * l.distanceAttenuation;
                    // Solo ilumina si la luz está delante del dibujo (una luz detrás de esta capa ilumina la de atrás),
                    // salvo la parte que deja pasar la translucidez.
                    half delante = step(0.0h, dot(plano, l.direction));
                    half difusa = saturate(dot(n, l.direction) * 0.9h + 0.1h);
                    half trasera = saturate(-dot(n, l.direction)) * _Translucidez;
                    luz += _LuzPuntual * c * (difusa * delante + trasera * (1.0h - delante));
                    brillo += c * BrilloRelieve(n, plano, l.direction, vista) * delante;
                    halo += c * delante;
                LIGHT_LOOP_END
            #endif

                half3 color = albedo * luz * _Lejania.rgb
                            + brillo * _Brillo * (0.25h + lum) * _Lejania.rgb
                            + halo * _Halo * _Lejania.rgb;
                return half4(color, tex.a);
            }
            ENDHLSL
        }
    }
    Fallback "Sprites/Default"
}
