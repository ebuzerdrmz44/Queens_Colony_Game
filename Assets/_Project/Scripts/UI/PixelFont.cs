using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// A tiny 5x7 bitmap font drawn with the immediate-mode GUI, so the HUD text is real pixel art instead of a
    /// smooth system font. Capitals only (text is upper-cased); glyphs are generated into one texture at start.
    /// Sizes are "font pixels": the size of one glyph pixel in GUI units. They are snapped to whole screen
    /// pixels, so the letters stay crisp at any resolution.
    /// </summary>
    public class PixelFont
    {
        const int GlyphHeight = 7;
        const int CellWidth = 6;
        const int CellHeight = 8;
        const int Columns = 16;
        const int SpaceAdvance = 3;
        /// <summary>Line height in font pixels (7 for the glyph plus a gap).</summary>
        public const int LineHeight = 10;

        struct Glyph
        {
            public Rect Uv;
            public int Width;
        }

        readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();

        /// <summary>Screen pixels per GUI unit. Text starts on whole screen pixels, so every glyph pixel is the same size.</summary>
        public float GuiScale { get; set; } = 1f;
        readonly Texture2D atlas;

        public PixelFont()
        {
            List<KeyValuePair<char, string[]>> shapes = Shapes();
            int rows = Mathf.CeilToInt(shapes.Count / (float)Columns);
            atlas = new Texture2D(Columns * CellWidth, rows * CellHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
                name = "Pixel Font",
            };
            atlas.SetPixels(new Color[atlas.width * atlas.height]); // all clear

            for (int i = 0; i < shapes.Count; i++)
            {
                string[] rowsOfGlyph = shapes[i].Value;
                int width = 0;
                foreach (string row in rowsOfGlyph) width = Mathf.Max(width, row.Length);

                int cellX = (i % Columns) * CellWidth;
                int cellTop = atlas.height - (i / Columns) * CellHeight; // textures go bottom-up
                for (int y = 0; y < rowsOfGlyph.Length; y++)
                {
                    for (int x = 0; x < rowsOfGlyph[y].Length; x++)
                    {
                        if (rowsOfGlyph[y][x] == '#') atlas.SetPixel(cellX + x, cellTop - 1 - y, Color.white);
                    }
                }

                var uv = new Rect(
                    (float)cellX / atlas.width, (float)(cellTop - GlyphHeight) / atlas.height,
                    (float)width / atlas.width, (float)GlyphHeight / atlas.height);
                glyphs[shapes[i].Key] = new Glyph { Uv = uv, Width = width };
            }
            atlas.Apply();
        }

        public void Destroy()
        {
            Object.Destroy(atlas);
        }

        /// <summary>Width of one line of text in GUI units at font pixel size <paramref name="size"/>.</summary>
        public float Measure(string text, float size)
        {
            int pixels = 0;
            foreach (char c in text.ToUpperInvariant()) pixels += Advance(c);
            return Mathf.Max(0, pixels - 1) * size;
        }

        /// <summary>Height a text takes when wrapped to <paramref name="width"/>.</summary>
        public float MeasureHeight(string text, float size, float width)
        {
            return Wrap(text, size, width).Count * LineHeight * size;
        }

        /// <summary>
        /// Draws text inside a rectangle, wrapped to its width, aligned left / centre / right.
        /// Returns the height used. A drop shadow one font pixel down-right makes it readable on any background.
        /// </summary>
        public float Draw(Rect rect, string text, float size, Color color, TextAnchor alignment = TextAnchor.UpperLeft, bool shadow = true)
        {
            List<string> lines = Wrap(text, size, rect.width);
            float totalHeight = lines.Count * LineHeight * size;

            float y = rect.y;
            if (alignment == TextAnchor.MiddleLeft || alignment == TextAnchor.MiddleCenter || alignment == TextAnchor.MiddleRight)
                y = rect.y + (rect.height - totalHeight) * 0.5f + size; // centre the glyphs, not the line gaps

            Color previous = GUI.color;
            foreach (string line in lines)
            {
                float lineWidth = Measure(line, size);
                float x = rect.x;
                if (alignment == TextAnchor.UpperCenter || alignment == TextAnchor.MiddleCenter || alignment == TextAnchor.LowerCenter)
                    x = rect.x + (rect.width - lineWidth) * 0.5f;
                else if (alignment == TextAnchor.UpperRight || alignment == TextAnchor.MiddleRight || alignment == TextAnchor.LowerRight)
                    x = rect.xMax - lineWidth;

                x = Mathf.Round(x * GuiScale) / GuiScale;
                float lineY = Mathf.Round(y * GuiScale) / GuiScale;
                if (shadow)
                {
                    GUI.color = new Color(0.08f, 0.04f, 0.02f, color.a * 0.9f);
                    DrawLine(line, x + size, lineY + size, size);
                }
                GUI.color = color;
                DrawLine(line, x, lineY, size);
                y += LineHeight * size;
            }
            GUI.color = previous;
            return totalHeight;
        }

        void DrawLine(string line, float x, float y, float size)
        {
            foreach (char raw in line.ToUpperInvariant())
            {
                if (raw == ' ')
                {
                    x += SpaceAdvance * size;
                    continue;
                }

                Glyph glyph = GlyphFor(raw);
                GUI.DrawTextureWithTexCoords(new Rect(x, y, glyph.Width * size, GlyphHeight * size), atlas, glyph.Uv);
                x += (glyph.Width + 1) * size;
            }
        }

        int Advance(char c) => c == ' ' ? SpaceAdvance : GlyphFor(c).Width + 1;

        Glyph GlyphFor(char c)
        {
            if (glyphs.TryGetValue(c, out Glyph glyph)) return glyph;
            return glyphs['?'];
        }

        /// <summary>Splits text into lines no wider than <paramref name="width"/>, breaking at spaces and at '\n'.</summary>
        List<string> Wrap(string text, float size, float width)
        {
            var lines = new List<string>();
            foreach (string paragraph in text.Split('\n'))
            {
                string line = "";
                foreach (string word in paragraph.Split(' '))
                {
                    string attempt = line.Length == 0 ? word : line + " " + word;
                    if (line.Length > 0 && Measure(attempt, size) > width)
                    {
                        lines.Add(line);
                        line = word;
                    }
                    else
                    {
                        line = attempt;
                    }
                }
                lines.Add(line);
            }
            return lines;
        }

        /// <summary>Snaps a font pixel size so each glyph pixel covers a whole number of screen pixels.</summary>
        public static float Snap(float size, float guiScale)
        {
            return Mathf.Max(1f, Mathf.Round(size * guiScale)) / guiScale;
        }

        static List<KeyValuePair<char, string[]>> Shapes()
        {
            var shapes = new List<KeyValuePair<char, string[]>>();
            void Add(char c, params string[] rows) => shapes.Add(new KeyValuePair<char, string[]>(c, rows));

            Add('A', " ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #");
            Add('B', "#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### ");
            Add('C', " ### ", "#   #", "#    ", "#    ", "#    ", "#   #", " ### ");
            Add('D', "#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### ");
            Add('E', "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####");
            Add('F', "#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#    ");
            Add('G', " ### ", "#   #", "#    ", "# ###", "#   #", "#   #", " ####");
            Add('H', "#   #", "#   #", "#   #", "#####", "#   #", "#   #", "#   #");
            Add('I', "###", " # ", " # ", " # ", " # ", " # ", "###");
            Add('J', "  ###", "   # ", "   # ", "   # ", "#  # ", "#  # ", " ##  ");
            Add('K', "#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #");
            Add('L', "#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####");
            Add('M', "#   #", "## ##", "# # #", "# # #", "#   #", "#   #", "#   #");
            Add('N', "#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #");
            Add('O', " ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### ");
            Add('P', "#### ", "#   #", "#   #", "#### ", "#    ", "#    ", "#    ");
            Add('Q', " ### ", "#   #", "#   #", "#   #", "# # #", "#  # ", " ## #");
            Add('R', "#### ", "#   #", "#   #", "#### ", "# #  ", "#  # ", "#   #");
            Add('S', " ####", "#    ", "#    ", " ### ", "    #", "    #", "#### ");
            Add('T', "#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ");
            Add('U', "#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### ");
            Add('V', "#   #", "#   #", "#   #", "#   #", "#   #", " # # ", "  #  ");
            Add('W', "#   #", "#   #", "#   #", "# # #", "# # #", "# # #", " # # ");
            Add('X', "#   #", "#   #", " # # ", "  #  ", " # # ", "#   #", "#   #");
            Add('Y', "#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  ");
            Add('Z', "#####", "    #", "   # ", "  #  ", " #   ", "#    ", "#####");

            Add('0', " ### ", "#   #", "#  ##", "# # #", "##  #", "#   #", " ### ");
            Add('1', " # ", "## ", " # ", " # ", " # ", " # ", "###");
            Add('2', " ### ", "#   #", "    #", "   # ", "  #  ", " #   ", "#####");
            Add('3', "#####", "   # ", "  #  ", "   # ", "    #", "#   #", " ### ");
            Add('4', "   # ", "  ## ", " # # ", "#  # ", "#####", "   # ", "   # ");
            Add('5', "#####", "#    ", "#### ", "    #", "    #", "#   #", " ### ");
            Add('6', "  ## ", " #   ", "#    ", "#### ", "#   #", "#   #", " ### ");
            Add('7', "#####", "    #", "   # ", "  #  ", " #   ", " #   ", " #   ");
            Add('8', " ### ", "#   #", "#   #", " ### ", "#   #", "#   #", " ### ");
            Add('9', " ### ", "#   #", "#   #", " ####", "    #", "   # ", " ##  ");

            Add('.', " ", " ", " ", " ", " ", " ", "#");
            Add(',', "  ", "  ", "  ", "  ", "  ", " #", "# ");
            Add(':', " ", " ", "#", " ", " ", "#", " ");
            Add(';', "  ", "  ", " #", "  ", "  ", " #", "# ");
            Add('!', "#", "#", "#", "#", "#", " ", "#");
            Add('?', " ### ", "#   #", "    #", "   # ", "  #  ", "     ", "  #  ");
            Add('\'', "#", "#", " ", " ", " ", " ", " ");
            Add('"', "# #", "# #", "   ", "   ", "   ", "   ", "   ");
            Add('-', "    ", "    ", "    ", "####", "    ", "    ", "    ");
            Add('+', "     ", "  #  ", "  #  ", "#####", "  #  ", "  #  ", "     ");
            Add('=', "    ", "    ", "####", "    ", "####", "    ", "    ");
            Add('_', "    ", "    ", "    ", "    ", "    ", "    ", "####");
            Add('/', "    #", "   # ", "   # ", "  #  ", " #   ", " #   ", "#    ");
            Add('(', " #", "# ", "# ", "# ", "# ", "# ", " #");
            Add(')', "# ", " #", " #", " #", " #", " #", "# ");
            Add('[', "##", "# ", "# ", "# ", "# ", "# ", "##");
            Add(']', "##", " #", " #", " #", " #", " #", "##");
            Add('<', "   #", "  # ", " #  ", "#   ", " #  ", "  # ", "   #");
            Add('>', "#   ", " #  ", "  # ", "   #", "  # ", " #  ", "#   ");
            Add('%', "##  #", "## # ", "   # ", "  #  ", " #   ", " # ##", "#  ##");
            Add('*', "     ", "# # #", " ### ", "#####", " ### ", "# # #", "     ");
            Add('^', "  #  ", " # # ", "#   #", "     ", "     ", "     ", "     ");
            Add('|', "#", "#", "#", "#", "#", "#", "#");
            Add('#', " # # ", "#####", " # # ", " # # ", "#####", " # # ", "     ");
            Add('&', " ##  ", "#  # ", " ##  ", " # # ", "#   #", "#  # ", " ## #");
            return shapes;
        }
    }
}
