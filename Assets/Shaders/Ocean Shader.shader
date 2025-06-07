Shader "Custom/Ocean Shader" {
    Properties {
        displacement_texture ("Texture", 2D) = "black" {}
    }

    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vertShader
            #pragma fragment fragShader

            #include "UnityCG.cginc"

            Texture2D<float4> displacement_texture;
            float N;
            float L;

            struct VertexInput {
                float4 vertex : POSITION;
                float2 uv: COORD1;
            };

            struct FragmentInput {
                float4 vertex : SV_POSITION;
            };

            FragmentInput vertShader (VertexInput v) {
                float2 pos = ((v.vertex / L) + 0.5f) * N;
                float height = displacement_texture[pos];
                v.vertex.y += height * 500.0f;
                FragmentInput o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 fragShader (FragmentInput i) : SV_Target {
                return fixed4(0.5, 0.5, 0.5, 1.0);
            }
            ENDCG
        }
    }
}