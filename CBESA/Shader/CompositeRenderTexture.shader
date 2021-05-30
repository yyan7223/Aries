// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/CompositeRenderTexture"
{
    Properties {
       _MainTex ("Out part Texture", 2D) = "white" {} // Out part texture
       _FoveaTex ("fovea part Texture", 2D) = "white" {} // Fovea part texture
       _FoveaCoordinateX ("Normalized fovea coordinate X", range(0,1)) = 0.5 // Normalized fovea coordinate X
       _FoveaCoordinateY ("Normalized fovea coordinate Y", range(0,1)) = 0.5 // Normalized fovea coordinate X
       _E1("Eccentricity 1", range(0,1)) = 0.2 // Eccentricity 1  
       _E2("Eccentricity 2", range(0,1)) = 0.3 // Eccentricity 2     
       _BlurSize("Blur Size", range(0.0001,0.1)) = 0.0001 // 值越大，out部分越模糊
       _StandardDeviation("Standard Deviation", range(0.0001, 0.3)) = 0.0001 // 值越大，像素上下交错越明显
     }

    SubShader {

      //Vertical Blur
      pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "unitycg.cginc"

            #define PI 3.14159265359
			      #define E 2.71828182846
            #define SAMPLES 100 //值越大，blur效果越柔和
            #define blurThickness 0.005 

            sampler2D _MainTex;
            sampler2D _FoveaTex;

            float4 _MainTex_ST;
            float4 _FoveaTex_ST;

            float _E1;
            float _E2;

            float _FoveaCoordinateX;
            float _FoveaCoordinateY;

            float _BlurSize;
			      float _StandardDeviation;

            struct v2f {
                float4 pos:SV_POSITION;
                //float3 normal:NORMAL;
                float2 uv:TEXCOORD0;
            };
            
            fixed4 resampleTexture(sampler2D tex, float2 coord, float4 rect){
                float2 offset = float2(coord.x - rect.x, coord.y - rect.z);
                float2 scale = 1 / float2(abs(rect.x - rect.y), abs(rect.z - rect.w));
                return tex2D(tex, offset * scale);
            }

            // x:left y:right z:bottom w:top
            float4 computeRect(float _FoveaCoordinateX, float _FoveaCoordinateY, float Eccentricity){
              return clamp(float4(_FoveaCoordinateX - Eccentricity, _FoveaCoordinateX + Eccentricity, 
                                  _FoveaCoordinateY - Eccentricity, _FoveaCoordinateY + Eccentricity), 
                                  0, 1);
            }

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            // fixed4 frag(v2f IN):SV_Target
            // {
            //   if(_StandardDeviation == 0) return tex2D(_MainTex, IN.uv);

            //   float dist = distance(IN.uv, float2(_FoveaCoordinateX, _FoveaCoordinateY));

            //   float4 rect = computeRect(_FoveaCoordinateX, _FoveaCoordinateY, _E1);
              
            //   if(dist < _E1 - blurThickness) // 1e-3 represent the thickness of the red circle
            //   {
            //     return resampleTexture(_FoveaTex, IN.uv, rect);
            //   }
            //   else if(dist >= _E1 - blurThickness && dist <= _E1) // red circle
            //   {
            //     float4 sum = resampleTexture(_FoveaTex, IN.uv, rect);
            //     sum += tex2D(_MainTex, IN.uv);
            //     return 0.5*sum;
            //   }
            //   else
            //   {
            //     return tex2D(_MainTex, IN.uv);
            //   }
              
            // }

            fixed4 frag(v2f IN):SV_Target
            {
              if(_StandardDeviation == 0) return tex2D(_MainTex, IN.uv);

              float dist = distance(IN.uv, float2(_FoveaCoordinateX, _FoveaCoordinateY));
              
              if(dist < _E1 - 1e-3) // 1e-3 represent the thickness of the red circle
              {
                return resampleTexture(_FoveaTex, IN.uv, computeRect(_FoveaCoordinateX, _FoveaCoordinateY, _E1));
              }
              else if(dist >= _E1 - 1e-3 && dist <= _E1 + 1e-3) // red circle
              {
                return float4(1,0,0,1);
              }
              else if(dist > _E1 + 1e-3 && dist < _E2 - 1e-3)
              {
                return tex2D(_MainTex, IN.uv);
              }
              else if(dist >= _E2 - 1e-3 && dist <= _E2 + 1e-3) // red circle
              {
                return float4(1,0,0,1);
              }
              else
              {
                return tex2D(_MainTex, IN.uv);
              }
              // return tex2D(_FoveaTex, IN.uv);
            }

            ENDCG
        }

      // //Horizontal Blur
      // pass {
      //       CGPROGRAM
      //       #pragma vertex vert
      //       #pragma fragment frag
      //       #include "unitycg.cginc"

      //       #define PI 3.14159265359
			//       #define E 2.71828182846
      //       #define SAMPLES 100 //值越大，blur效果越柔和

      //       sampler2D _MainTex;
      //       sampler2D _FoveaTex;

      //       float4 _MainTex_ST;
      //       float4 _FoveaTex_ST;

      //       float _E1;
      //       float _E2;

      //       float _FoveaCoordinateX;
      //       float _FoveaCoordinateY;

      //       float _BlurSize;
			//       float _StandardDeviation;

      //       struct v2f {
      //           float4 pos:SV_POSITION;
      //           //float3 normal:NORMAL;
      //           float2 uv:TEXCOORD0;
      //       };
            
      //       fixed4 resampleTexture(sampler2D tex, float2 coord, float4 rect){
      //           float2 offset = float2(coord.x - rect.x, coord.y - rect.z);
      //           float2 scale = 1 / float2(abs(rect.x - rect.y), abs(rect.z - rect.w));
      //           return tex2D(tex, offset * scale);
      //       }

      //       float4 computeRect(float _FoveaCoordinateX, float _FoveaCoordinateY, float Eccentricity){
      //         return clamp(float4(_FoveaCoordinateX - Eccentricity, _FoveaCoordinateX + Eccentricity, 
      //                             _FoveaCoordinateY - Eccentricity, _FoveaCoordinateY + Eccentricity), 
      //                             0, 1);
      //       }

      //       v2f vert(appdata_base v)
      //       {
      //           v2f o;
      //           o.pos = UnityObjectToClipPos(v.vertex);
      //           o.uv = v.texcoord;
      //           return o;
      //       }

      //       fixed4 frag(v2f IN):SV_Target
      //       {
      //         if(_StandardDeviation == 0) return tex2D(_MainTex, IN.uv);

      //         float dist = distance(IN.uv, float2(_FoveaCoordinateX, _FoveaCoordinateY));
              
      //         if(dist < _E1 - 1e-3) // 1e-3 represent the thickness of the red circle
      //         {
      //           return resampleTexture(_FoveaTex, IN.uv, computeRect(_FoveaCoordinateX, _FoveaCoordinateY, _E1));
      //         }
      //         else if(dist >= _E1 - 1e-3 && dist <= _E1 + 1e-3) // red circle
      //         {
      //           return float4(1,0,0,1);
      //         }
      //         else if(dist > _E1 + 1e-3 && dist < _E2 - 1e-3)
      //         {
      //           return tex2D(_MainTex, IN.uv);
      //         }
      //         else if(dist >= _E2 - 1e-3 && dist <= _E2 + 1e-3) // red circle
      //         {
      //           return float4(1,0,0,1);
      //         }
      //         else
      //         {

      //           float invAspect = _ScreenParams.y / _ScreenParams.x;
      //           float4 pixelValue = 0;
      //           float sum = 0;

      //           //iterate over blur samples
      //           for(float index = 0; index < SAMPLES; index++){

      //             //get the offset of the sample
      //             float offset = (index/(SAMPLES-1) - 0.5) * _BlurSize * invAspect;
      //             //get uv coordinate of sample
      //             float2 uv = IN.uv + float2(0, offset);

      //             //calculate the result of the gaussian function
      //             float stDevSquared = _StandardDeviation * _StandardDeviation;
      //             float gauss = (1 / sqrt(2*PI*stDevSquared)) * pow(E, -((offset*offset)/(2*stDevSquared)));

      //             //add result to sum
			// 		        sum += gauss;
                  
      //             pixelValue += tex2D(_MainTex, uv) * gauss;
      //           }

      //           return pixelValue / sum;

      //         }
              
      //       }

      //       ENDCG
      //   }
    } 
}
