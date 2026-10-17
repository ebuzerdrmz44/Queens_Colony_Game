using UnityEngine;

namespace AntColony
{
    /// <summary>A ring that shrinks and fades where the player sent their soldiers.</summary>
    public class OrderMarker : MonoBehaviour
    {
        const float Lifetime = 0.45f;
        const float StartWidth = 1.4f;
        const float EndWidth = 0.4f;

        SpriteRenderer ring;
        Color color;
        float age;

        public static void Show(ColonyGame game, Vector2 position, Color color)
        {
            SpriteRenderer ring = game.Art.CreateSprite("Order Marker", game.SurfaceRoot, game.Art.SelectionRing, SortingOrder.OrderMarkers);
            ring.transform.position = position;
            ring.color = color;

            OrderMarker marker = ring.gameObject.AddComponent<OrderMarker>();
            marker.ring = ring;
            marker.color = color;
            marker.Update();
        }

        void Update()
        {
            age += Time.deltaTime;
            float t = age / Lifetime;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            GameArt.ScaleToWidth(ring, Mathf.Lerp(StartWidth, EndWidth, t));
            ring.color = new Color(color.r, color.g, color.b, 1f - t * t);
        }
    }
}
