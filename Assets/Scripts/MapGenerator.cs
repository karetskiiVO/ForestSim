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

    [SerializeField]
    int iterations = 10; // количество шагов релаксации
    [SerializeField]
    float dt = 1f;

    [Sirenix.OdinInspector.Button]
    void Generate() {
        var random = seed == ""
            ? new System.Random() 
            : new System.Random(seed.GetHashCode());

        var map = CreateMap(random);        
        var heighs = Enumerable.Range(0, map.GetLength(0))
            .SelectMany(i => Enumerable.Range(0, map.GetLength(1))
            .Select(j => map[i, j]));

        Debug.Log($"min: {heighs.Min()} max: {heighs.Max()}");

        terrain.terrainData.SetHeights(0, 0, map);

        var heighmap = new Texture2D(resolution.x, resolution.y, TextureFormat.RFloat, false, true);
        heighmap.SetPixelData(
            Enumerable.Range(0, resolution.x)
                .SelectMany(y => Enumerable.Range(0, resolution.y).Select(x => map[x, y]))
                .ToArray(),
            0
        );
        heighmap.Apply(false, false);

        var gradsRTexture = CalcGrads(heighmap);
        var moistureRTexture = CalcMoiste(heighmap, gradsRTexture);
        DebugDrawRFloatRenderTexture(moistureRTexture, "wet");

        gradsRTexture.Release();
        moistureRTexture.Release();
    }

    RenderTexture CalcMoiste(Texture heighmap, RenderTexture gradsField) {
        var moistureRTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
        };
        moistureRTexture.Create();
        Graphics.SetRenderTarget(moistureRTexture);
        GL.Clear(true, true, Color.black); // Очищаем черным (0)
        Graphics.SetRenderTarget(null);
        var moistureBufRTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
        };
        moistureBufRTexture.Create();
        Graphics.SetRenderTarget(moistureBufRTexture);
        GL.Clear(true, true, Color.black); // Очищаем черным (0)
        Graphics.SetRenderTarget(null);

        int gx = Mathf.CeilToInt(resolution.x / 8.0f);
        int gy = Mathf.CeilToInt(resolution.y / 8.0f);

        int initKernel = initShader.FindKernel("init");
        initShader.SetTexture(initKernel, "Moisture", moistureRTexture);
        initShader.SetInts("resolution", resolution.x, resolution.y); // важно!
        initShader.Dispatch(initKernel, gx, gy, 1);

        var moistureKernel = moistureShader.FindKernel("RelaxMoisture");
        moistureShader.SetTexture(moistureKernel, "heighmap", heighmap);
        moistureShader.SetTexture(moistureKernel, "GradientTex", gradsField);
        moistureShader.SetTexture(moistureKernel, "Moisture", moistureRTexture);
        moistureShader.SetTexture(moistureKernel, "MoistureBuf", moistureBufRTexture);
        
        for(int i = 0; i < iterations; i++)
        {
            moistureShader.SetVector("size", new Vector2(1, 1));
            moistureShader.SetVector("resolution", new Vector2(resolution.x, resolution.y));
            moistureShader.SetFloat("dt", dt);
            moistureShader.SetFloat("flowSpeed", 5.0f);
            moistureShader.SetInt("iteration", 0);
            moistureShader.SetFloat("maxMoisture", 1.0f);
            moistureShader.SetBool("copyMode", false);
            moistureShader.Dispatch(moistureKernel, gx, gy, 1);

            moistureShader.SetVector("size", new Vector2(1, 1));
            moistureShader.SetVector("resolution", new Vector2(resolution.x, resolution.y));
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

    RenderTexture CalcGrads(Texture heighmap) {
        var gradKernel = gradShader.FindKernel("grad");
        var gradsRTexture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGFloat)
        {
            enableRandomWrite = true,
            filterMode = FilterMode.Point,
        };
        gradsRTexture.Create();

        gradShader.SetVector("size", new Vector4(1, 1, 0, 0)); // Change to normal size
        gradShader.SetInts("resolution", resolution.x, resolution.y);

        gradShader.SetTexture(gradKernel, "heighmap", heighmap);
        gradShader.SetTexture(gradKernel, "Result", gradsRTexture);
        
        int gx = Mathf.CeilToInt(resolution.x / 8.0f);
        int gy = Mathf.CeilToInt(resolution.y / 8.0f);

        gradShader.Dispatch(gradKernel, gx, gy, 1);

        return gradsRTexture;
    }

    void DebugDrawRFloatRenderTexture (RenderTexture renderTexture, string outName = "debug") {
        RenderTexture.active = renderTexture;
        var textureRaw = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RFloat, false);
        textureRaw.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        textureRaw.Apply();
        RenderTexture.active = null;

        var pixelData = textureRaw.GetPixelData<float>(0);
        Debug.Log($"min: {pixelData.Min()}, max: {pixelData.Max()} cnt: {pixelData.Count(Single.IsNaN)}");

        // **НОРМАЛИЗАЦИЯ**: float moisture → Color (dry → wet)
        var textureGradient = new Gradient();
    
        // Настройка градиента по умолчанию
        var colorKeys = new GradientColorKey[3];
        colorKeys[0] = new GradientColorKey(new Color(0.6f, 0.4f, 0.2f), 0.0f); // Сухой
        colorKeys[1] = new GradientColorKey(new Color(0.3f, 0.6f, 0.2f), 0.5f); // Средний
        colorKeys[2] = new GradientColorKey(new Color(0.1f, 0.2f, 0.8f), 1.0f); // Мокрый
        
        var alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0] = new GradientAlphaKey(1.0f, 0.0f);
        alphaKeys[1] = new GradientAlphaKey(1.0f, 1.0f);
        
        textureGradient.SetKeys(colorKeys, alphaKeys);
        var colors = textureRaw.GetPixelData<float>(0)
            .Select(level => {
                float t = Mathf.Clamp01(level); // нормализация 0..1
                return textureGradient.Evaluate(t);
            })
            .ToArray();

        var texture = new Texture2D(renderTexture.width, renderTexture.height)
        {
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels(colors);
        texture.Apply(true, false); // true = перезаписать исходные данные
        
        // пока костыль, но пусть будет
        texture = TextureFlip.FlipHorizontal(texture);
        texture = TextureFlip.RotateTexture90CounterClockwise(texture);
    
        TerrainLayer textureLayer = new TerrainLayer();
        textureLayer.diffuseTexture = texture;  // ваша готовая текстура!
        textureLayer.normalMapTexture = null;
        textureLayer.tileSize = new Vector2(terrain.terrainData.size.x, terrain.terrainData.size.z);
        //moistureLayer.tileSize = new Vector3(1f, 0f, 1f);

        terrain.terrainData.terrainLayers = new TerrainLayer[] { textureLayer };
    
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

        string path = Path.Combine(Application.dataPath, outName+".png");
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.Refresh();
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

    public static class TextureFlip
    {
        // Зеркальное отражение по горизонтали
        public static Texture2D FlipHorizontal(Texture2D original)
        {
            int width = original.width;
            int height = original.height;
            
            Texture2D flipped = new Texture2D(width, height);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Берем пиксель из оригинальной позиции
                    Color pixel = original.GetPixel(x, y);
                    // Помещаем в зеркальную позицию по X
                    flipped.SetPixel(width - 1 - x, y, pixel);
                }
            }
            
            flipped.Apply();
            return flipped;
        }

        public static Texture2D FlipVertical(Texture2D original)
        {
            int width = original.width;
            int height = original.height;
            
            Texture2D flipped = new Texture2D(width, height);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color pixel = original.GetPixel(x, y);
                    // Помещаем в зеркальную позицию по Y
                    flipped.SetPixel(x, height - 1 - y, pixel);
                }
            }
            
            flipped.Apply();
            return flipped;
        }

        public static Texture2D FlipBoth(Texture2D original)
        {
            int width = original.width;
            int height = original.height;
            
            Texture2D flipped = new Texture2D(width, height);
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Color pixel = original.GetPixel(x, y);
                    // Отражаем и по X и по Y
                    flipped.SetPixel(width - 1 - x, height - 1 - y, pixel);
                }
            }
            
            flipped.Apply();
            return flipped;
        }
    
        public static Texture2D RotateTexture90Clockwise(Texture2D originalTexture)
        {
            Color32[] original = originalTexture.GetPixels32();
            Color32[] rotated = new Color32[original.Length];
            
            int width = originalTexture.width;
            int height = originalTexture.height;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int originalIndex = x + y * width;
                    int rotatedIndex = (height - 1 - y) + x * height;
                    rotated[rotatedIndex] = original[originalIndex];
                }
            }
            
            Texture2D rotatedTexture = new Texture2D(height, width);
            rotatedTexture.SetPixels32(rotated);
            rotatedTexture.Apply();
            
            return rotatedTexture;
        }

        // Поворот на 90 градусов против часовой стрелки
        public static Texture2D RotateTexture90CounterClockwise(Texture2D originalTexture)
        {
            Color32[] original = originalTexture.GetPixels32();
            Color32[] rotated = new Color32[original.Length];
            
            int width = originalTexture.width;
            int height = originalTexture.height;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int originalIndex = x + y * width;
                    int rotatedIndex = y + (width - 1 - x) * height;
                    rotated[rotatedIndex] = original[originalIndex];
                }
            }
            
            Texture2D rotatedTexture = new Texture2D(height, width);
            rotatedTexture.SetPixels32(rotated);
            rotatedTexture.Apply();
            
            return rotatedTexture;
        }

        // Поворот на 180 градусов
        public static Texture2D RotateTexture180(Texture2D originalTexture)
        {
            Color32[] original = originalTexture.GetPixels32();
            Color32[] rotated = new Color32[original.Length];
            
            int width = originalTexture.width;
            int height = originalTexture.height;
            
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int originalIndex = x + y * width;
                    int rotatedIndex = (width - 1 - x) + (height - 1 - y) * width;
                    rotated[rotatedIndex] = original[originalIndex];
                }
            }
            
            Texture2D rotatedTexture = new Texture2D(width, height);
            rotatedTexture.SetPixels32(rotated);
            rotatedTexture.Apply();
            
            return rotatedTexture;
        }
    }
}