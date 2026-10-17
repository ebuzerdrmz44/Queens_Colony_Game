using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The boss's mega spit: a big blob of acid lobbed at a spot (not at a unit, so ants can step out of the way).
    /// It arcs up and down on its way (in whole pixels) and calls <c>landed</c> where it comes down.
    /// </summary>
    public class BossGlob : MonoBehaviour
    {
        const float Speed = 6f;
        const float Width = 0.75f;
        /// <summary>Highest point of the arc, as a share of the distance.</summary>
        const float ArcHeight = 0.25f;

        Vector2 start;
        Vector2 target;
        float travelled;
        float distance;
        Action<Vector2> onLanded;
        SpriteRenderer blob;

        public static void Throw(ColonyGame game, Vector2 from, Vector2 to, Action<Vector2> landed)
        {
            SpriteRenderer sprite = game.Art.CreateSprite("Boss Glob", game.SurfaceRoot, game.Art.AcidShot, SortingOrder.Projectiles);
            GameArt.ScaleToWidth(sprite, Width);
            sprite.color = new Color(0.75f, 1f, 0.35f);
            sprite.transform.position = from;

            BossGlob glob = sprite.gameObject.AddComponent<BossGlob>();
            glob.blob = sprite;
            glob.start = from;
            glob.target = to;
            glob.distance = Mathf.Max(0.01f, Vector2.Distance(from, to));
            glob.onLanded = landed;
        }

        void Update()
        {
            travelled = Mathf.Min(distance, travelled + Speed * Time.deltaTime);
            float t = travelled / distance;
            Vector2 ground = Vector2.Lerp(start, target, t);

            // Seen from above, the arc shows as a lift and a swell; both move in whole pixels.
            const float pixel = 1f / PixelArt.PixelsPerUnit;
            float lift = Mathf.Round(Mathf.Sin(t * Mathf.PI) * distance * ArcHeight / pixel) * pixel;
            transform.position = ground + new Vector2(0f, lift);
            float swell = 1f + Mathf.Round(Mathf.Sin(t * Mathf.PI) * 4f) / 8f;
            transform.localScale = Vector3.one * (Width / Mathf.Max(0.01f, blob.sprite.bounds.size.x)) * swell;

            if (t < 1f) return;
            onLanded?.Invoke(target);
            Destroy(gameObject);
        }
    }
}
