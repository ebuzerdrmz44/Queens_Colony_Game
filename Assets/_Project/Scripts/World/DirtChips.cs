using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// A small burst of pixel chips that fly off a tile when a builder strikes it, fall and fade.
    /// The built-in look for <see cref="EffectKind.DigHit"/>; an effect prefab in that slot replaces it.
    /// </summary>
    public class DirtChips : MonoBehaviour
    {
        const float Lifetime = 0.5f;
        const float Gravity = 9f;
        const float ChipSize = 0.09f;

        Transform[] chips;
        SpriteRenderer[] renderers;
        Vector2[] velocities;
        float age;

        /// <param name="away">Rough direction the chips fly in (from the struck face toward the digger).</param>
        public static void Burst(ColonyGame game, Vector2 position, Vector2 away, Color[] colors, int count = 6)
        {
            var root = new GameObject("Dirt Chips");
            root.transform.SetParent(game.AreaRoot(position), false);
            root.transform.position = position;

            DirtChips burst = root.AddComponent<DirtChips>();
            burst.chips = new Transform[count];
            burst.renderers = new SpriteRenderer[count];
            burst.velocities = new Vector2[count];
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector2.up;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer chip = game.Art.CreateSprite("Chip", root.transform, game.Art.Pixel, SortingOrder.Projectiles);
                chip.color = colors[Random.Range(0, colors.Length)];
                float size = ChipSize * Random.Range(0.7f, 1.3f);
                chip.transform.localScale = new Vector3(size, size, 1f);
                chip.transform.localPosition = Random.insideUnitCircle * 0.08f;

                // Mostly back toward the digger and upward, spread out a little.
                Vector2 direction = Quaternion.Euler(0f, 0f, Random.Range(-55f, 55f)) * (away + Vector2.up * 0.6f).normalized;
                burst.chips[i] = chip.transform;
                burst.renderers[i] = chip;
                burst.velocities[i] = direction * Random.Range(1.4f, 2.6f);
            }
        }

        void Update()
        {
            // Real time while the game is frozen (e.g. during the intro), so the chips still fly.
            float deltaTime = Time.timeScale > 0f ? Time.deltaTime : Time.unscaledDeltaTime;
            age += deltaTime;
            if (age >= Lifetime)
            {
                Destroy(gameObject);
                return;
            }

            float fade = 1f - age / Lifetime;
            for (int i = 0; i < chips.Length; i++)
            {
                velocities[i].y -= Gravity * deltaTime;
                chips[i].localPosition += (Vector3)(velocities[i] * deltaTime);
                Color color = renderers[i].color;
                color.a = fade;
                renderers[i].color = color;
            }
        }
    }
}
