using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Small pixel-art motion on the surface, all stepped (a few frames per second, whole sprite pixels):
    /// - grass tufts and flowers lean one pixel back and forth, in a wave that rolls across the meadow,
    /// - now and then a gust of wind sweeps over the field: what it passes leans further, and bushes rustle,
    /// - tree canopies sway one pixel, slowly,
    /// - a few butterflies flap around the meadow.
    /// </summary>
    public class SurfaceLife : MonoBehaviour
    {
        public enum Kind { Grass, Flower, Bush, Canopy }

        /// <summary>Frames per second of the sway (low on purpose: chunky pixel-art timing).</summary>
        const float SwayFps = 4f;
        const float ButterflyFps = 8f;
        const int ButterflyCount = 6;
        const float ButterflySpeed = 1.1f;
        const float ButterflyWidth = 0.32f;
        const float GustWidth = 3f;
        const float GustSpeed = 9f;
        const float GustEverySeconds = 9f;

        struct Swayer
        {
            public Transform Visual;
            public Vector3 Rest;
            /// <summary>One sprite pixel, in the visual's local units.</summary>
            public float Pixel;
            public Kind Kind;
            public int Phase;
        }

        class Butterfly
        {
            public SpriteRenderer Sprite;
            public Vector2 Position;
            public Vector2 Target;
            public int Frame;
        }

        static readonly int[] GrassLean = { 0, 0, 1, 1, 0, 0, -1, -1 };
        static readonly int[] CanopyLean = { 0, 0, 0, 1, 1, 1 };
        static readonly int[] Rustle = { 1, -1, 1, 0 };
        static readonly Color[] WingColors = { AutumnPalette.Cream, AutumnPalette.Gold, AutumnPalette.Orange, AutumnPalette.PlumLight };

        readonly List<Swayer> swayers = new List<Swayer>();
        readonly List<Butterfly> butterflies = new List<Butterfly>();
        Rect field;
        Sprite[] wingFrames;
        float clock;
        int swayFrame = -1;
        int butterflyFrame = -1;
        float gustX = float.NegativeInfinity;
        float nextGust = 3f;

        public void Init(GameArt art, Rect playArea, System.Random random)
        {
            field = playArea;
            wingFrames = new[]
            {
                PixelArt.FromMap("Butterfly Open", new[]
                {
                    "WW.WW",
                    "WWkWW",
                    ".WkW.",
                    "..k..",
                }),
                PixelArt.FromMap("Butterfly Closed", new[]
                {
                    ".WkW.",
                    ".WkW.",
                    "..k..",
                    ".....",
                }),
            };

            for (int i = 0; i < ButterflyCount; i++)
            {
                SpriteRenderer sprite = art.CreateSprite("Butterfly", transform, wingFrames[0], SortingOrder.FallingLeaves - 1);
                sprite.color = WingColors[random.Next(WingColors.Length)];
                GameArt.ScaleToWidth(sprite, ButterflyWidth);
                var butterfly = new Butterfly { Sprite = sprite, Position = RandomPoint(random), Frame = random.Next(2) };
                butterfly.Target = RandomPoint(random);
                sprite.transform.position = butterfly.Position;
                butterflies.Add(butterfly);
            }
        }

        /// <summary>Makes a piece of scenery sway. Its sprite moves by whole pixels of its own art.</summary>
        public void Add(SpriteRenderer visual, Kind kind)
        {
            if (visual == null || visual.sprite == null) return;
            Transform visualTransform = visual.transform;
            Vector3 position = visualTransform.position;
            swayers.Add(new Swayer
            {
                Visual = visualTransform,
                Rest = visualTransform.localPosition,
                Pixel = visual.sprite.bounds.size.x / Mathf.Max(1f, visual.sprite.rect.width)
                        * visualTransform.localScale.x,
                Kind = kind,
                // Neighbours sway together and the wave rolls from left to right.
                Phase = Mathf.FloorToInt(-position.x * 0.6f),
            });
        }

        Vector2 RandomPoint(System.Random random)
        {
            return new Vector2(
                field.xMin + (float)random.NextDouble() * field.width,
                field.yMin + (float)random.NextDouble() * field.height);
        }

        void Update()
        {
            clock += Time.deltaTime;

            // Gusts: a band of wind crossing the field now and then.
            nextGust -= Time.deltaTime;
            if (nextGust <= 0f)
            {
                nextGust = GustEverySeconds * Random.Range(0.6f, 1.4f);
                gustX = field.xMin - GustWidth * 2f;
            }
            if (gustX > float.NegativeInfinity) gustX += GustSpeed * Time.deltaTime;
            if (gustX > field.xMax + GustWidth * 4f) gustX = float.NegativeInfinity;

            int frame = Mathf.FloorToInt(clock * SwayFps);
            if (frame != swayFrame)
            {
                swayFrame = frame;
                UpdateSway(frame);
            }

            int wingFrame = Mathf.FloorToInt(clock * ButterflyFps);
            if (wingFrame != butterflyFrame)
            {
                butterflyFrame = wingFrame;
                UpdateButterflies();
            }
        }

        void UpdateSway(int frame)
        {
            for (int i = swayers.Count - 1; i >= 0; i--)
            {
                Swayer swayer = swayers[i];
                if (swayer.Visual == null)
                {
                    swayers.RemoveAt(i);
                    continue;
                }

                bool inGust = Mathf.Abs(swayer.Visual.position.x - gustX) < GustWidth;
                int lean;
                switch (swayer.Kind)
                {
                    case Kind.Bush:
                        lean = inGust ? Rustle[(frame + swayer.Phase) & 3] : 0;
                        break;
                    case Kind.Canopy:
                        lean = CanopyLean[Mod(frame + swayer.Phase, CanopyLean.Length)] + (inGust ? 1 : 0);
                        break;
                    default:
                        lean = inGust ? 2 : GrassLean[Mod(frame + swayer.Phase, GrassLean.Length)];
                        break;
                }
                swayer.Visual.localPosition = swayer.Rest + new Vector3(lean * swayer.Pixel, 0f, 0f);
            }
        }

        /// <summary>Wings flap every frame; the butterfly hops a step toward where it's going, bobbing a pixel.</summary>
        void UpdateButterflies()
        {
            float step = ButterflySpeed / ButterflyFps;
            const float pixel = 1f / PixelArt.PixelsPerUnit;
            foreach (Butterfly butterfly in butterflies)
            {
                if (butterfly.Sprite == null) continue;
                butterfly.Frame = 1 - butterfly.Frame;
                butterfly.Sprite.sprite = wingFrames[butterfly.Frame];

                Vector2 toTarget = butterfly.Target - butterfly.Position;
                if (toTarget.magnitude < step * 2f)
                {
                    butterfly.Target = new Vector2(Random.Range(field.xMin, field.xMax), Random.Range(field.yMin, field.yMax));
                    toTarget = butterfly.Target - butterfly.Position;
                }
                // A wobbly path: the heading wanders a little each hop.
                Vector2 heading = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f)) * toTarget.normalized;
                butterfly.Position += heading * step;
                butterfly.Sprite.flipX = heading.x < 0f;

                // Wings open: up a pixel. Snapped to the pixel grid so it moves in whole pixels.
                Vector2 shown = butterfly.Position + new Vector2(0f, butterfly.Frame == 0 ? pixel * 2f : 0f);
                shown = new Vector2(Mathf.Round(shown.x / pixel) * pixel, Mathf.Round(shown.y / pixel) * pixel);
                butterfly.Sprite.transform.position = shown;
            }
        }

        static int Mod(int value, int length) => ((value % length) + length) % length;
    }
}
