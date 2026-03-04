Shader "Custom/TerrainShader"
{
    Properties
    {
        _HeightMin       ("Height Min (world)",       Float)        =   0.0
        _HeightMax       ("Height Max (world)",       Float)        = 100.0

        // Grass colours
        _ColorGrassDark  ("Grass Dark",   Color) = (0.10, 0.22, 0.08, 1)
        _ColorGrassLight ("Grass Light",  Color) = (0.18, 0.36, 0.12, 1)
        _ColorGrassDry   ("Grass Dry",    Color) = (0.30, 0.36, 0.13, 1)

        // Ground / dirt colours
        _ColorDirt       ("Dirt",         Color) = (0.30, 0.22, 0.14, 1)
        _ColorDirtLight  ("Dirt Light",   Color) = (0.42, 0.33, 0.22, 1)

        // Noise scales (world units)
        _GrassScale      ("Grass scale (detail)",    Range(0.5, 30)) =  3.5
        _MacroScale      ("Macro scale (patches)",   Range(5,  300)) = 60.0

        // Intensity
        _GrassContrast   ("Grass Contrast",          Range(0, 1))  = 0.60
        _MacroStrength   ("Macro variation strength",Range(0, 1))  = 0.50
        _NormalStrength  ("Normal bump strength",    Range(0, 3))  = 1.20

        // Cliff
        _CliffAngle      ("Cliff angle (deg)",        Range(0,90)) = 33
        _CliffBlend      ("Cliff blend width (deg)",  Range(1,30)) = 14

        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _Metallic   ("Metallic",   Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        float  _HeightMin, _HeightMax;
        fixed4 _ColorGrassDark, _ColorGrassLight, _ColorGrassDry;
        fixed4 _ColorDirt, _ColorDirtLight;
        float  _GrassScale, _MacroScale;
        float  _GrassContrast, _MacroStrength, _NormalStrength;
        float  _CliffAngle, _CliffBlend;
        half   _Glossiness, _Metallic;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        // ── Noise ──────────────────────────────────────────────────────────

        inline float hash21(float2 p)
        {
            p = frac(p * float2(127.1, 311.7));
            p += dot(p, p.yx + 19.19);
            return frac((p.x + p.y) * p.x);
        }

        float valueNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            float2 u = f * f * (3.0 - 2.0 * f);
            return lerp(
                lerp(hash21(i + float2(0,0)), hash21(i + float2(1,0)), u.x),
                lerp(hash21(i + float2(0,1)), hash21(i + float2(1,1)), u.x),
                u.y);
        }

        float fbm(float2 p, int oct)
        {
            float v = 0, amp = 0.5, freq = 1.0;
            for (int i = 0; i < oct; i++)
            {
                v    += amp * valueNoise(p * freq);
                amp  *= 0.48;
                freq *= 2.13;
            }
            return v;
        }

        // Gradient of valueNoise — used to build a bump normal.
        float2 noiseGrad(float2 p)
        {
            const float eps = 0.04;
            float dx = valueNoise(p + float2(eps, 0)) - valueNoise(p - float2(eps, 0));
            float dy = valueNoise(p + float2(0, eps)) - valueNoise(p - float2(0, eps));
            return float2(dx, dy) / (2.0 * eps);
        }

        // Grass-blade pattern with warp + clumping
        float grassBlades(float2 p)
        {
            float warp    = valueNoise(p * 0.55) * 1.3 - 0.45;
            float2 bp     = float2(p.x + warp * 0.4, p.y * 2.6);
            float streaks = valueNoise(float2(bp.x * 5.8, bp.y * 0.65));
            streaks       = pow(saturate(streaks), 2.8);
            float clumps  = fbm(p * 0.45, 3);
            return saturate(streaks * 0.65 + clumps * 0.35);
        }

        // ──────────────────────────────────────────────────────────────────

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 worldN = normalize(IN.worldNormal);

            // ── slope ──
            float cosAngle   = abs(dot(worldN, float3(0,1,0)));
            float slopeAngle = degrees(acos(cosAngle));
            float cliffT     = saturate((slopeAngle - (_CliffAngle - _CliffBlend * 0.5))
                                          / max(_CliffBlend, 0.001));
            // Steeper = sharper dirt contrast
            float dirtVariation = valueNoise(IN.worldPos.xz * 0.18 + float2(3.7, 8.1));

            // ── detail grass UVs ──
            float2 uvDetail = IN.worldPos.xz / _GrassScale;
            float2 uvMacro  = IN.worldPos.xz / _MacroScale;

            // ── macro variation: large dry/lush patches ──
            float macro = fbm(uvMacro, 4);  // [0,1]

            // ── detail blade pattern ──
            float blade = grassBlades(uvDetail);

            // Combine blade with macro for richly varying grass
            float grassT = saturate(blade * _GrassContrast + (1.0 - _GrassContrast) * 0.5);

            fixed4 grassCol = lerp(_ColorGrassDark, _ColorGrassLight, grassT);
            // Patch in dry/yellow spots based on macro noise
            fixed4 dryCol   = lerp(grassCol, _ColorGrassDry,
                                   saturate((macro - 0.55) / 0.25) * _MacroStrength);

            // ── dirt variation ──
            fixed4 dirtCol = lerp(_ColorDirt, _ColorDirtLight, dirtVariation);

            // ── blend grass → dirt on cliffs ──
            fixed4 col = lerp(dryCol, dirtCol, cliffT);

            // ── procedural bump: gradient of grass noise → tangent-space normal ──
            float2 grad = noiseGrad(uvDetail) * _NormalStrength * (1.0 - cliffT);
            o.Normal     = normalize(float3(-grad.x, -grad.y, 1.0));

            // ── micro AO: darken grass "valleys" ──
            float microAO = lerp(0.72, 1.0, grassT) * lerp(0.80, 1.0, macro);
            microAO = lerp(microAO, 1.0, cliffT);

            o.Albedo     = col.rgb * microAO;
            o.Metallic   = _Metallic;
            o.Smoothness = lerp(_Glossiness, 0.03, cliffT);
            o.Alpha      = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}

