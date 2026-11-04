Shader "Hidden/ProceduralVegetation/CellIndexBake" {
    SubShader {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv1 : TEXCOORD1;
                float3 uv2 : TEXCOORD2;
            };

            struct v2f {
                float4 pos : SV_POSITION;
                float index : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                float3 centerRadius : TEXCOORD2;
            };

            v2f vert(appdata v) {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.index = v.uv1.x;
                o.worldPos = v.vertex.xy;
                o.centerRadius = v.uv2;
                return o;
            }

            float4 frag(v2f i) : SV_Target {
                float dist = distance(i.worldPos, i.centerRadius.xy);
                if (dist > i.centerRadius.z) {
                    discard;
                }
                return float4(i.index, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
