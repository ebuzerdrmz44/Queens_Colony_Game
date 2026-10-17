using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>Autumn leaves blowing across the surface view, above everything else. Purely decorative.</summary>
    public class FallingLeaves : MonoBehaviour
    {
        const int LeafCount = 30;
        static readonly Vector2 Wind = new Vector2(1.1f, -0.6f);

        class Leaf
        {
            public Transform Transform;
            public float Speed;
            public float SwayAmount;
            public float SwaySpeed;
            public float SwayPhase;
            public float SpinSpeed;
        }

        readonly List<Leaf> leaves = new List<Leaf>();
        ColonyGame game;
        Camera viewCamera;
        bool visible = true;

        public void Init(ColonyGame colonyGame, Camera cameraToFollow)
        {
            game = colonyGame;
            viewCamera = cameraToFollow;

            for (int i = 0; i < LeafCount; i++)
            {
                SpriteRenderer leafRenderer = game.Art.CreateSprite("Leaf", transform, game.Art.FallingLeaf, SortingOrder.FallingLeaves);
                leafRenderer.color = AutumnPalette.LeafColors[Random.Range(0, AutumnPalette.LeafColors.Length)];
                leafRenderer.transform.localScale = Vector3.one * Random.Range(1.2f, 2f);

                var leaf = new Leaf
                {
                    Transform = leafRenderer.transform,
                    Speed = Random.Range(0.7f, 1.5f),
                    SwayAmount = Random.Range(0.4f, 1.2f),
                    SwaySpeed = Random.Range(1f, 2.5f),
                    SwayPhase = Random.Range(0f, Mathf.PI * 2f),
                    SpinSpeed = Random.Range(-120f, 120f),
                };
                leaves.Add(leaf);

                // Start spread over the whole view instead of all at the edge.
                Respawn(leaf, anywhereInView: true);
            }
        }

        void Update()
        {
            // Leaves only blow over the surface, so hide them while the camera is in the nest.
            bool onSurface = game.CameraRig.View == CameraView.Surface;
            if (onSurface != visible)
            {
                visible = onSurface;
                foreach (Leaf leaf in leaves)
                {
                    leaf.Transform.gameObject.SetActive(visible);
                    if (visible) Respawn(leaf, anywhereInView: true);
                }
            }
            if (!visible) return;

            float deltaTime = Time.deltaTime;
            Rect view = ViewRect();
            var sway = new Vector2(-Wind.y, Wind.x).normalized; // across the wind

            foreach (Leaf leaf in leaves)
            {
                Vector2 position = leaf.Transform.position;
                float swayOffset = Mathf.Sin(Time.time * leaf.SwaySpeed + leaf.SwayPhase) * leaf.SwayAmount;
                position += (Wind * leaf.Speed + sway * swayOffset) * deltaTime;
                leaf.Transform.position = position;
                leaf.Transform.Rotate(0f, 0f, leaf.SpinSpeed * deltaTime);

                Rect outer = new Rect(view.position - Vector2.one * 2f, view.size + Vector2.one * 4f);
                if (!outer.Contains(position)) Respawn(leaf, anywhereInView: false);
            }
        }

        /// <summary>Puts a leaf back in view, or just outside the edge the wind blows from.</summary>
        void Respawn(Leaf leaf, bool anywhereInView)
        {
            Rect view = ViewRect();
            Vector2 position;
            if (anywhereInView)
            {
                position = new Vector2(Random.Range(view.xMin, view.xMax), Random.Range(view.yMin, view.yMax));
            }
            else if (Random.value < view.height / (view.width + view.height))
            {
                position = new Vector2(view.xMin - 1f, Random.Range(view.yMin, view.yMax)); // upwind side
            }
            else
            {
                position = new Vector2(Random.Range(view.xMin, view.xMax), view.yMax + 1f); // top edge
            }
            leaf.Transform.position = position;
        }

        Rect ViewRect()
        {
            Vector2 center = viewCamera.transform.position;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            return new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
        }
    }
}
