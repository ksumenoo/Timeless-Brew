// Контур-подсветка обучения: «вывернутый» силуэт. Вершины раздуваются вдоль нормалей,
// отсекаются передние грани (Cull Front) — наружу торчит только белая «изнанка» = ровный контур.
// Рисуется ПОСЛЕ непрозрачной геометрии: сам предмет уже записал глубину и закрывает всё, кроме каёмки.
Shader "TimelessBrew/TutorialOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlineWidth ("Outline Width", Float) = 0.03
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+20" "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            Name "Outline"
            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings  { float4 positionCS : SV_POSITION; };

            float  _OutlineWidth;
            float4 _OutlineColor;

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                // Раздуваем вдоль нормали в МИРОВЫХ единицах — толщина контура одинакова на любом масштабе меша.
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 nWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                posWS += nWS * _OutlineWidth;
                OUT.positionCS = TransformWorldToHClip(posWS);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target { return _OutlineColor; }
            ENDHLSL
        }
    }
    Fallback Off
}
