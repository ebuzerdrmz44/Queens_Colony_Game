using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Generated autumn pixel art. Used for anything that has no imported sprite assigned
    /// in <see cref="ArtOverrides"/>. Map legend: see PixelArt.MapColors
    /// (k outline, d/b browns, r rust, o orange, c burnt orange, y gold, g olive, v deep green, w cream,
    /// p/l plum, e eye red, s/t stone, a/A acid green, W white).
    /// </summary>
    public static class PixelSprites
    {
        // ---------- Side-view ant for the nest: 16x16, four walking frames, facing right ----------

        public static Sprite[] AntWalk() => Frames("Ant", new[]
        {
            new[]
            {
                "................",
                ".............kk.",
                "............k...",
                "...........k....",
                "............k...",
                "...........kkk..",
                "..kkkk....kooWk.",
                ".kooook.kkkrrkk.",
                "korrrrokookrrrk.",
                "krrrrrrorrkrrrkk",
                "krrrrrrkkk.kkk..",
                ".krrrrk.kkk.....",
                "..kkkk.kk..kk...",
                "......kk.....k..",
                "......k.k.....k.",
                "......k.k.....k.",
            },
            new[]
            {
                "................",
                ".............kk.",
                "............k...",
                "...........k....",
                "............k...",
                "...........kkk..",
                "..kkkk....kooWk.",
                ".kooook.kkkrrkk.",
                "korrrrokookrrrk.",
                "krrrrrrorrkrrrkk",
                "krrrrrrkkk.kkk..",
                ".krrrrk.kkk.....",
                "..kkkkkk.k.k....",
                ".....k...k..k...",
                ".....k...k...k..",
                ".........k......",
            },
            new[]
            {
                "................",
                ".............kk.",
                "............k...",
                "...........k....",
                "............k...",
                "...........kkk..",
                "..kkkk....kooWk.",
                ".kooook.kkkrrkk.",
                "korrrrokookrrrk.",
                "krrrrrrorrkrrrkk",
                "krrrrrrkkk.kkk..",
                ".krrrrk.kkk.....",
                "..kkkkkk..kk....",
                ".....k....k.k...",
                "....k.....k.k...",
                "....k.....k.k...",
            },
            new[]
            {
                "................",
                ".............kk.",
                "............k...",
                "...........k....",
                "............k...",
                "...........kkk..",
                "..kkkk....kooWk.",
                ".kooook.kkkrrkk.",
                "korrrrokookrrrk.",
                "krrrrrrorrkrrrkk",
                "krrrrrrkkk.kkk..",
                ".krrrrk.kkk.....",
                "..kkkkkk.k.k....",
                ".....k...k..k...",
                ".....k...k..k...",
                ".....k.......k..",
            },
        });

        // ---------- Attack poses: frame 1 winds up, frame 2 strikes ----------

        public static Sprite[] AntAttack() => Frames("Ant Attack", new[]
        {
            new[]
            {
                "................",
                ".........k......",
                "..........k.....",
                "...........k....",
                "............k...",
                "...........kkk..",
                "..kkkk....kooWk.",
                ".kooook.kkkrrkkk",
                "korrrrokookrrrk.",
                "krrrrrrorrkrrrk.",
                "krrrrrrkkk.kkk.k",
                ".krrrrk.kkk...k.",
                "..kkkk.k.k.kkk..",
                "......k..k......",
                ".....k..k.......",
                "....k...k.......",
            },
            new[]
            {
                "................",
                "................",
                "..............kk",
                ".............k..",
                "............k...",
                "...........kkk..",
                "..kkkk....kooWk.",
                ".kooook.kkkrrkk.",
                "korrrrokookrrrkk",
                "krrrrrrorrkrrrkk",
                "krrrrrrkkk.kkk..",
                ".krrrrk.kkk.....",
                "..kkkkkk.k.kk...",
                ".....k...k...k..",
                "....k...k.....kk",
                "...k...k........",
            },
        });

        // ---------- Top-down creatures for the surface: seen from above, facing right ----------

        public static Sprite[] AntTopWalk() => Frames("Ant Top", new[]
        {
            new[]
            {
                "........k.......",
                ".....k..k.....k.",
                ".....k...k...k.k",
                "......k..k..k.k.",
                "..kkkkk..k.k..k.",
                ".kooookkkk.kkk..",
                "korrrrokookookkk",
                "kroorrrorrorrrk.",
                "krrrrrrrrrrrrrk.",
                "krrrrrrkrrkrrkkk",
                ".krrrrkkkk.kkk..",
                "..kkkkk..k.k..k.",
                ".....k...k.k..k.",
                "....k....k..k..k",
                "...k......k.k...",
                "..........k.....",
            },
            new[]
            {
                ".........k......",
                "....k....k...k..",
                ".....k...k..k..k",
                ".....k...k..k.k.",
                "..kkkkk..k.k..k.",
                ".kooookkkk.kkk..",
                "korrrrokookookkk",
                "kroorrrorrorrrk.",
                "krrrrrrrrrrrrrk.",
                "krrrrrrkrrkrrkkk",
                ".krrrrkkkk.kkk..",
                "..kkkkk..k.k..k.",
                ".....k...k..k.k.",
                ".....k...k..k..k",
                "....k....k...k..",
                ".........k......",
            },
            new[]
            {
                "..........k.....",
                "...k......k.k...",
                "....k....k..k..k",
                ".....k...k.k..k.",
                "..kkkkk..k.k..k.",
                ".kooookkkk.kkk..",
                "korrrrokookookkk",
                "kroorrrorrorrrk.",
                "krrrrrrrrrrrrrk.",
                "krrrrrrkrrkrrkkk",
                ".krrrrkkkk.kkk..",
                "..kkkkk..k.k..k.",
                "......k..k..k.k.",
                ".....k...k...k.k",
                ".....k..k.....k.",
                "........k.......",
            },
            new[]
            {
                ".........k......",
                "....k....k...k..",
                ".....k...k..k..k",
                ".....k...k..k.k.",
                "..kkkkk..k.k..k.",
                ".kooookkkk.kkk..",
                "korrrrokookookkk",
                "kroorrrorrorrrk.",
                "krrrrrrrrrrrrrk.",
                "krrrrrrkrrkrrkkk",
                ".krrrrkkkk.kkk..",
                "..kkkkk..k.k..k.",
                ".....k...k..k.k.",
                ".....k...k..k..k",
                "....k....k...k..",
                ".........k......",
            },
        });

        public static Sprite[] AntTopAttack() => Frames("Ant Top Attack", new[]
        {
            new[]
            {
                ".........k......",
                "....k....k..k...",
                ".....k...k..k...",
                ".....k...k.kk...",
                "..kkkkk..k.kk..k",
                ".kooookkkk.kkkk.",
                "korrrrokookookk.",
                "kroorrrorrorrrk.",
                "krrrrrrrrrrrrrk.",
                "krrrrrrkrrkrrkk.",
                ".krrrrkkkk.kkkk.",
                "..kkkkk..k.kk..k",
                ".....k...k.kk...",
                ".....k...k..k...",
                "....k....k..k...",
                ".........k......",
            },
            new[]
            {
                ".........k......",
                "....k....k....k.",
                ".....k...k...k..",
                ".....k...k..k..k",
                "..kkkkk..k.k..k.",
                ".kooookkkk.kkk..",
                "korrrrokookookk.",
                "kroorrrorrorrrkk",
                "krrrrrrrrrrrrrkk",
                "krrrrrrkrrkrrkk.",
                ".krrrrkkkk.kkk..",
                "..kkkkk..k.k..k.",
                ".....k...k..k..k",
                ".....k...k...k..",
                "....k....k....k.",
                ".........k......",
            },
        });

        public static Sprite[] SpiderTopWalk() => Frames("Spider Top", new[]
        {
            new[]
            {
                "........k.k....k",
                ".kk.....k..k...k",
                "...kk....k..k.k.",
                ".....kk..k..k.k.",
                "...kkkkk..k.kk..",
                "..kllllkkkkk.k..",
                ".klrpprlkkkke...",
                ".kpprrppkppelk..",
                ".kpprrppkppepk..",
                ".kprpprpkkkke...",
                "..kppppkkkkk.k..",
                "...kkkkk..k.kk..",
                "......k..k..k.k.",
                "....kk...k..k.k.",
                "...k...kk...k.k.",
                "......k.....k.k.",
            },
            new[]
            {
                ".......k...k...k",
                "..k.....k...k..k",
                "...kkk...k..k.k.",
                "......k..k..k.k.",
                "...kkkkk..k.kk..",
                "..kllllkkkkk.k..",
                ".klrpprlkkkke...",
                ".kpprrppkppelk..",
                ".kpprrppkppepk..",
                ".kprpprpkkkke...",
                "..kppppkkkkk.k..",
                "...kkkkk..k.kk..",
                ".....kk..k..k.k.",
                "....k....k..k.k.",
                "..kk....k...k..k",
                ".......k...k...k",
            },
            new[]
            {
                "......k.....k.k.",
                "...k...kk...k.k.",
                "....kk...k..k.k.",
                "......k..k..k.k.",
                "...kkkkk..k.kk..",
                "..kllllkkkkk.k..",
                ".klrpprlkkkke...",
                ".kpprrppkppelk..",
                ".kpprrppkppepk..",
                ".kprpprpkkkke...",
                "..kppppkkkkk.k..",
                "...kkkkk..k.kk..",
                ".....kk..k..k.k.",
                "...kk....k..k.k.",
                ".kk.....k..k...k",
                "........k.k....k",
            },
            new[]
            {
                ".......k...k...k",
                "..k.....k...k..k",
                "...kkk...k..k.k.",
                "......k..k..k.k.",
                "...kkkkk..k.kk..",
                "..kllllkkkkk.k..",
                ".klrpprlkkkke...",
                ".kpprrppkppelk..",
                ".kpprrppkppepk..",
                ".kprpprpkkkke...",
                "..kppppkkkkk.k..",
                "...kkkkk..k.kk..",
                ".....kk..k..k.k.",
                "....k....k..k.k.",
                "..kk....k...k..k",
                ".......k...k...k",
            },
        });

        public static Sprite[] SpiderTopAttack() => Frames("Spider Top Attack", new[]
        {
            new[]
            {
                ".......k...k....",
                "..k.....k...k...",
                "...kkk...k..k...",
                "......k..k..k...",
                "...kkkkk..k.k.k.",
                "..kllllkkkkk.k.k",
                ".klrpprlkkkke...",
                ".kpprrppkppelw..",
                ".kpprrppkppepw..",
                ".kprpprpkkkke...",
                "..kppppkkkkk.k.k",
                "...kkkkk..k.k.k.",
                ".....kk..k..k...",
                "....k....k..k...",
                "..kk....k...k...",
                ".......k...k....",
            },
            new[]
            {
                ".......k...k....",
                "..k.....k...k...",
                "...kkk...k..k...",
                "......k..k..k...",
                "...kkkkk..k.k...",
                "..kllllkkkkk..kk",
                ".klrpprlkkkkek.k",
                ".kpprrppkppelw.k",
                ".kpprrppkppepw.k",
                ".kprpprpkkkkek.k",
                "..kppppkkkkk..kk",
                "...kkkkk..k.k...",
                ".....kk..k..k...",
                "....k....k..k...",
                "..kk....k...k...",
                ".......k...k....",
            },
        });

        public static Sprite[] BeetleTopWalk() => Frames("Beetle Top", new[]
        {
            new[]
            {
                "......k.........",
                "...k..k......k..",
                "...k..k.....k...",
                "....kkkkk..k....",
                "..kkkggkgkk.....",
                ".kggWWvvvgkkk...",
                "kgvvvvvvvvgggkkk",
                "kvvvvvvvvvvvvggb",
                "kvvvvvvvvvvvvvvb",
                "kvvvvvvvvvvvvkkk",
                ".kvvvvvvvvkkk...",
                "..kkkvvkvkk.....",
                "...kkkkkk.k.....",
                "..k.....k..k....",
                ".k......k..k....",
                "........k.......",
            },
            new[]
            {
                ".......k........",
                "..k....k....k...",
                "...k...k...k....",
                "...kkkkkk..k....",
                "..kkkggkgkk.....",
                ".kggWWvvvgkkk...",
                "kgvvvvvvvvgggkkk",
                "kvvvvvvvvvvvvggb",
                "kvvvvvvvvvvvvvvb",
                "kvvvvvvvvvvvvkkk",
                ".kvvvvvvvvkkk...",
                "..kkkvvkvkk.....",
                "...kkkkkk..k....",
                "...k...k...k....",
                "..k....k....k...",
                ".......k........",
            },
            new[]
            {
                "........k.......",
                ".k......k..k....",
                "..k.....k..k....",
                "...kkkkkk.k.....",
                "..kkkggkgkk.....",
                ".kggWWvvvgkkk...",
                "kgvvvvvvvvgggkkk",
                "kvvvvvvvvvvvvggb",
                "kvvvvvvvvvvvvvvb",
                "kvvvvvvvvvvvvkkk",
                ".kvvvvvvvvkkk...",
                "..kkkvvkvkk.....",
                "....kkkkk..k....",
                "...k..k.....k...",
                "...k..k......k..",
                "......k.........",
            },
            new[]
            {
                ".......k........",
                "..k....k....k...",
                "...k...k...k....",
                "...kkkkkk..k....",
                "..kkkggkgkk.....",
                ".kggWWvvvgkkk...",
                "kgvvvvvvvvgggkkk",
                "kvvvvvvvvvvvvggb",
                "kvvvvvvvvvvvvvvb",
                "kvvvvvvvvvvvvkkk",
                ".kvvvvvvvvkkk...",
                "..kkkvvkvkk.....",
                "...kkkkkk..k....",
                "...k...k...k....",
                "..k....k....k...",
                ".......k........",
            },
        });

        public static Sprite[] BeetleTopAttack() => Frames("Beetle Top Attack", new[]
        {
            new[]
            {
                ".....k..........",
                "k....k....k.....",
                ".k....k...k.....",
                "..kkkkkk.k......",
                ".kkkggkgkk......",
                "kggWWvvvgkkk....",
                "gvvvvvvvvgggkk..",
                "vvvvvvvvvvvvggk.",
                "vvvvvvvvvvvvvvk.",
                "vvvvvvvvvvvvkk..",
                "kvvvvvvvvkkk....",
                ".kkkvvkvkk......",
                "..kkkkkk.k......",
                ".k....k...k.....",
                "k....k....k.....",
                ".....k..........",
            },
            new[]
            {
                "........k.......",
                "...k....k....k..",
                "...k....k...k...",
                "....kkkkk..k....",
                "..kkkggkgkk.....",
                ".kggWWvvvgkkk...",
                "kgvvvvvvvvgggkkk",
                "kvvvvvvvvvvvvggy",
                "kvvvvvvvvvvvvvvy",
                "kvvvvvvvvvvvvkkk",
                ".kvvvvvvvvkkk...",
                "..kkkvvkvkk.....",
                "....kkkkk..k....",
                "...k....k...k...",
                "...k....k....k..",
                "........k.......",
            },
        });

        // ---------- Food (16x16) ----------

        public static Sprite Leaf() => PixelArt.FromMap("Leaf", new[]
        {
            "................",
            ".......kk.......",
            "......kook......",
            "..kk..koyk..kk..",
            "..kokkoyyokkok..",
            "...koooyooook...",
            ".kkkoyoyoyookkk.",
            "kooooooyooooook.",
            ".kooyoyyyoyook..",
            "..kooooyooook...",
            "...kkooyookk....",
            ".....kkykk......",
            ".......bk.......",
            ".......bk.......",
            "......kbk.......",
            "................",
        });

        public static Sprite Acorn() => PixelArt.FromMap("Acorn", new[]
        {
            "................",
            ".......kk.......",
            ".......bk.......",
            "....kkkkkkkk....",
            "...kbdbdbdbdk...",
            "..kdbdbdbdbdbk..",
            "..kbdbdbdbdbdk..",
            "..kkkkkkkkkkkk..",
            "...kwyyyyyyok...",
            "...kyyyyyyyok...",
            "...kyyyyyyook...",
            "....kyyyyyok....",
            "....kyyyyook....",
            ".....kyyook.....",
            "......kkkk......",
            "................",
        });

        public static Sprite Berry() => PixelArt.FromMap("Berry", new[]
        {
            "................",
            ".........kkk....",
            "........kggvk...",
            ".......kkgvk....",
            "....kkkkbkk.....",
            "...kkrrrkrrkk...",
            "..krrwwrrrrrrk..",
            ".krrwWrrrrrrrrk.",
            ".krrwrrrrrrrrrk.",
            ".krrrrrrrrrrrck.",
            ".krrrrrrrrrrrck.",
            ".kcrrrrrrrrrcck.",
            "..kcrrrrrrrcck..",
            "...kccrrrccck...",
            "....kkkkkkkk....",
            "................",
        });

        /// <summary>Tiny bit of food a worker carries on its back.</summary>
        public static Sprite Crumb() => PixelArt.FromMap("Crumb", new[]
        {
            ".o.",
            "oyo",
            ".o.",
        });

        // ---------- Decorations ----------

        public static Sprite MushroomTop() => PixelArt.FromMap("Mushroom Top", new[]
        {
            "................",
            "................",
            ".....kkkkkk.....",
            "....krrrrrrk....",
            "...krrwwrrrrk...",
            "..krrrwwrrwwrk..",
            "..krrrrrrrwwrk..",
            "..krwwrrrrrrrk..",
            "..krwwrrrwwrck..",
            "..krrrrrrwwrck..",
            "..kcrrwwrrrcck..",
            "...kcrwwrrcck...",
            "....kccccccck...",
            ".....kkkkkkk....",
            "................",
            "................",
        });

        public static Sprite Pebble() => PixelArt.FromMap("Pebble", new[]
        {
            "........",
            "..kkkk..",
            ".kswwsk.",
            "kssssssk",
            "kssssssk",
            "ksssssdk",
            ".kdssdk.",
            "..kkkk..",
        });

        public static Sprite GrassTuft() => PixelArt.FromMap("Grass Tuft", new[]
        {
            "y...y",
            ".y.o.",
            "oy.yo",
            "yoyoy",
        });

        /// <summary>White so it can be tinted with any leaf colour.</summary>
        public static Sprite FallingLeaf() => PixelArt.FromMap("Falling Leaf", new[]
        {
            ".WW",
            "WWW",
            "WW.",
        });

        public static Sprite Egg() => PixelArt.FromMap("Egg", new[]
        {
            "................",
            "......kkkk......",
            ".....kwwwwk.....",
            "....kwWWwwwk....",
            "....kwWwwwwk....",
            "...kwWwwwwwwk...",
            "...kwwwwwwwwk...",
            "...kwwwwwwwwk...",
            "...kwwwwwwwyk...",
            "...kwwwwwwwyk...",
            "...kwwwwwwyyk...",
            "....kwwwwyyk....",
            "....kwwwyyyk....",
            ".....kyyyyk.....",
            "......kkkk......",
            "................",
        });

        public static Sprite RallyFlag() => PixelArt.FromMap("Rally Flag", new[]
        {
            ".kk.............",
            "kyyk............",
            ".kkkkkkkkkkk....",
            ".kbkrrrrrrrrkk..",
            ".kbkrorrrrrrrrk.",
            ".kbkroorrrrrrk..",
            ".kbkrrrrrrrrk...",
            ".kbkrrrrrrrrrk..",
            ".kbkkkrrrrrrrrk.",
            ".kbk..kkkkkkkk..",
            ".kbk............",
            ".kbk............",
            ".kbk............",
            ".kbk............",
            "kdbdk...........",
            "kkkkk...........",
        });

        /// <summary>The rally flag waving: the pole stays put while a wave rolls out along the cloth.</summary>
        public static Sprite[] RallyFlagWaving() => Frames("Rally Flag Waving", new[]
        {
            new[]
            {
                ".kk.............",
                "kyyk..kkk.......",
                ".kkkkkrrkkk.....",
                ".kbkrrrrrrkkkkk.",
                ".kbkroorrrrrkrk.",
                ".kbkrorrrrrrrkk.",
                ".kbkrrrrrrrrrk..",
                ".kbkrrrrrrrrkkk.",
                ".kbkkkkkrrrrrrk.",
                ".kbk....kkrrrkk.",
                ".kbk......kkkk..",
                ".kbk............",
                ".kbk............",
                ".kbk............",
                "kdbd............",
                "kkkk............",
            },
            new[]
            {
                ".kk.............",
                "kyyk....kkkk....",
                ".kkkkkkkrrrkk...",
                ".kbkrrrrrrrrk...",
                ".kbkrorrrrrrkkk.",
                ".kbkroorrrrrrrk.",
                ".kbkrrrrrrrrrkk.",
                ".kbkrrrrrrrrkk..",
                ".kbkkkrrkkkrrkk.",
                ".kbk..kkk..krrk.",
                ".kbk........kkk.",
                ".kbk............",
                ".kbk............",
                ".kbk............",
                "kdbd............",
                "kkkk............",
            },
            new[]
            {
                ".kk.............",
                "kyyk......kkk...",
                ".kkkkkk.kkrrkk..",
                ".kbkrrkkrrrrrkk.",
                ".kbkrorrrrrrrrk.",
                ".kbkrorrrrrrkkk.",
                ".kbkrrorrrrrrk..",
                ".kbkrrrrrrrrrkk.",
                ".kbkkkrrrrkkkrk.",
                ".kbk.krrkkk..kk.",
                ".kbk..kkk.......",
                ".kbk............",
                ".kbk............",
                ".kbk............",
                "kdbd............",
                "kkkk............",
            },
            new[]
            {
                ".kk.............",
                "kyyk............",
                ".kkkkkkkk..kkkk.",
                ".kbkrrrrkkkrrrk.",
                ".kbkrorrrrrrrkk.",
                ".kbkroorrrrrkk..",
                ".kbkrrrrrrrrrkk.",
                ".kbkrrrrrrrrrrk.",
                ".kbkkkrrrrrrkkk.",
                ".kbk..kkrrrkk...",
                ".kbk....kkkk....",
                ".kbk............",
                ".kbk............",
                ".kbk............",
                "kdbd............",
                "kkkk............",
            },
        });

        // ---------- Nest furniture (side view, one 16x16 cell per nest tile) ----------

        /// <summary>A grub, two frames so it can squirm.</summary>
        public static Sprite[] Larva() => Frames("Larva", new[]
        {
            new[]
            {
                "................",
                "................",
                "................",
                ".....kkkkkk.....",
                "....kwwwwwwkk...",
                "...kwwywwywywk..",
                "..kwywwwwwwwwk..",
                ".kwwwwwwwwwwwyk.",
                ".kwwwWwwwwwwwwk.",
                ".kywWwwkkwwwwwk.",
                ".kwwwwwkkwwbbwk.",
                ".kwwwwk.kwbbbwk.",
                "..kkkk...kwbbk..",
                "..........kkk...",
                "................",
                "................",
            },
            new[]
            {
                "................",
                "................",
                "................",
                ".....kkkkkk.....",
                "...kkwwwwwwk....",
                "..kwwywywwywk...",
                "..kwwwwwwwwwyk..",
                ".kwywwwwwwwwwwk.",
                ".kwwwWwwwwwwwwk.",
                ".kwwWwwkkwwbbwk.",
                ".kywwwwkkwwbbbk.",
                ".kwwwwwk.kwbbwk.",
                "..kwwwk...kkkk..",
                "...kkk..........",
                "................",
                "................",
            },
        });

        /// <summary>A bed of leaves for one egg or larva (1x1 tiles).</summary>
        public static Sprite Cradle() => PixelArt.FromMap("Cradle", new[]
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "................",
            "..kkkkkkkkkkkk..",
            ".kgvvvvvvvvvvgk.",
            "kgvvvvvvvvvvvvgk",
            "ggygyygyygyygygg",
            "kggggggggggggggk",
        });

        /// <summary>A heap of seeds, berries and crumbs (2x1 tiles).</summary>
        public static Sprite StoragePile() => PixelArt.FromMap("Storage Pile", new[]
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "................................",
            "...........kkkkkkkkkk...........",
            "........kkkyyyyyyyyyykkk........",
            "......kkyyybobrbbbbbbyyykk......",
            "....kkbboobobbbbbbbyobbbbokk....",
            "...kbbybobybbbybbbbbbbbbbbbbk...",
            "..kbbybbbbobobbbrbbbbbbbbbobbk..",
            ".kbbbbbbbbrbbbbobbbdbbbbbbbybbk.",
            ".kbbbbbybbbbdoybroobbobobbbbbbk.",
            ".kbdbrybbbbybbbbbbbbbbybbbbbybk.",
        });

        /// <summary>Spongy fungus growing on chewed leaves (2x1 tiles).</summary>
        public static Sprite FungusGarden() => PixelArt.FromMap("Fungus Garden", new[]
        {
            "................................",
            "................................",
            "................................",
            "................................",
            "............kkkkkkkk............",
            "..........kkwwwwwwwwkk..........",
            ".........kwwwwWwwdwwwWk.........",
            "...kkkkkkkwwwwwwwwwwwwkkkkkkk...",
            "..kwwwwwwwwwwwwwwwdwwwwwwwwwwk..",
            ".kWwwwwwwwwwwywwwwwwwwwwWWwwwwk.",
            "kwwwwwwwwwwwwwwwwwwwwwwwwwwwwwwk",
            "kwwWwwwwwwwwwwwwwwwwwwwwWWwwwWwk",
            "kwwwwwwwwwwwwwwwwwwwwwwdwwwwWwwk",
            "kwwwwWwwwwwdwwwwwwwwwwwwwwwwwwwk",
            ".kwwWwgwwwwwwwwowWwwwwwwwowwwWk.",
            "..kwwoWWwwwgwwowowwwowwwgwwwwk..",
        });

        // ---------- Defenses (top-down) ----------

        /// <summary>One block of a barricade: stones packed in dirt.</summary>
        public static Sprite WallBlock() => PixelArt.FromMap("Wall", new[]
        {
            "..kkkkkkkkkkkk..",
            ".kddkdbddkkkddk.",
            "kdkwwwkbkwwwkkdk",
            "kkwssswkwssswkdk",
            "kksssssktssstkbk",
            "kktsssskktttkkdk",
            "kbkttsswwkkkwwkk",
            "kdbdksssswkksskk",
            "kdkkkssssskkstkk",
            "kkwwwststswwskbk",
            "kwssssbbksssswkk",
            "ktsssbkdkssssskk",
            "kkttbkdbkttsttkk",
            "kdkbkdddbkktkkdk",
            ".kbdbddddbddddk.",
            "..kkkkkkkkkkkk..",
        });

        /// <summary>A stone turret around a pool of ant acid.</summary>
        public static Sprite Tower() => PixelArt.FromMap("Tower", new[]
        {
            ".....kkkkkk.....",
            "...kkwwttwwkk...",
            "..kwwsssssswwk..",
            ".kwssssddsssswk.",
            ".ksssddddddsssk.",
            "kwssddaaaaddsswk",
            "ksssdaAAaaadsssk",
            "ktsddaAaaaaddstk",
            "ktsddaaaaaaddstk",
            "ksssdaaaaaadsssk",
            "ktssddaaaaddsstk",
            ".ksssddddddsssk.",
            ".ktssssddsssstk.",
            "..kttssssssttk..",
            "...kkttttttkk...",
            ".....kkkkkk.....",
        });

        /// <summary>A blob of acid shot by towers.</summary>
        public static Sprite AcidShot() => PixelArt.FromMap("Acid Shot", new[]
        {
            ".kk.",
            "kAak",
            "kaak",
            ".kk.",
        });

        // ---------- Procedural shapes ----------

        /// <summary>A 1x1 world-unit white square. Tint and scale it to draw rectangles.</summary>
        public static Sprite Pixel()
        {
            Texture2D texture = PixelArt.CreateTexture("Pixel", 1, 1);
            texture.SetPixel(0, 0, Color.white);
            return PixelArt.Finish(texture, pixelsPerUnit: 1);
        }

        /// <summary>A 16x16 tile of autumn meadow seen from above, meant to be drawn in Tiled mode.</summary>
        public static Sprite GrassTile()
        {
            var random = new System.Random(5);
            var pixels = new Color[16, 16];
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                    pixels[x, y] = RandomGrass(random);
            }
            return PixelArt.FromPixels("Meadow", pixels);
        }

        /// <summary>A round autumn tree canopy seen from above, shaded toward the bottom right.</summary>
        public static Sprite TreeTop(int seed)
        {
            const int size = 32;
            var random = new System.Random(seed);
            var pixels = new Color[size, size];

            Color[] leafColors = AutumnPalette.LeafColors;
            Color mainColor = leafColors[seed % leafColors.Length];
            Vector3[] blobs =
            {
                new Vector3(16, 16, 10),
                new Vector3(9, 18, 6),
                new Vector3(23, 14, 6),
                new Vector3(15, 24, 6),
                new Vector3(17, 8, 6),
            };

            foreach (Vector3 blob in blobs)
            {
                float centerX = blob.x + random.Next(-1, 2);
                float centerY = blob.y + random.Next(-1, 2);
                float radius = blob.z;

                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        float distance = Mathf.Sqrt(dx * dx + dy * dy);
                        if (distance > radius) continue;

                        Color leaf = random.NextDouble() < 0.7 ? mainColor : leafColors[random.Next(leafColors.Length)];
                        // Light comes from the top left: darken the far side of each blob.
                        float shadow = Mathf.Clamp01((dx - dy) / (radius * 1.4f));
                        pixels[x, y] = Color.Lerp(leaf, AutumnPalette.DarkBrown, shadow * 0.45f);
                    }
                }
            }

            PixelArt.AddOutline(pixels, AutumnPalette.Outline);
            return PixelArt.FromPixels($"Tree Top {seed + 1}", pixels);
        }

        /// <summary>A leafy bush seen from above: overlapping blobs of late-summer green with a few red berries.</summary>
        public static Sprite BushTop(int seed)
        {
            const int size = 20;
            var random = new System.Random(seed * 31 + 5);
            var pixels = new Color[size, size];
            Color[] greens = { AutumnPalette.Olive, AutumnPalette.GrassLight, AutumnPalette.DeepGreen };
            Vector3[] blobs =
            {
                new Vector3(10, 10, 6),
                new Vector3(6, 11, 4),
                new Vector3(14, 9, 4),
                new Vector3(10, 14, 4),
            };

            foreach (Vector3 blob in blobs)
            {
                float centerX = blob.x + random.Next(-1, 2);
                float centerY = blob.y + random.Next(-1, 2);
                for (int x = 0; x < size; x++)
                {
                    for (int y = 0; y < size; y++)
                    {
                        float dx = x - centerX;
                        float dy = y - centerY;
                        if (Mathf.Sqrt(dx * dx + dy * dy) > blob.z) continue;

                        Color leaf = random.NextDouble() < 0.65 ? greens[0] : greens[random.Next(greens.Length)];
                        float shadow = Mathf.Clamp01((dx - dy) / (blob.z * 1.4f));
                        pixels[x, y] = Color.Lerp(leaf, AutumnPalette.DarkBrown, shadow * 0.5f);
                    }
                }
            }

            for (int i = 0; i < 4; i++)
            {
                int x = random.Next(5, 15);
                int y = random.Next(6, 15);
                if (pixels[x, y].a > 0f) pixels[x, y] = AutumnPalette.Rust;
            }

            PixelArt.AddOutline(pixels, AutumnPalette.Outline);
            return PixelArt.FromPixels($"Bush Top {seed}", pixels);
        }

        /// <summary>A mossy grey boulder seen from above, lit from the top left.</summary>
        public static Sprite Boulder(int seed)
        {
            const int width = 24;
            const int height = 20;
            var random = new System.Random(seed * 17 + 3);
            var pixels = new Color[width, height];
            float radiusX = 10f + random.Next(-1, 2);
            float radiusY = 8f + random.Next(-1, 1);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dx = (x + 0.5f - width / 2f) / radiusX;
                    float dy = (y + 0.5f - height / 2f) / radiusY;
                    float wobble = (float)random.NextDouble() * 0.08f;
                    if (dx * dx + dy * dy > 1f - wobble) continue;

                    float light = dx - dy; // positive toward the bottom right
                    Color stone = AutumnPalette.Stone;
                    if (light > 0.6f) stone = AutumnPalette.StoneDark;
                    else if (light < -0.7f) stone = Color.Lerp(AutumnPalette.Stone, AutumnPalette.Cream, 0.35f);
                    if (dy > 0.25f && random.NextDouble() < 0.3) stone = AutumnPalette.Olive; // moss on the top
                    pixels[x, y] = stone;
                }
            }

            PixelArt.AddOutline(pixels, AutumnPalette.Outline);
            return PixelArt.FromPixels($"Boulder {seed}", pixels);
        }

        public static Sprite StumpTop() => PixelArt.FromMap("Stump Top", new[]
        {
            "................",
            ".....kkkkkk.....",
            "...kkddddddkk...",
            "..kddbbbbbbddk..",
            "..kdbbyyyybbdk..",
            ".kdbbyybbyybbdk.",
            ".kdbybbyybbybdk.",
            ".kdbybyyyybybdk.",
            ".kdbybybbybybdk.",
            ".kdbybbyybbybdk.",
            ".kdbbyybbyybbdk.",
            "..kdbbyyyybbdk..",
            "..kddbbbbbbddk..",
            "...kkddddddkk...",
            ".....kkkkkk.....",
            "................",
        });

        public static Sprite FallenLog() => PixelArt.FromMap("Fallen Log", new[]
        {
            "..kkkkkkkkkkkkkkkkkkkk..",
            ".kddbbdbbbbdbbbbbdbbbkk.",
            "kbbbbbbbdbbbbbbdbbbbkyyk",
            "kbdbbbbbbbbbdbbbbbbbkyby",
            "kbbbbdbbbbbbbbbbbdbbkyyk",
            "kdbbbbbbbdbbbbbbbbbbkyyk",
            ".kddddddddddddddddddkkk.",
            "..kkkkkkkkkkkkkkkkkkkk..",
        });

        /// <summary>A small white flower (tint it); the centre stays gold-ish.</summary>
        public static Sprite Flower() => PixelArt.FromMap("Flower", new[]
        {
            ".W.",
            "WyW",
            ".W.",
        });

        /// <summary>A thin white circle with a faint fill, for showing a tower's range (tint it).</summary>
        public static Sprite RangeCircle()
        {
            const int size = 96;
            var pixels = new Color[size, size];
            float radius = size / 2f - 0.5f;
            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x + 0.5f - size / 2f;
                    float dy = y + 0.5f - size / 2f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    if (distance > radius) continue;
                    pixels[x, y] = distance > radius - 1.5f ? Color.white : new Color(1f, 1f, 1f, 0.12f);
                }
            }
            return PixelArt.FromPixels("Range Circle", pixels);
        }

        /// <summary>A soft dark oval for shadows under trees and boulders (tint the alpha).</summary>
        public static Sprite Shadow()
        {
            const int width = 24;
            const int height = 16;
            var pixels = new Color[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dx = (x + 0.5f - width / 2f) / (width / 2f);
                    float dy = (y + 0.5f - height / 2f) / (height / 2f);
                    if (dx * dx + dy * dy <= 1f) pixels[x, y] = Color.black;
                }
            }
            return PixelArt.FromPixels("Shadow", pixels);
        }

        /// <summary>The dirt mound over the nest entrance seen from above: rings of soil around a dark hole.</summary>
        public static Sprite AntHillTop()
        {
            const int size = 32;
            var random = new System.Random(7);
            var pixels = new Color[size, size];
            float center = size / 2f;

            for (int x = 0; x < size; x++)
            {
                for (int y = 0; y < size; y++)
                {
                    float dx = x + 0.5f - center;
                    float dy = y + 0.5f - center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) / (center - 1f);
                    if (distance > 1f) continue;

                    if (distance < 0.22f) pixels[x, y] = AutumnPalette.Tunnel;
                    else if (distance < 0.32f) pixels[x, y] = AutumnPalette.DirtDark;
                    else if (distance < 0.55f && dx - dy < 0f) pixels[x, y] = AutumnPalette.DirtLight; // sunlit slope
                    else pixels[x, y] = RandomDirt(random);
                }
            }

            PixelArt.AddOutline(pixels, AutumnPalette.Outline);
            return PixelArt.FromPixels("Ant Hill", pixels);
        }

        /// <summary>A white oval outline drawn under selected units (tint it).</summary>
        public static Sprite SelectionRing()
        {
            const int width = 20;
            const int height = 20;
            var pixels = new Color[width, height];
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    float dx = (x + 0.5f - width / 2f) / (width / 2f);
                    float dy = (y + 0.5f - height / 2f) / (height / 2f);
                    float distance = dx * dx + dy * dy;
                    if (distance <= 1f && distance >= 0.72f) pixels[x, y] = Color.white;
                }
            }
            return PixelArt.FromPixels("Selection Ring", pixels);
        }

        /// <summary>A 16x16 tile of speckled soil, meant to be drawn in Tiled mode.</summary>
        public static Sprite DirtTile()
        {
            var random = new System.Random(11);
            var pixels = new Color[16, 16];
            for (int x = 0; x < 16; x++)
            {
                for (int y = 0; y < 16; y++)
                    pixels[x, y] = RandomDirt(random);
            }
            return PixelArt.FromPixels("Dirt", pixels);
        }

        // ---------- Helpers ----------

        static Sprite[] Frames(string name, string[][] maps)
        {
            var frames = new Sprite[maps.Length];
            for (int i = 0; i < maps.Length; i++)
                frames[i] = PixelArt.FromMap($"{name} {i + 1}", maps[i]);
            return frames;
        }

        static Color RandomDirt(System.Random random)
        {
            double roll = random.NextDouble();
            if (roll < 0.15) return AutumnPalette.DirtDark;
            if (roll < 0.25) return AutumnPalette.DirtLight;
            if (roll < 0.27) return AutumnPalette.Stone;
            return AutumnPalette.Dirt;
        }

        /// <summary>Mostly faded green, with dry gold and a few fallen-leaf specks.</summary>
        static Color RandomGrass(System.Random random)
        {
            double roll = random.NextDouble();
            if (roll < 0.45) return AutumnPalette.Olive;
            if (roll < 0.72) return AutumnPalette.GrassDark;
            if (roll < 0.9) return AutumnPalette.GrassLight;
            if (roll < 0.97) return AutumnPalette.Gold;
            return AutumnPalette.Orange;
        }
    }
}
