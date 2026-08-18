Shader "Poi/VR Water Surface"
{
    Properties
    {
        _Color ("Water Color", Color) = (0.08, 0.55, 0.78, 0.42)
        _WaveStrength ("Wave Strength", Range(0,0.01)) = 0.0015
        _WaveScale ("Wave Scale", Range(0.5,30)) = 9
        _WaveSpeed ("Wave Speed", Range(0,5)) = 0.8
        _Smoothness ("Smoothness", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 150
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert noforwardadd
        #pragma target 3.0
        #include "UnityCG.cginc"

        fixed4 _Color;
        half _WaveStrength, _WaveScale, _WaveSpeed, _Smoothness;

        struct Input { float3 worldPos; };

        void vert(inout appdata_full v)
        {
            float3 world = mul(unity_ObjectToWorld, v.vertex).xyz;
            float t = _Time.y * _WaveSpeed;
            float a = sin((world.x + world.z * 0.63) * _WaveScale + t);
            float b = sin((world.z - world.x * 0.41) * (_WaveScale * 1.37) - t * 0.83);
            v.vertex.y += (a + b) * 0.5 * _WaveStrength;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float t = _Time.y * _WaveSpeed;
            float shimmer = sin((IN.worldPos.x - IN.worldPos.z) * _WaveScale * 1.8 + t * 1.2) * 0.035;
            o.Albedo = saturate(_Color.rgb + shimmer);
            o.Metallic = 0;
            o.Smoothness = _Smoothness;
            o.Alpha = _Color.a;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
