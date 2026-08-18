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
