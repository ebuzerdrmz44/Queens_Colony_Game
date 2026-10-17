using System;
using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Turns pixel data into sprites at runtime. Pixels come either from text maps
    /// (one character per pixel, see <see cref="MapColors"/>) or from a Color[x, y] grid.
    /// </summary>
    public static class PixelArt
    {
        public const int PixelsPerUnit = 16;

        static readonly Dictionary<char, Color> MapColors = new Dictionary<char, Color>
        {
            { '.', Color.clear },
            { 'k', AutumnPalette.Outline },
            { 'd', AutumnPalette.DarkBrown },
            { 'b', AutumnPalette.Brown },
            { 'r', AutumnPalette.Rust },
            { 'o', AutumnPalette.Orange },
            { 'c', AutumnPalette.BurntOrange },
            { 'y', AutumnPalette.Gold },
            { 'g', AutumnPalette.Olive },
            { 'v', AutumnPalette.DeepGreen },
            { 'w', AutumnPalette.Cream },
            { 'p', AutumnPalette.Plum },
            { 'l', AutumnPalette.PlumLight },
            { 's', AutumnPalette.Stone },
            { 't', AutumnPalette.StoneDark },
            { 'a', AutumnPalette.Acid },
            { 'A', AutumnPalette.AcidLight },
            { 'e', AutumnPalette.EyeRed },
            { 'W', Color.white },
        };

        /// <summary>Builds a sprite from text rows. The first row is the top of the sprite.</summary>
        public static Sprite FromMap(string name, string[] rows)
        {
            int height = rows.Length;
            int width = rows[0].Length;
            var pixels = new Color[width, height];

            for (int row = 0; row < height; row++)
            {
                if (rows[row].Length != width)
                    throw new ArgumentException($"Pixel map '{name}': row {row} has {rows[row].Length} pixels, expected {width}.");

                int y = height - 1 - row; // text rows go top-down, textures go bottom-up
                for (int x = 0; x < width; x++)
                {
                    char symbol = rows[row][x];
                    if (!MapColors.TryGetValue(symbol, out Color color))
                        throw new ArgumentException($"Pixel map '{name}': unknown colour symbol '{symbol}'.");
                    pixels[x, y] = color;
                }
            }

            return FromPixels(name, pixels);
        }

        /// <summary>Builds a sprite from a pixel grid indexed as [x, y], with y = 0 at the bottom.</summary>
        public static Sprite FromPixels(string name, Color[,] pixels)
        {
            int width = pixels.GetLength(0);
            int height = pixels.GetLength(1);
            Texture2D texture = CreateTexture(name, width, height);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                    texture.SetPixel(x, y, pixels[x, y]);
            }

            return Finish(texture);
        }

        public static Texture2D CreateTexture(string name, int width, int height)
        {
            return new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        /// <summary>
        /// Uploads the texture and wraps it in a sprite with a centred pivot.
        /// FullRect meshes are needed so the sprite can be used in Tiled draw mode.
        /// </summary>
        public static Sprite Finish(Texture2D texture, float pixelsPerUnit = PixelsPerUnit)
        {
            texture.Apply();
            var rect = new Rect(0, 0, texture.width, texture.height);
            Sprite sprite = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = texture.name;
            return sprite;
        }

        /// <summary>Paints every empty pixel that touches a filled pixel, giving the shape a 1-pixel outline.</summary>
        public static void AddOutline(Color[,] pixels, Color outline)
        {
            int width = pixels.GetLength(0);
            int height = pixels.GetLength(1);
            var isOutline = new bool[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (IsFilled(pixels, x, y)) continue;
                    isOutline[x, y] = IsFilled(pixels, x - 1, y) || IsFilled(pixels, x + 1, y)
                                   || IsFilled(pixels, x, y - 1) || IsFilled(pixels, x, y + 1);
                }
            }

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (isOutline[x, y]) pixels[x, y] = outline;
                }
            }
        }

        static bool IsFilled(Color[,] pixels, int x, int y)
        {
            bool inside = x >= 0 && y >= 0 && x < pixels.GetLength(0) && y < pixels.GetLength(1);
            return inside && pixels[x, y].a > 0f;
        }
    }
}
