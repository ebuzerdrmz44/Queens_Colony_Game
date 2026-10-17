using UnityEngine;

namespace AntColony
{
    /// <summary>The autumn colour palette shared by all generated pixel art and the UI.</summary>
    public static class AutumnPalette
    {
        public static readonly Color Outline = Hex("2b1a12");
        public static readonly Color DarkBrown = Hex("4a2c18");
        public static readonly Color Brown = Hex("7a4a24");
        public static readonly Color Rust = Hex("b83a24");
        public static readonly Color Orange = Hex("e07a2e");
        public static readonly Color BurntOrange = Hex("c95a1e");
        public static readonly Color Gold = Hex("e8b23a");
        public static readonly Color Olive = Hex("6b7a2e");
        public static readonly Color DeepGreen = Hex("3d4a24");
        public static readonly Color GrassDark = Hex("5a6a28");
        public static readonly Color GrassLight = Hex("8a8a38");
        public static readonly Color Cream = Hex("f4e6c8");
        public static readonly Color Plum = Hex("4a2440");
        public static readonly Color PlumLight = Hex("7a3e6a");
        public static readonly Color EyeRed = Hex("ff4030");
        public static readonly Color Stone = Hex("8a7a6a");
        public static readonly Color StoneDark = Hex("5e5246");
        public static readonly Color Acid = Hex("8cc43c");
        public static readonly Color AcidLight = Hex("d4f07a");

        public static readonly Color SkyTop = Hex("5b6c9a");
        public static readonly Color SkyBottom = Hex("f2b46b");
        public static readonly Color Selection = Hex("f8e070");

        public static readonly Color Dirt = Hex("5c3a22");
        public static readonly Color DirtDark = Hex("4a2e1a");
        public static readonly Color DirtLight = Hex("6e4a2c");
        public static readonly Color Tunnel = Hex("26160d");
        public static readonly Color TunnelRim = Hex("3a2214");

        public static readonly Color[] LeafColors = { Orange, Rust, Gold, BurntOrange };

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out Color color);
            return color;
        }
    }
}
