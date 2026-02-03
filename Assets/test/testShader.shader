Shader "Custom/PixelBlobUpscaler"
{
    Properties
    {
        _MainTex ("Low-Res Blob Texture", 2D)                   = "white" {}
        _NoiseTex("Noise texture", 2D)                          = "white" {}
        _UpscaleFactor ("Upscale Factor", Float)                = 8.0
        _Smoothness ("Border Smoothness", Range(0.001, 2.0))    = 0.5
        _Threshold ("Black/White Threshold", Range(0.0, 1.0))   = 0.5
        [Toggle] _OriginalEnable ("Original", Float)            = 0
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

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float4 _NoiseTex_TexelSize;

            float _UpscaleFactor;
            float _Smoothness;
            float _Threshold;

            bool _OriginalEnable;

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float getPixel (float2 uv) { return tex2D(_MainTex, uv).r; }

            float cubic (float x) {
                float x2 = x * x;
                float x3 = x2 * x;
                if (x < 1.0)
                    return (1.5 * x3 - 2.5 * x2 + 1.0);
                else if (x < 2.0)
                    return (-0.5 * x3 + 2.5 * x2 - 4.0 * x + 2.0);
                return 0.0;
            }

            float sampleBilinearSmooth (float2 uv) {
                float2 pixelPos = uv / _MainTex_TexelSize.xy;
                float2 floorPos = floor(pixelPos);
                float2 frac = pixelPos - floorPos;

                float2 texelSize = _MainTex_TexelSize.xy;
                float tl = getPixel((floorPos + float2(0.0, 0.0)) * texelSize);
                float tr = getPixel((floorPos + float2(1.0, 0.0)) * texelSize);
                float bl = getPixel((floorPos + float2(0.0, 1.0)) * texelSize);
                float br = getPixel((floorPos + float2(1.0, 1.0)) * texelSize);

                float top = lerp(tl, tr, frac.x);
                float bottom = lerp(bl, br, frac.x);
                float result = lerp(top, bottom, frac.y);

                return result;
            }

            float sampleAdvancedSmooth (float2 uv) {
                float2 pixelPos = uv / _MainTex_TexelSize.xy;
                float2 centerPos = floor(pixelPos) + 0.5;
                float2 offset = pixelPos - centerPos;

                float dist = length(offset);

                float centerValue = getPixel(centerPos * _MainTex_TexelSize.xy);

                float sum = 0.0;
                float totalWeight = 0.0;

                for (int x = -1; x <= 1; x++) {
                    for (int y = -1; y <= 1; y++) {
                        float2 samplePos = centerPos + float2(x, y);
                        float2 sampleUV = samplePos * _MainTex_TexelSize.xy;
                        float sampleValue = getPixel(sampleUV);

                        float2 toSample = float2(x, y) - offset;
                        float sampleDist = length(toSample) + 2 * tex2D(_NoiseTex, samplePos).r;

                        float weight = exp(-sampleDist * sampleDist / (_Smoothness * _Smoothness));

                        sum += sampleValue * weight;
                        totalWeight += weight;
                    }
                }

                return sum / totalWeight;
            }

            float sampleBicubic (float2 uv) {
                float2 pixelPos = uv / _MainTex_TexelSize.xy;
                float2 centerPos = floor(pixelPos);
                float2 frac = pixelPos - centerPos;
                
                float result = 0.0;
                
                for (int y = -1; y <= 2; y++) {
                    float yWeight = cubic(abs(frac.y - y));
                    for (int x = -1; x <= 2; x++) {
                        float xWeight = cubic(abs(frac.x - x));
                        float2 samplePos = (centerPos + float2(x, y)) * _MainTex_TexelSize.xy;
                        float sample = getPixel(samplePos);
                        result += sample * xWeight * yWeight;
                    }
                }
                
                return result;
            }

            fixed4 frag (v2f i) : SV_Target {
                float smoothValue;

                smoothValue = sampleAdvancedSmooth(i.uv);

                float edgeWidth = 0.1 / _UpscaleFactor;
                float result = smoothstep(_Threshold - edgeWidth, _Threshold + edgeWidth, smoothValue);

                result = result > 0.5 ? 1.0 : 0.0;
                fixed4 col = fixed4(result, result, result, 1.0);

                if (_OriginalEnable > 0.5) {
                    if (tex2D(_MainTex, i.uv).r >= 1) {
                        col = fixed4(1, 0, 0, 1);
                    }
                }

                return col;
            }
            ENDCG
        }
    }
}
