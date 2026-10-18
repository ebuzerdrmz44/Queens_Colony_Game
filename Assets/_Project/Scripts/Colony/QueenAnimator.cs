using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The queen's frame-by-frame art: a slow breathing idle, a walk while she moves, the egg-laying frames while she
    /// lays (in step with <see cref="Queen"/>'s birthing pose), and every few seconds a flap of her wings over whatever
    /// she's doing. Each frame comes in three wing positions (resting, half up, up).
    /// Runs after <see cref="UnitAnimator"/>, which still turns her, tilts her while laying and spots her moving.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class QueenAnimator : MonoBehaviour
    {
        /// <summary>One body pose with its three wing positions.</summary>
        [Serializable]
        public class QueenFrame
        {
            public Sprite wingsRest;
            public Sprite wingsHalf;
            public Sprite wingsUp;

            public Sprite Get(int wing)
            {
                Sprite sprite = wing == 2 ? wingsUp : wing == 1 ? wingsHalf : wingsRest;
                return sprite != null ? sprite : wingsRest;
            }
        }

        [Tooltip("Breathing, looped while she stands.")]
        public QueenFrame[] idle;
        [Tooltip("Walk cycle, stepped by how far she moves.")]
        public QueenFrame[] walk;
        [Tooltip("Laying an egg: settle, squeeze, push (the egg shows), relax.")]
        public QueenFrame[] lay;

        [Header("Timing")]
        [Min(0.1f)] public float idleFps = 2f;
        [Tooltip("Walk frames per world unit travelled.")]
        [Min(0.5f)] public float walkFramesPerUnit = 7f;
        [Tooltip("Seconds between wing flaps (picked at random between these).")]
        [Min(0.5f)] public float flapEveryMin = 3f;
        [Min(0.5f)] public float flapEveryMax = 4f;
        [Range(2f, 30f)] public float flapFps = 12f;

        /// <summary>Two wing beats: half, up, half, rest, twice.</summary>
        static readonly int[] FlapSequence = { 1, 2, 1, 0, 1, 2, 1, 0 };

        SpriteRenderer body;
        Queen queen;
        Vector3 lastPosition;
        float walkProgress;
        float nextFlap;
        float flapTime = -1f;

        public void Init(SpriteRenderer queenBody, Queen owner)
        {
            body = queenBody;
            queen = owner;
            lastPosition = transform.position;
            nextFlap = UnityEngine.Random.Range(flapEveryMin, Mathf.Max(flapEveryMin, flapEveryMax));
        }

        void LateUpdate()
        {
            if (body == null) return;
            float deltaTime = Time.deltaTime;

            int wing = UpdateWings(deltaTime);

            Vector3 position = transform.position;
            float moved = Vector3.Distance(position, lastPosition);
            lastPosition = position;

            QueenFrame frame = null;
            int layFrame = queen != null ? LayFrameFor(queen.BirthFrame) : -1;
            if (layFrame >= 0 && Has(lay))
            {
                frame = lay[Mathf.Min(layFrame, lay.Length - 1)];
            }
            else if (moved > 0.0005f && moved < 2f && Has(walk))
            {
                walkProgress += moved * walkFramesPerUnit;
                frame = walk[Mathf.FloorToInt(walkProgress) % walk.Length];
            }
            else if (Has(idle))
            {
                frame = idle[Mathf.FloorToInt(Time.time * idleFps) % idle.Length];
            }

            Sprite sprite = frame?.Get(wing);
            if (sprite != null) body.sprite = sprite;
        }

        /// <summary>The wing position this frame: resting, or part of a flap that starts every few seconds.</summary>
        int UpdateWings(float deltaTime)
        {
            if (flapTime < 0f)
            {
                nextFlap -= deltaTime;
                if (nextFlap > 0f) return 0;
                flapTime = 0f;
            }

            flapTime += deltaTime;
            int step = Mathf.FloorToInt(flapTime * flapFps);
            if (step < FlapSequence.Length) return FlapSequence[step];

            flapTime = -1f;
            nextFlap = UnityEngine.Random.Range(flapEveryMin, Mathf.Max(flapEveryMin, flapEveryMax));
            return 0;
        }

        /// <summary>
        /// Which laying frame goes with the queen's birthing pose frame: settle while she squats, pulse while she
        /// strains, push, the egg as it pops out, then relax. -1 before she has turned round (her normal frames).
        /// </summary>
        static int LayFrameFor(int birthFrame)
        {
            if (birthFrame < 2) return -1;
            if (birthFrame <= 4) return 0;
            if (birthFrame <= 8) return birthFrame % 2 == 1 ? 1 : 0;
            if (birthFrame == 9) return 1;
            if (birthFrame == 10) return 2;
            return 3;
        }

        static bool Has(QueenFrame[] frames) => frames != null && frames.Length > 0;
    }
}
