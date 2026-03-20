using UnityEngine;

namespace ProceduralVegetation {
    public static class LandscapeWaterGenerator {
        private const string GradShaderResourcePath = "ProceduralVegetation/grad";
        private const string MoistureShaderResourcePath = "ProceduralVegetation/moistre";
        private const string InitShaderResourcePath = "ProceduralVegetation/init";

        private static ComputeShader gradShader;
        private static ComputeShader moistureShader;
        private static ComputeShader initShader;

        public static Texture2D GenerateWaterMap(
            BakedLandscape baked,
            int iterations = 10,
            float dt = 1f
        ) {
            if (baked == null || baked.heightmap == null) return null;
            EnsureShadersReady();

            var heightmap = baked.heightmap;
            var grads = CalcGrads(heightmap, gradShader);
            var moisture = CalcMoisture(heightmap, grads, moistureShader, initShader, iterations, dt);

            var waterMap = ReadbackRFloatTexture(moisture);

            grads.Release();
            moisture.Release();

            return waterMap;
        }

        private static void LoadShader(ref ComputeShader shader, string resourcePath) {
            if (shader == null) {
                shader = Resources.Load<ComputeShader>(resourcePath);
            }

            if (shader == null) {
                Debug.LogError($"Failed to load shader at path: {resourcePath}");
                throw new System.Exception($"Failed to load shader at path: {resourcePath}");
            }
        }

        private static void EnsureShadersReady() {
            LoadShader(ref gradShader, GradShaderResourcePath);
            LoadShader(ref moistureShader, MoistureShaderResourcePath);
            LoadShader(ref initShader, InitShaderResourcePath);
}

        private static RenderTexture CalcGrads(Texture heightmap, ComputeShader gradShader) {
            int width = heightmap.width;
            int height = heightmap.height;

            var gradsRTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RGFloat) {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
            };
            gradsRTexture.Create();

            int gradKernel = gradShader.FindKernel("grad");

            gradShader.SetVector("size", new Vector4(1, 1, 0, 0));
            gradShader.SetInts("resolution", width, height);

            gradShader.SetTexture(gradKernel, "heightmap", heightmap);
            gradShader.SetTexture(gradKernel, "Result", gradsRTexture);

            int gx = Mathf.CeilToInt(width / 8.0f);
            int gy = Mathf.CeilToInt(height / 8.0f);

            gradShader.Dispatch(gradKernel, gx, gy, 1);

            return gradsRTexture;
        }

        private static RenderTexture CalcMoisture(
            Texture heightmap,
            RenderTexture gradsField,
            ComputeShader moistureShader,
            ComputeShader initShader,
            int iterations,
            float dt
        ) {
            int width = heightmap.width;
            int height = heightmap.height;

            var moistureRTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat) {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
            };
            moistureRTexture.Create();

            var moistureBufRTexture = new RenderTexture(width, height, 0, RenderTextureFormat.RFloat) {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
            };
            moistureBufRTexture.Create();

            int gx = Mathf.CeilToInt(width / 8.0f);
            int gy = Mathf.CeilToInt(height / 8.0f);

            int initKernel = initShader.FindKernel("init");
            initShader.SetTexture(initKernel, "Moisture", moistureRTexture);
            initShader.SetInts("resolution", width, height);
            initShader.Dispatch(initKernel, gx, gy, 1);

            int moistureKernel = moistureShader.FindKernel("RelaxMoisture");
            moistureShader.SetTexture(moistureKernel, "heightmap", heightmap);
            moistureShader.SetTexture(moistureKernel, "GradientTex", gradsField);
            moistureShader.SetTexture(moistureKernel, "Moisture", moistureRTexture);
            moistureShader.SetTexture(moistureKernel, "MoistureBuf", moistureBufRTexture);

            for (int i = 0; i < iterations; i++) {
                moistureShader.SetVector("size", new Vector2(1, 1));
                moistureShader.SetVector("resolution", new Vector2(width, height));
                moistureShader.SetFloat("dt", dt);
                moistureShader.SetFloat("flowSpeed", 5.0f);
                moistureShader.SetInt("iteration", 0);
                moistureShader.SetFloat("maxMoisture", 1.0f);
                moistureShader.SetBool("copyMode", false);
                moistureShader.Dispatch(moistureKernel, gx, gy, 1);

                moistureShader.SetVector("size", new Vector2(1, 1));
                moistureShader.SetVector("resolution", new Vector2(width, height));
                moistureShader.SetFloat("dt", dt);
                moistureShader.SetFloat("flowSpeed", 5.0f);
                moistureShader.SetInt("iteration", 0);
                moistureShader.SetFloat("maxMoisture", 1.0f);
                moistureShader.SetBool("copyMode", true);
                moistureShader.Dispatch(moistureKernel, gx, gy, 1);
            }

            moistureBufRTexture.Release();

            return moistureRTexture;
        }

        private static Texture2D ReadbackRFloatTexture(RenderTexture renderTexture) {
            var prevRt = RenderTexture.active;
            RenderTexture.active = renderTexture;

            var texture = new Texture2D(
                renderTexture.width,
                renderTexture.height,
                TextureFormat.RFloat,
                false,
                true
            ) {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0, false);
            texture.Apply(false, false);

            RenderTexture.active = prevRt;

            return texture;
        }
    }
}
