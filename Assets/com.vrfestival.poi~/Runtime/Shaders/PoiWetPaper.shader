Shader "Poi/Wet Paper"
{
    Properties
    {
        _Color ("Dry Paper Color", Color) = (1,0.96,0.82,1)
        _WetnessMap ("Wetness Map", 2D) = "black" {}
        _WetDarkness ("Wet Darkness", Range(0.1,1)) = 0.58
        _WetAlpha ("Wet Alpha", Range(0.2,1)) = 0.68
        _Smoothness ("Smoothness", Range(0,1)) = 0.12
        [HideInInspector] _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        [HideInInspector] _DissolveSeed ("Dissolve Seed", Float) = 0
    }
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal" }
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_WetnessMap);
            SAMPLER(sampler_WetnessMap);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _WetDarkness;
                half _WetAlpha;
                half _Smoothness;
                half _DissolveAmount;
                float _DissolveSeed;
            CBUFFER_END

            half Hash21Urp(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            half PaperNoiseUrp(float2 p)
            {
                float2 cell = floor(p);
                float2 local = frac(p);
                local = local * local * (3.0 - 2.0 * local);
                half a = Hash21Urp(cell);
                half b = Hash21Urp(cell + float2(1, 0));
                half c = Hash21Urp(cell + float2(0, 1));
                half d = Hash21Urp(cell + float2(1, 1));
                return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 dissolveUv = input.uv * 22.0 + _DissolveSeed * float2(1.73, 2.91);
                half dissolveNoise = PaperNoiseUrp(dissolveUv) * 0.68h + PaperNoiseUrp(dissolveUv * 2.13 + 7.4) * 0.32h;
                clip(dissolveNoise - _DissolveAmount);
                half wet = saturate(SAMPLE_TEXTURE2D(_WetnessMap, sampler_WetnessMap, input.uv).r);
                wet = wet * wet * (3.0h - 2.0h * wet);
                half3 color = _Color.rgb * lerp(1.0h, _WetDarkness, wet);
                return half4(color, lerp(_Color.a, _WetAlpha, wet));
            }
            ENDHLSL
        }
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 150
        Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade noforwardadd
        #pragma target 3.0
        sampler2D _WetnessMap;
        fixed4 _Color;
        half _WetDarkness, _WetAlpha, _Smoothness, _DissolveAmount;
        float _DissolveSeed;
        struct Input { float2 uv_WetnessMap; };

        half Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 345.45));
            p += dot(p, p + 34.345);
            return frac(p.x * p.y);
        }

        half PaperNoise(float2 p)
        {
            float2 cell = floor(p);
            float2 local = frac(p);
            local = local * local * (3.0 - 2.0 * local);
            half a = Hash21(cell);
            half b = Hash21(cell + float2(1, 0));
            half c = Hash21(cell + float2(0, 1));
            half d = Hash21(cell + float2(1, 1));
            return lerp(lerp(a, b, local.x), lerp(c, d, local.x), local.y);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 dissolveUv = IN.uv_WetnessMap * 22.0 + _DissolveSeed * float2(1.73, 2.91);
            half softNoise = PaperNoise(dissolveUv) * 0.68h + PaperNoise(dissolveUv * 2.13 + 7.4) * 0.32h;
            clip(softNoise - _DissolveAmount);
            half wet = saturate(tex2D(_WetnessMap, IN.uv_WetnessMap).r);
            // Smooth interpolation and bilinear texture filtering keep the wet edge soft.
            wet = wet * wet * (3.0h - 2.0h * wet);
            o.Albedo = _Color.rgb * lerp(1.0h, _WetDarkness, wet);
            o.Metallic = 0;
            o.Smoothness = lerp(_Smoothness, 0.5h, wet);
            o.Alpha = lerp(_Color.a, _WetAlpha, wet);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
