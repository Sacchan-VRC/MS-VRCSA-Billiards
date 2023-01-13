Shader "metaphira/Timer"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _TimeFrac( "Time", Range(0, 1) ) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
        };

        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        float _TimeFrac;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex);

            float4 colourterm = clamp( (c.r - _TimeFrac) * 12.0, 0.0, 1.0 );

            o.Albedo = lerp( _Color * colourterm, fixed4(1.0,1.0,1.0,1.0), c.g );
            
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = c.a;
            o.Emission = colourterm * (1.0-c.g) * _Color;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
