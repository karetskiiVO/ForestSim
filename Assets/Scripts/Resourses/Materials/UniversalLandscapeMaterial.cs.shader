Shader "Custom/UniversalLandscapeMaterial"
{
    Properties
    {
        _DirtTex ("Dirt Texture", 2D) = "white" {}
        _GrassTex ("Grass Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _Glossiness ("Global Glossiness", Range(0,1)) = 0.2
        _GrassSmoothness ("Grass Smoothness", Range(0,1)) = 0.15
        _DirtSmoothness ("Dirt Smoothness", Range(0,1)) = 0.05
        _SlopeStart ("Slope Start", Range(0,1)) = 0.05
        _SlopeEnd ("Slope End", Range(0,1)) = 0.3
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows

        // Use shader model 3.0 target, to get nicer looking lighting
        #pragma target 3.0

        sampler2D _DirtTex;
        sampler2D _GrassTex;
        fixed4 _Color;
        half _Glossiness;
        half _GrassSmoothness;
        half _DirtSmoothness;
        half _Metallic;
        float _SlopeStart;
        float _SlopeEnd;

        struct Input
        {
            float2 uv_DirtTex;
            float2 uv_GrassTex;
            float3 worldNormal;
        };

        // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
        // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
        // #pragma instancing_options assumeuniformscaling
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // compute slope: 0 = flat, 1 = vertical
            float3 n = normalize(IN.worldNormal);
            float slope = saturate(1.0 - dot(n, float3(0,1,0)));
            float t = smoothstep(_SlopeStart, _SlopeEnd, slope);

            fixed4 grass = tex2D(_GrassTex, IN.uv_GrassTex) * _Color;
            fixed4 dirt  = tex2D(_DirtTex, IN.uv_DirtTex) * _Color;
            fixed4 c = lerp(grass, dirt, t);

            // smoothness: interpolate between grass and dirt smoothness, modulated by global glossiness
            half smooth = lerp(_GrassSmoothness, _DirtSmoothness, t) * _Glossiness;
            smooth = saturate(smooth);

            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = smooth;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
