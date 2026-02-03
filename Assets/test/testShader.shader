Shader "Unlit/testShader"
{
    Properties
    {
        maskTexture  ("Texture", 2D) = "white" {}
        noiseTexture ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D noiseTexture;
            float4 noiseTexture_ST;

            sampler2D maskTexture;
            float4 maskTexture_ST;

            const float _ScaleFactor   = 1.0;
            const float _NoiseStrength = 10;
            const float _EdgeDetail    = 2.0;
            const float _NoiseScale    = 10;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, noiseTexture);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float scaleFactor = 10;
                float2 texelSize = 1.0 / float2(32, 32) / _ScaleFactor;

                fixed4 col = tex2D(noiseTexture, i.uv);

                float sum = 0;
                float weightSum = 0;

                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        float weight = 1.0 / (1.0 + abs(x) + abs(y));
                        float sample = tex2D(
                            maskTexture,
                            i.uv + float2(x, y) * texelSize * 0.5
                        ).r;
                        sum += sample * weight;
                        weightSum += weight;
                    }
                }

                float base = sum / weightSum;

                // Многослойный шум для детализации краёв
                float2 noiseUV = i.uv * _NoiseScale;
                float noise1 = tex2D(noiseTexture, noiseUV).r;
                float noise2 = tex2D(noiseTexture, noiseUV * 2.3).r;
                float noise3 = tex2D(noiseTexture, noiseUV * 4.7).r;

                // Комбинируем шумы с разными весами
                float noise = noise1 * 0.5 + noise2 * 0.35 + noise3 * 0.15;
                noise = noise * 2.0 - 1.0; // Приводим к диапазону [-1, 1]

                // Усиливаем шум на границах
                float edgeMask = saturate(abs(base - 0.5) * _EdgeDetail);
                noise *= _NoiseStrength * (1.0 + edgeMask * 2.0);

                // Пороговое значение с шумом
                float threshold = 0.5 + noise * 0.25;

                // Изменяемая ширина границы в зависимости от шума
                float edgeWidth = 0.05 + abs(noise) * 0.1;

                // Создаём результат с шумом на границах
                float result = smoothstep(threshold - edgeWidth, threshold + edgeWidth, base);

                // Добавляем дополнительную детализацию на границах
                if (result > 0.1 && result < 0.9) {
                    // Добавляем высокочастотный шум на границы
                    float fineNoise = tex2D(noiseTexture, i.uv * 8.0).r * 2.0 - 1.0;
                    result += fineNoise * 0.15 * (1.0 - abs(result - 0.5) * 2.0);
                    result = saturate(result);
                }

                col = float4(result, result, result, 1.0);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
