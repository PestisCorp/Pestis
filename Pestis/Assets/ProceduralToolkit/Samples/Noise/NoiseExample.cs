using ProceduralToolkit.FastNoiseLib;
using UnityEngine;
using UnityEngine.UI;

namespace ProceduralToolkit.Samples
{
    public class NoiseExample : ConfiguratorBase
    {
        public RawImage image;

        private const int width = 512;
        private const int height = 512;

        private Color[] pixels;
        private Texture2D texture;
        private FastNoise noise;

        private void Awake()
        {
            pixels = new Color[width*height];
            texture = PTUtils.CreateTexture(width, height, Color.clear);
            image.texture = texture;

            noise = new FastNoise();
            Generate();
            SetupSkyboxAndPalette();
        }

        private void Update()
        {
            UpdateSkybox();
        }

        private void Generate()
        {
            noise.SetNoiseType(FastNoise.NoiseType.Cellular);

            GeneratePalette();

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float value = noise.GetNoise01(x, y);
                    pixels[y*width + x] = GetMainColorHSV().WithSV(value, value).ToColor();
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }
    }
}
