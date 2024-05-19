Shader "metaphira/TableSurface"
{
   Properties
   {
      _EmissionColor ("Emission Color", Color) = (1,1,1,1)
      _Color ("Tint Color", Color) = (1,1,1,1)

      _MainTex ("Albedo (RGB), TintMap(A)", 2D) = "white" {}
      _EmissionMap ("Emission Mask", 2D) = "black" {}
      _Metalic ("Metallic(R)/Smoothness(A)", 2D) = "white" {}
		[Toggle(DETAIL_CLOTH)]_UseDetailCloth ("Use Cloth Detail Texture", Range(0,1)) = 0
      _DetailCloth ("Cloth Detail", 2D) = "white" {}
      _DetailClothBrightness("Detail Brightness", Range(0, 1)) = 1
      _DetailClothMask ("Cloth Detail Mask", 2D) = "white" {}
      _MaskStrengthCloth("Mask Strength Cloth", Range(0, 1)) = 1
		[Toggle(DETAIL_OTHER)]_UseDetailOther ("Use Non-Cloth Detail Texture", Range(0,1)) = 0
      _DetailOther ("Other Detail", 2D) = "white" {}
      _DetailOtherBrightness("Other Detail Brightness", Range(0, 1)) = 1
      _MaskStrengthOther("Mask Strength Other", Range(0, 1)) = 1

      _TimerPct("Timer Percentage", Range(0, 1)) = 1
   }
   SubShader
   {
      Tags { "RenderType"="Opaque" }
      LOD 200

      CGPROGRAM

      #pragma surface surf Standard fullforwardshadows vertex:vert
      #pragma target 3.0
      #pragma shader_feature DETAIL_CLOTH
      #pragma shader_feature DETAIL_OTHER

      sampler2D _MainTex;
      sampler2D _EmissionMap;
      sampler2D _Metalic;
      sampler2D _DetailCloth;
      sampler2D _DetailOther;
      sampler2D _DetailClothMask;
      sampler2D _TimerMap;
	   fixed4 _DetailCloth_ST;
	   fixed4 _DetailOther_ST;

      fixed4 _EmissionColor;
      fixed3 _Color;

      float _MaskStrengthCloth;
      float _MaskStrengthOther;
      float _DetailClothBrightness;
      float _DetailOtherBrightness;
      float _TimerPct;

      struct Input
      {
         float2 uv_MainTex;
         float3 modelPos;
      };

      void vert ( inout appdata_full v, out Input o ) 
      {
         UNITY_INITIALIZE_OUTPUT(Input,o);
         o.modelPos = v.vertex.xyz;
      }

      static const float M_PI = 3.14159265358979323846264338327950288;

      void surf (Input IN, inout SurfaceOutputStandard o)
      {
         fixed4 sample_diffuse = tex2D (_MainTex, IN.uv_MainTex);
         fixed4 sample_emission = tex2D( _EmissionMap, IN.uv_MainTex );
         fixed4 sample_metalic = tex2D( _Metalic, IN.uv_MainTex );
#if defined(DETAIL_CLOTH)
         fixed4 sample_detail = tex2D (_DetailCloth, IN.uv_MainTex * _DetailCloth_ST.xy + _DetailCloth_ST.wz) * unity_ColorSpaceDouble * _DetailClothBrightness;
         float sample_detailclothmask = tex2D(_DetailClothMask, IN.uv_MainTex ).r;
         fixed3 final = lerp( sample_diffuse.rgb, _Color * sample_diffuse.rgb * 2.0, pow(sample_diffuse.a,0.1) );
         final = lerp(final, final * sample_detail, sample_detailclothmask * _MaskStrengthCloth);
#if defined(DETAIL_OTHER)
         fixed4 sample_detailother = tex2D (_DetailOther, IN.uv_MainTex * _DetailOther_ST.xy + _DetailOther_ST.wz) * unity_ColorSpaceDouble * _DetailOtherBrightness;
         final = lerp(final, final * sample_detailother, (1-sample_detailclothmask) * _MaskStrengthOther);
#endif
#else
         fixed3 final = lerp( sample_diffuse.rgb, _Color * sample_diffuse.rgb * 2.0, pow(sample_diffuse.a,0.1) );
#endif
         o.Albedo = final;
         o.Metallic = sample_metalic.r;
         o.Smoothness = sample_metalic.a;
         o.Alpha = 1.0;

         float timer_pct = clamp(_TimerPct, 0, 1);
         // add a small fudge factor so that the light connects
         float surf_angle_pct = (M_PI + atan2(IN.modelPos.x, IN.modelPos.z)) / (2*M_PI) / 1.04 + (1 - 1 / 1.04);
         float angle_cl = clamp((surf_angle_pct - timer_pct) * 40.0, 0, 1.5);
         o.Emission = sample_emission.r * _EmissionColor * angle_cl;
      }

      ENDCG
   }
   FallBack "Diffuse"
}
