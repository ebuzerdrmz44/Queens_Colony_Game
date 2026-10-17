using UnityEngine;

namespace AntColony
{
    /// <summary>A small bar floating above a unit. Hidden while the unit is at full health, unless told otherwise.</summary>
    public class HealthBar : MonoBehaviour
    {
        const float BarHeight = 0.1f;
        const float BorderSize = 0.04f;

        SpriteRenderer fill;
        float width;
        bool alwaysVisible;

        public static HealthBar Create(GameArt art, Transform owner, float heightAboveFeet, float width, Color fillColor, bool alwaysVisible = false)
        {
            var barObject = new GameObject("Health Bar");
            barObject.transform.SetParent(owner, false);
            barObject.transform.localPosition = new Vector3(0f, heightAboveFeet, 0f);

            SpriteRenderer background = art.CreateSprite("Background", barObject.transform, art.Pixel, SortingOrder.HealthBars);
            background.color = new Color(0f, 0f, 0f, 0.6f);
            GameArt.Stretch(background, new Vector2(width + BorderSize, BarHeight + BorderSize));

            HealthBar bar = barObject.AddComponent<HealthBar>();
            bar.width = width;
            bar.alwaysVisible = alwaysVisible;
            bar.fill = art.CreateSprite("Fill", barObject.transform, art.Pixel, SortingOrder.HealthBars + 1);
            bar.fill.color = fillColor;
            bar.SetFraction(1f);
            return bar;
        }

        public void SetFraction(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);

            // The Pixel sprite is 1x1 unit with a centred pivot, so scale = size. Keep the bar left-aligned.
            fill.transform.localScale = new Vector3(width * fraction, BarHeight, 1f);
            fill.transform.localPosition = new Vector3((fraction - 1f) * width * 0.5f, 0f, 0f);

            gameObject.SetActive(alwaysVisible || fraction < 1f);
        }
    }
}
