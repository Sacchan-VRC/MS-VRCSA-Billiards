// converted from old surf shader by AI (Opus 5.5)
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
      _ClothHue ("Cloth Hue", Range(0, 1)) = 0
      _ClothSaturation ("Cloth Saturation", Range(0, 3)) = 1
      _DetailClothBrightness ("Detail Brightness", Range(0, 2)) = 1
      _DetailClothMask ("Cloth Detail Mask", 2D) = "white" {}
      _MaskStrengthCloth ("Mask Strength Cloth", Range(0, 1)) = 1
      [Toggle(DETAIL_OTHER)]_UseDetailOther ("Use Non-Cloth Detail Texture", Range(0,1)) = 0
      _DetailOther ("Other Detail", 2D) = "white" {}
      _DetailOtherBrightness ("Other Detail Brightness", Range(0, 2)) = 1
      _MaskStrengthOther ("Mask Strength Other", Range(0, 1)) = 1

      _TimerPct ("Timer Percentage", Range(0, 1)) = 1

      [Header(VRC Light Volumes)]
      [Toggle(INTEGRATE_VRCLV)]_IntegrateVRCLV ("Integrate VRC Light Volumes", Int) = 1
   }
   SubShader
   {
      Tags { "RenderType"="Opaque" }
      LOD 200

      CGINCLUDE
      #include "UnityCG.cginc"

      sampler2D _MainTex;
      float4 _MainTex_ST;
      sampler2D _EmissionMap;
      sampler2D _Metalic;
      sampler2D _DetailCloth;
      sampler2D _DetailOther;
      sampler2D _DetailClothMask;
      float4 _DetailCloth_ST;
      float4 _DetailOther_ST;

      static const float M_PI = 3.14159265358979323846264338327950288;

      UNITY_INSTANCING_BUFFER_START( Props )
          UNITY_DEFINE_INSTANCED_PROP( half4, _EmissionColor)
          UNITY_DEFINE_INSTANCED_PROP( half4, _Color)
          UNITY_DEFINE_INSTANCED_PROP( float, _ClothHue)
          UNITY_DEFINE_INSTANCED_PROP( float, _ClothSaturation)
          UNITY_DEFINE_INSTANCED_PROP( float, _MaskStrengthCloth)
          UNITY_DEFINE_INSTANCED_PROP( float, _MaskStrengthOther)
          UNITY_DEFINE_INSTANCED_PROP( float, _DetailClothBrightness)
          UNITY_DEFINE_INSTANCED_PROP( float, _DetailOtherBrightness)
          UNITY_DEFINE_INSTANCED_PROP( float, _TimerPct)
      UNITY_INSTANCING_BUFFER_END( Props )

      float3 linear_srgb_to_oklab(float3 c)
      {
         float l = 0.4122214708 * c.x + 0.5363325363 * c.y + 0.0514459929 * c.z;
         float m = 0.2119034982 * c.x + 0.6806995451 * c.y + 0.1073969566 * c.z;
         float s = 0.0883024619 * c.x + 0.2817188376 * c.y + 0.6299787005 * c.z;

         float l_ = pow(l, 1.0 / 3.0);
         float m_ = pow(m, 1.0 / 3.0);
         float s_ = pow(s, 1.0 / 3.0);

         return float3(
            0.2104542553 * l_ + 0.7936177850 * m_ - 0.0040720468 * s_,
            1.9779984951 * l_ - 2.4285922050 * m_ + 0.4505937099 * s_,
            0.0259040371 * l_ + 0.7827717662 * m_ - 0.8086757660 * s_
         );
      }

      float3 oklab_to_linear_srgb(float3 c)
      {
         float l_ = c.x + 0.3963377774 * c.y + 0.2158037573 * c.z;
         float m_ = c.x - 0.1055613458 * c.y - 0.0638541728 * c.z;
         float s_ = c.x - 0.0894841775 * c.y - 1.2914855480 * c.z;

         float l = l_ * l_ * l_;
         float m = m_ * m_ * m_;
         float s = s_ * s_ * s_;

         return float3(
            +4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s,
            -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s,
            -0.0041960863 * l - 0.7034186147 * m + 1.7076147010 * s
         );
      }

      float3 hueShift(float3 color, float shift)
      {
         float3 oklab = linear_srgb_to_oklab(max(color, 0.0000000001));
         float hue = atan2(oklab.z, oklab.y);
         hue += shift * M_PI * 2;

         float chroma = length(oklab.yz);
         oklab.y = cos(hue) * chroma;
         oklab.z = sin(hue) * chroma;

         return oklab_to_linear_srgb(oklab);
      }

      float3 Unity_Saturation_float(float3 In, float Saturation)
      {
         float luma = dot(In, float3(0.2126729, 0.7151522, 0.0721750));
         float3 Out = luma.xxx + Saturation.xxx * (In - luma.xxx);
         return Out;
      }

      struct TableSurface
      {
         float3 albedo;
         float  metallic;
         float  smoothness;
         float3 emission;
      };

      TableSurface SampleTableSurface(float2 uv, float3 modelPos)
      {
         TableSurface o;

         half4 sample_diffuse  = tex2D(_MainTex, uv);
         half4 sample_emission = tex2D(_EmissionMap, uv);
         half4 sample_metalic  = tex2D(_Metalic, uv);

         float4 _Color_var = UNITY_ACCESS_INSTANCED_PROP( Props, _Color );
         float3 final = lerp(sample_diffuse.rgb, _Color_var.rgb * sample_diffuse.rgb * 2.0, pow(sample_diffuse.a, 0.1));

      #if defined(DETAIL_CLOTH)
         float4 _DetailClothBrightness_var = UNITY_ACCESS_INSTANCED_PROP( Props, _DetailClothBrightness );
         float3 sample_detail = tex2D(_DetailCloth, uv * _DetailCloth_ST.xy + _DetailCloth_ST.wz).rgb * unity_ColorSpaceDouble.rgb * _DetailClothBrightness_var;
         float sample_detailclothmask = tex2D(_DetailClothMask, uv).r;
         float4 _MaskStrengthCloth_var = UNITY_ACCESS_INSTANCED_PROP( Props, _MaskStrengthCloth );
         final = lerp(final, final * sample_detail, sample_detailclothmask * _MaskStrengthCloth_var);
         float3 cloth = final * sample_detailclothmask;
         float3 other = final * (1 - sample_detailclothmask);
         float4 _ClothHue_var = UNITY_ACCESS_INSTANCED_PROP( Props, _ClothHue );
         cloth = hueShift(cloth, _ClothHue_var);
         float4 _ClothSaturation_var = UNITY_ACCESS_INSTANCED_PROP( Props, _ClothSaturation );
         cloth = Unity_Saturation_float(cloth, _ClothSaturation_var);
         final = cloth + other;
         #if defined(DETAIL_OTHER)
            float4 _DetailOtherBrightness_var = UNITY_ACCESS_INSTANCED_PROP( Props, _DetailOtherBrightness );
            float3 sample_detailother = tex2D(_DetailOther, uv * _DetailOther_ST.xy + _DetailOther_ST.wz).rgb * unity_ColorSpaceDouble.rgb * _DetailOtherBrightness_var;
            float4 _MaskStrengthOther_var = UNITY_ACCESS_INSTANCED_PROP( Props, _MaskStrengthOther );
            final = lerp(final, final * sample_detailother, (1 - sample_detailclothmask) * _MaskStrengthOther_var);
         #endif
      #endif

         o.albedo     = final;
         o.metallic   = sample_metalic.r;
         o.smoothness = sample_metalic.a;

         float4 _TimerPct_var = UNITY_ACCESS_INSTANCED_PROP( Props, _TimerPct );
         float timer_pct = clamp(_TimerPct_var, 0, 1);
         float surf_angle_pct = (M_PI + atan2(modelPos.x, modelPos.z)) / (2 * M_PI) / 1.04 + (1 - 1 / 1.04);
         float angle_cl = clamp((surf_angle_pct - timer_pct) * 40.0, 0, 1.5);
         float4 _EmissionColor_var = UNITY_ACCESS_INSTANCED_PROP( Props, _EmissionColor );
         o.emission = sample_emission.rgb * _EmissionColor_var.rgb * angle_cl;

         return o;
      }
      ENDCG

      Pass
      {
         Name "FORWARD"
         Tags { "LightMode"="ForwardBase" }

         CGPROGRAM
         #pragma vertex vertBase
         #pragma fragment fragBase
         #pragma target 4.5
         #pragma multi_compile_fwdbase
         #pragma multi_compile_fog
         #pragma shader_feature_local DETAIL_CLOTH
         #pragma shader_feature_local DETAIL_OTHER
         #pragma shader_feature_local INTEGRATE_VRCLV
         #pragma multi_compile_instancing

         #define UNITY_PASS_FORWARDBASE
         #include "UnityPBSLighting.cginc"
         #include "AutoLight.cginc"

         #if defined(INTEGRATE_VRCLV)
            #include "Packages/red.sim.lightvolumes/Shaders/LightVolumes.cginc"
         #endif

         struct appdata_custom
         {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
            float2 texcoord2 : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
         };

         struct v2f_base
         {
            float4 pos         : SV_POSITION;
            float2 uv          : TEXCOORD0;
            float3 modelPos    : TEXCOORD1;
            float3 worldPos    : TEXCOORD2;
            float3 worldNormal : TEXCOORD3;

            float4 ambientOrLightmapUV : TEXCOORD4;
            UNITY_LIGHTING_COORDS(5, 6)
            UNITY_FOG_COORDS(7)
            UNITY_VERTEX_OUTPUT_STEREO
            UNITY_VERTEX_INPUT_INSTANCE_ID
         };

         v2f_base vertBase(appdata_custom v)
         {
            v2f_base o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f_base, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            UNITY_TRANSFER_INSTANCE_ID( v, o );

            o.pos         = UnityObjectToClipPos(v.vertex);
            o.uv          = TRANSFORM_TEX(v.texcoord, _MainTex);
            o.modelPos    = v.vertex.xyz;
            o.worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);

            #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
               o.ambientOrLightmapUV = 0;
               #ifdef LIGHTMAP_ON
                  o.ambientOrLightmapUV.xy = v.texcoord1.xy * unity_LightmapST.xy + unity_LightmapST.zw;
               #endif
               #ifdef DYNAMICLIGHTMAP_ON
                  o.ambientOrLightmapUV.zw = v.texcoord2.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
               #endif
            #else
               half3 ambient = 0;
               #ifdef VERTEXLIGHT_ON
                  ambient += Shade4PointLights(
                     unity_4LightPosX0, unity_4LightPosY0, unity_4LightPosZ0,
                     unity_LightColor[0].rgb, unity_LightColor[1].rgb, unity_LightColor[2].rgb, unity_LightColor[3].rgb,
                     unity_4LightAtten0, o.worldPos, o.worldNormal);
               #endif
               #if !defined(INTEGRATE_VRCLV) && UNITY_SHOULD_SAMPLE_SH
                  ambient = ShadeSHPerVertex(o.worldNormal, ambient);
               #endif
               o.ambientOrLightmapUV = half4(ambient, 0);
            #endif

            UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
            UNITY_TRANSFER_FOG(o, o.pos);
            return o;
         }

         half4 fragBase(v2f_base i) : SV_Target
         {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            UNITY_SETUP_INSTANCE_ID( i );

            TableSurface s = SampleTableSurface(i.uv, i.modelPos);

            float3 worldPos = i.worldPos;
            float3 normalWS = normalize(i.worldNormal);
            float3 viewDir  = normalize(UnityWorldSpaceViewDir(worldPos));

            #ifndef USING_DIRECTIONAL_LIGHT
               float3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
            #else
               float3 lightDir = _WorldSpaceLightPos0.xyz;
            #endif

            UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

            half3 specColor;
            half  oneMinusReflectivity;
            half3 diffColor = DiffuseAndSpecularFromMetallic(s.albedo, s.metallic, specColor, oneMinusReflectivity);

            UnityGIInput giInput;
            UNITY_INITIALIZE_OUTPUT(UnityGIInput, giInput);
            giInput.light.color  = _LightColor0.rgb;
            giInput.light.dir    = lightDir;
            giInput.worldPos     = worldPos;
            giInput.worldViewDir = viewDir;
            giInput.atten        = atten;
            #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
               giInput.lightmapUV = i.ambientOrLightmapUV;
               giInput.ambient    = 0;
            #else
               giInput.lightmapUV = 0;
               giInput.ambient    = i.ambientOrLightmapUV.rgb;
            #endif
            giInput.probeHDR[0] = unity_SpecCube0_HDR;
            giInput.probeHDR[1] = unity_SpecCube1_HDR;
            #if defined(UNITY_SPECCUBE_BLENDING) || defined(UNITY_SPECCUBE_BOX_PROJECTION)
               giInput.boxMin[0] = unity_SpecCube0_BoxMin; // .w holds lerp value for blending
            #endif
            #ifdef UNITY_SPECCUBE_BOX_PROJECTION
               giInput.boxMax[0]        = unity_SpecCube0_BoxMax;
               giInput.probePosition[0] = unity_SpecCube0_ProbePosition;
               giInput.boxMax[1]        = unity_SpecCube1_BoxMax;
               giInput.boxMin[1]        = unity_SpecCube1_BoxMin;
               giInput.probePosition[1] = unity_SpecCube1_ProbePosition;
            #endif

            Unity_GlossyEnvironmentData glossIn = UnityGlossyEnvironmentSetup(s.smoothness, viewDir, normalWS, specColor);
            UnityGI gi = UnityGlobalIllumination(giInput, 1.0, normalWS, glossIn);


            float3 lvSpecular = 0;
            #if defined(INTEGRATE_VRCLV)
               float3 worldPosOffset    = 0;
               float  pointLightShading = 3;
               float3 L0, L1r, L1g, L1b;

               #if defined(LIGHTMAP_ON) || defined(DYNAMICLIGHTMAP_ON)
                  LightVolumeAdditiveSHSpecular(worldPos, L0, L1r, L1g, L1b, lvSpecular,
                     s.albedo, s.smoothness, s.metallic, normalWS, viewDir, worldPosOffset, pointLightShading);
                  gi.indirect.diffuse = max(gi.indirect.diffuse + LightVolumeEvaluate(normalWS, L0, L1r, L1g, L1b), 0);
               #else
                  LightVolumeSHSpecular(worldPos, L0, L1r, L1g, L1b, lvSpecular,
                     s.albedo, s.smoothness, s.metallic, normalWS, viewDir, worldPosOffset, pointLightShading);
                  gi.indirect.diffuse = max(LightVolumeEvaluate(normalWS, L0, L1r, L1g, L1b), 0)
                                      + i.ambientOrLightmapUV.rgb;
               #endif
            #endif

            half4 c = UNITY_BRDF_PBS(diffColor, specColor, oneMinusReflectivity, s.smoothness,
                                     normalWS, viewDir, gi.light, gi.indirect);

            c.rgb += lvSpecular;
            c.rgb += s.emission;

            UNITY_APPLY_FOG(i.fogCoord, c);
            UNITY_OPAQUE_ALPHA(c.a);
            return c;
         }
         ENDCG
      }
      Pass
      {
         Name "FORWARD_DELTA"
         Tags { "LightMode"="ForwardAdd" }
         Blend One One
         ZWrite Off

         CGPROGRAM
         #pragma vertex vertAdd
         #pragma fragment fragAdd
         #pragma target 4.5
         #pragma multi_compile_fwdadd_fullshadows
         #pragma multi_compile_fog
         #pragma shader_feature_local DETAIL_CLOTH
         #pragma shader_feature_local DETAIL_OTHER
         #pragma multi_compile_instancing

         #define UNITY_PASS_FORWARDADD
         #include "UnityPBSLighting.cginc"
         #include "AutoLight.cginc"

         struct appdata_custom
         {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
            float2 texcoord2 : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
         };

         struct v2f_add
         {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            float4 pos         : SV_POSITION;
            float2 uv          : TEXCOORD0;
            float3 modelPos    : TEXCOORD1;
            float3 worldPos    : TEXCOORD2;
            float3 worldNormal : TEXCOORD3;
            UNITY_LIGHTING_COORDS(4, 5)
            UNITY_FOG_COORDS(6)
            UNITY_VERTEX_OUTPUT_STEREO
         };

         v2f_add vertAdd(appdata_custom v)
         {
            v2f_add o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f_add, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            UNITY_TRANSFER_INSTANCE_ID( v, o );

            o.pos         = UnityObjectToClipPos(v.vertex);
            o.uv          = TRANSFORM_TEX(v.texcoord, _MainTex);
            o.modelPos    = v.vertex.xyz;
            o.worldPos    = mul(unity_ObjectToWorld, v.vertex).xyz;
            o.worldNormal = UnityObjectToWorldNormal(v.normal);

            UNITY_TRANSFER_LIGHTING(o, v.texcoord1.xy);
            UNITY_TRANSFER_FOG(o, o.pos);
            return o;
         }

         half4 fragAdd(v2f_add i) : SV_Target
         {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
            UNITY_SETUP_INSTANCE_ID( i );

            TableSurface s = SampleTableSurface(i.uv, i.modelPos);

            float3 worldPos = i.worldPos;
            float3 normalWS = normalize(i.worldNormal);
            float3 viewDir  = normalize(UnityWorldSpaceViewDir(worldPos));

            #ifndef USING_DIRECTIONAL_LIGHT
               float3 lightDir = normalize(UnityWorldSpaceLightDir(worldPos));
            #else
               float3 lightDir = _WorldSpaceLightPos0.xyz;
            #endif

            UNITY_LIGHT_ATTENUATION(atten, i, worldPos);

            half3 specColor;
            half  oneMinusReflectivity;
            half3 diffColor = DiffuseAndSpecularFromMetallic(s.albedo, s.metallic, specColor, oneMinusReflectivity);

            UnityLight light;
            UNITY_INITIALIZE_OUTPUT(UnityLight, light);
            light.color = _LightColor0.rgb * atten;
            light.dir   = lightDir;

            UnityIndirect noIndirect;
            noIndirect.diffuse  = 0;
            noIndirect.specular = 0;

            half4 c = UNITY_BRDF_PBS(diffColor, specColor, oneMinusReflectivity, s.smoothness,
                                     normalWS, viewDir, light, noIndirect);

            UNITY_APPLY_FOG_COLOR(i.fogCoord, c, half4(0, 0, 0, 0));
            return half4(c.rgb, 0);
         }
         ENDCG
      }
      Pass
      {
         Name "ShadowCaster"
         Tags { "LightMode"="ShadowCaster" }
         ZWrite On
         ZTest LEqual

         CGPROGRAM
         #pragma vertex vertShadow
         #pragma fragment fragShadow
         #pragma target 4.5
         #pragma multi_compile_shadowcaster
         #pragma multi_compile_instancing

         #define UNITY_PASS_SHADOWCASTER

         struct appdata_custom
         {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
         };

         struct v2f_shadow
         {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            V2F_SHADOW_CASTER;
            UNITY_VERTEX_OUTPUT_STEREO
         };

         v2f_shadow vertShadow(appdata_custom v)
         {
            v2f_shadow o;
            UNITY_SETUP_INSTANCE_ID(v);
            UNITY_INITIALIZE_OUTPUT(v2f_shadow, o);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
            return o;
         }

         float4 fragShadow(v2f_shadow i) : SV_Target
         {
            SHADOW_CASTER_FRAGMENT(i)
         }
         ENDCG
      }
      Pass
      {
         Name "META"
         Tags { "LightMode"="Meta" }
         Cull Off

         CGPROGRAM
         #pragma vertex vertMeta
         #pragma fragment fragMeta
         #pragma target 4.5
         #pragma shader_feature_local DETAIL_CLOTH
         #pragma shader_feature_local DETAIL_OTHER
         #pragma multi_compile_instancing

         #define UNITY_PASS_META
         #include "UnityMetaPass.cginc"

         struct appdata_custom
         {
            float4 vertex : POSITION;
            float3 normal : NORMAL;
            float2 texcoord : TEXCOORD0;
            float2 texcoord1 : TEXCOORD1;
            float2 texcoord2 : TEXCOORD2;
            UNITY_VERTEX_INPUT_INSTANCE_ID
         };

         struct v2f_meta
         {
            UNITY_VERTEX_INPUT_INSTANCE_ID
            float4 pos      : SV_POSITION;
            float2 uv       : TEXCOORD0;
            float3 modelPos : TEXCOORD1;
         };

         v2f_meta vertMeta(appdata_custom v)
         {
            v2f_meta o;
            UNITY_INITIALIZE_OUTPUT(v2f_meta, o);
            o.pos      = UnityMetaVertexPosition(v.vertex, v.texcoord1.xy, v.texcoord2.xy, unity_LightmapST, unity_DynamicLightmapST);
            o.uv       = TRANSFORM_TEX(v.texcoord, _MainTex);
            o.modelPos = v.vertex.xyz;
            return o;
         }

         half4 fragMeta(v2f_meta i) : SV_Target
         {
            TableSurface s = SampleTableSurface(i.uv, i.modelPos);

            UnityMetaInput metaIN;
            UNITY_INITIALIZE_OUTPUT(UnityMetaInput, metaIN);
            metaIN.Albedo   = s.albedo;
            metaIN.Emission = s.emission;
            return UnityMetaFragment(metaIN);
         }
         ENDCG
      }
   }
   FallBack "Diffuse"
}
