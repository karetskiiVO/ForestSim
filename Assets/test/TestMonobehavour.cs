using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

public class TestMonobehavour : MonoBehaviour
{
    [SerializeField]
    Material material;

    [SerializeField]
    Texture2D sample;

    [Button]
    void Draw ()
    {
        var scale = 5f;
        var (width, height) = (512, 512);

        var noiseTex = new Texture2D(width, height);
        var pix = Enumerable.Range(0, width*height)
            .Select(idx => {
                var ix = idx % width;
                var iy = idx / width;

                var x = (scale * ix) / width;
                var y = (scale * iy) / height;

                return Mathf.PerlinNoise(x, y);
            })
            .Select(v => new Color(v, v, v))
            .ToArray();

        noiseTex.SetPixels(pix);
        noiseTex.Apply();

        material.SetTexture("noiseTexture", noiseTex);
        material.SetTexture("maskTexture", sample);
    }
}
