using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using Sirenix.Utilities;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

class MapGenerator : MonoBehaviour
{
    [SerializeField]
    Terrain terrain;
    [SerializeField]
    string seed = "";
    [SerializeField]
    [Range(1, 10)]
    int octaves;
    [SerializeField]
    float persistence = 0.5f;
    [SerializeField]
    float scale = 0.01f;
    [SerializeField]
    Vector2Int resolution = Vector2Int.one * 512;
    [SerializeField]
    ComputeShader gradShader;
    [SerializeField]
    ComputeShader moistureShader, initShader;

    int iterations = 0; // количество шагов релаксации
    float dt = 0.05f;

    [Sirenix.OdinInspector.Button]
    void CreateNoise() {
        var random = seed == ""
            ? new System.Random() 
            : new System.Random(seed.GetHashCode());

        // init perm
        var map = CreateMap(random);        

        terrain.terrainData.SetHeights(0, 0, map);

        var gradKernel = gradShader.FindKernel("grad");
        var gradsRTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
        };
        gradsRTexture.Create();
        var moistureRTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
        };
        moistureRTexture.Create();
        var moistureBufRTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
        };
        moistureBufRTexture.Create();

        gradShader.SetVector("size", new Vector4(1, 1, 0, 0)); // Change to normal size
        gradShader.SetInts("resolution", resolution.x, resolution.y);

        var heighmapTextured = new Texture2D(resolution.x, resolution.y, TextureFormat.RFloat, false, true);
        heighmapTextured.SetPixelData(
            Enumerable.Range(0, resolution.x)
                .SelectMany(y => Enumerable.Range(0, resolution.y).Select(x => map[x, y]))
                .ToArray(),
            0
        );
        heighmapTextured.Apply(false, false);
        gradShader.SetTexture(gradKernel, "heighmap", heighmapTextured);
        gradShader.SetTexture(gradKernel, "Result", gradsRTexture);
        
        int gx = Mathf.CeilToInt(resolution.x / 8.0f);
        int gy = Mathf.CeilToInt(resolution.y / 8.0f);

        gradShader.Dispatch(gradKernel, gx, gy, 1);

        // RenderTexture.active = gradsRTexture;
        // var gradsTex = new Texture2D(
        //     gradsRTexture.width, 
        //     gradsRTexture.height, 
        //     TextureFormat.RGFloat,
        //     false
        // );
        // gradsTex.ReadPixels(new Rect(0, 0, gradsRTexture.width, gradsRTexture.height), 0, 0);
        // gradsTex.Apply();
        // RenderTexture.active = null;

        int initKernel = initShader.FindKernel("init");
        initShader.SetTexture(initKernel, "Moisture", moistureRTexture);
        initShader.SetInts("resolution", resolution.x, resolution.y); // важно!
        initShader.Dispatch(initKernel, gx, gy, 1);

        /*
        for(int i = 0; i < iterations; i++)
        {
            Debug.Log("aboba");

            var moistureKernel = moistureShader.FindKernel("RelaxMoisture");
            moistureShader.SetTexture(moistureKernel, "heighmap", heighmapTextured);
            moistureShader.SetTexture(moistureKernel, "GradientTex", gradsRTexture);
            moistureShader.SetTexture(moistureKernel, "Moisture", moistureRTexture);
            moistureShader.SetTexture(moistureKernel, "MoistureBuf", moistureBufRTexture);

            moistureShader.SetFloat("dt", dt);
            moistureShader.SetFloat("flowSpeed", 5.0f);
            moistureShader.SetInt("iteration", i);
            moistureShader.Dispatch(moistureKernel, gx, gy, 1);

            var cmd = new CommandBuffer
            {
                name = "Copy RenderTexture"
            };
            cmd.Blit(moistureBufRTexture, moistureRTexture);
            Graphics.ExecuteCommandBuffer(cmd);
            cmd.Dispose();
        }
        */

        RenderTexture.active = moistureRTexture;
        var wetRaw = new Texture2D(moistureRTexture.width, moistureRTexture.height, TextureFormat.RFloat, false);
        wetRaw.ReadPixels(new Rect(0, 0, moistureRTexture.width, moistureRTexture.height), 0, 0);
        wetRaw.Apply();
        RenderTexture.active = null;

        var pixelData = wetRaw.GetPixelData<float>(0);
        Debug.Log($"min: {pixelData.Min()}, max: {pixelData.Max()} cnt: {pixelData.Count(Single.IsNaN)}");

        // **НОРМАЛИЗАЦИЯ**: float moisture → Color (dry → wet)
        var moistureGradient = new Gradient();
    
        // Настройка градиента по умолчанию
        var colorKeys = new GradientColorKey[3];
        colorKeys[0] = new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 0.0f); // Сухой
        colorKeys[1] = new GradientColorKey(new Color(0.3f, 0.6f, 0.2f), 0.5f); // Средний
        colorKeys[2] = new GradientColorKey(new Color(0.1f, 0.2f, 0.8f), 1.0f); // Мокрый
        
        var alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
        
        moistureGradient.SetKeys(colorKeys, alphaKeys);
        var colors = wetRaw.GetPixelData<float>(0)
            .Select(level => {
                float t = Mathf.Clamp01(level); // нормализация 0..1
                return moistureGradient.Evaluate(t);
            })
            .ToArray();

        var wet = new Texture2D(moistureRTexture.width, moistureRTexture.height);
        wet.SetPixels(colors);
        wet.Apply(true, false); // true = перезаписать исходные данные

        TerrainLayer moistureLayer = new TerrainLayer();
        moistureLayer.diffuseTexture = wet;  // ваша готовая текстура!
        moistureLayer.normalMapTexture = null;
        //moistureLayer.tileSize = terrain.terrainData.size;
        //moistureLayer.tileSize = new Vector3(1f, 0f, 1f);

        terrain.terrainData.terrainLayers = new TerrainLayer[] { moistureLayer };
    
        // 3. Splatmap с одним слоем — вес всегда 1.0
        float[,,] alphamap = new float[terrain.terrainData.alphamapWidth, terrain.terrainData.alphamapHeight, 1];
        
        for (int x = 0; x < terrain.terrainData.alphamapWidth; x++)
        {
            for (int y = 0; y < terrain.terrainData.alphamapHeight; y++)
            {
                alphamap[x, y, 0] = 1.0f; // 100% вес для единственного слоя
            }
        }
        
        terrain.terrainData.SetAlphamaps(0, 0, alphamap);

        string path = Path.Combine(Application.dataPath, "wetTex.png");
        File.WriteAllBytes(path, wet.EncodeToPNG());
        AssetDatabase.Refresh();

        moistureRTexture.Release();
        gradsRTexture.Release();
    }

    float[,] CreateMap(System.Random random) {
        var perm = Enumerable.Range(0, 256)
            .OrderBy(_ => random.Next())
            .ToArray();
        
        perm = perm.Concat(perm).ToArray();

        var map = new float[resolution.x, resolution.y];
        for (var x = 0; x < resolution.x; x++) {
            for (var y = 0; y < resolution.y; y++) {
                var val = FractalNoise(new Vector2(x, y) * scale, perm);

                map[x, y] = val;
            }
        }

        return map;
    }

    float FractalNoise(in Vector2 p, int[] perm) {
        float total = 0;
        float frequency = 1;
        float amplitude = 1;
        float maxValue = 0;

        for (int i = 0; i < octaves; i++) {
            total += Noise(p, perm) * amplitude;
            maxValue += amplitude;
            amplitude *= persistence;
            frequency *= 2;
        }

        return total / maxValue;
    }

    float Noise(Vector3 p, int[] perm) {
        static float fade(float x) => x * x * x * (x * (x * 6 - 15) + 10);
        static float lerp(float t, float a, float b) => a + t * (b - a);
        static float grad(int hash, float x, float y, float z) {
            int h = hash & 15;
            float u = h < 8 ? x : y;
            float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
            return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
        };

        int xi = (int)MathF.Floor(p.x) & 255;
        int yi = (int)MathF.Floor(p.y) & 255;
        int zi = (int)MathF.Floor(p.z) & 255;

        float xf = p.x - MathF.Floor(p.x);
        float yf = p.y - MathF.Floor(p.y);
        float zf = p.z - MathF.Floor(p.z);

        float u = fade(xf);
        float v = fade(yf);
        float w = fade(zf);

        int aaa = perm[perm[perm[xi] + yi] + zi];
        int aba = perm[perm[perm[xi] + yi + 1] + zi];
        int aab = perm[perm[perm[xi] + yi] + zi + 1];
        int abb = perm[perm[perm[xi] + yi + 1] + zi + 1];
        int baa = perm[perm[perm[xi + 1] + yi] + zi];
        int bba = perm[perm[perm[xi + 1] + yi + 1] + zi];
        int bab = perm[perm[perm[xi + 1] + yi] + zi + 1];
        int bbb = perm[perm[perm[xi + 1] + yi + 1] + zi + 1];

        float x1 = lerp(u, grad(aaa, xf, yf, zf), grad(baa, xf - 1, yf, zf));
        float x2 = lerp(u, grad(aba, xf, yf - 1, zf), grad(bba, xf - 1, yf - 1, zf));
        float y1 = lerp(v, x1, x2);

        x1 = lerp(u, grad(aab, xf, yf, zf - 1), grad(bab, xf - 1, yf, zf - 1));
        x2 = lerp(u, grad(abb, xf, yf - 1, zf - 1), grad(bbb, xf - 1, yf - 1, zf - 1));
        float y2 = lerp(v, x1, x2);

        return (lerp(w, y1, y2) + 1) / 2;
    }
}