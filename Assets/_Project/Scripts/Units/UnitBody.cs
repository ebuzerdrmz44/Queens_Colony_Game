using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Creates the visible sprite of a unit as a child object named "Body".
    /// The body is positioned so the unit's own position is at its feet, which keeps
    /// every creature standing on the ground no matter where the sprite's pivot is.
    /// (<see cref="UnitAnimator"/> re-centres it for top-down views.)
    /// </summary>
    public static class UnitBody
    {
        public static SpriteRenderer Create(GameArt art, Transform unit, Sprite sprite, float width, int sortingOrder, Color tint)
        {
            SpriteRenderer body = art.CreateSprite("Body", unit, sprite, sortingOrder);
            body.color = tint;
            GameArt.ScaleToWidth(body, width);
            body.transform.localPosition = new Vector3(0f, GameArt.PivotHeight(body), 0f);
            return body;
        }

        /// <summary>
        /// The prefab's own "Body" child if it has one (kept as it is: its scale, colour and sorting set in the prefab),
        /// otherwise a new body made like <see cref="Create"/>.
        /// </summary>
        public static SpriteRenderer FindOrCreate(GameArt art, Transform unit, Sprite sprite, float width, int sortingOrder, Color tint)
        {
            Transform existing = unit.Find("Body");
            if (existing != null && existing.TryGetComponent(out SpriteRenderer body))
            {
                if (body.sprite == null)
                {
                    body.sprite = sprite;
                    GameArt.ScaleToWidth(body, width);
                }
                body.transform.localPosition = new Vector3(0f, GameArt.PivotHeight(body), 0f);
                return body;
            }
            return Create(art, unit, sprite, width, sortingOrder, tint);
        }
    }
}
