Shader "Poi/Wet Paper"
{
    Properties
    {
        _Color ("Dry Paper Color", Color) = (1,0.96,0.82,1)
        _WetnessMap ("Wetness Map", 2D) = "black" {}
        _WetDarkness ("Wet Darkness", Range(0.1,1)) = 0.58
        _WetAlpha ("Wet Alpha", Range(0.2,1)) = 0.68
        _Smoothness ("Smoothness", Range(0,1)) = 0.12
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
        half _WetDarkness, _WetAlpha, _Smoothness;
        struct Input { float2 uv_WetnessMap; };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
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
