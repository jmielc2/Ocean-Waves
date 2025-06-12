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
            Texture2D<float4> normal_texture;
            float N;
            float L;

            struct VertexInput {
                float4 vertex : POSITION;
                float2 uv: TEXCOORD0;
            };

            struct FragmentInput {
                float4 vertex : SV_POSITION;
                float3 normal: NORMAL;
            };

            FragmentInput vertShader (VertexInput v) {
                float2 pos = v.uv * N;
                float3 displacement = displacement_texture[pos].xyz;
                v.vertex.xyz += displacement;
                FragmentInput o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = normal_texture[pos].xyz;
                return o;
            }

            fixed4 fragShader (FragmentInput i) : SV_Target {
                float3 lightDir = normalize(float3(1, 1, 1));
                float sim = clamp(dot(i.normal, lightDir), 0.0, 1.0);
                return fixed4(0.75, 0.75, 0.75, 1.0) * sim;
            }
            ENDCG
        }
    }
}