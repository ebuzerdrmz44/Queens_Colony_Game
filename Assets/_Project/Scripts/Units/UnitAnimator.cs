using UnityEngine;

namespace AntColony
{
    public enum BodyView
    {
        /// <summary>Seen from the side (the nest): the body stands on the unit's position and flips left/right.</summary>
        Side,
        /// <summary>Seen from above (the surface): the body is centred on the unit and turns to face where it goes.</summary>
        TopDown,
    }

    /// <summary>
    /// Owns how a unit's body sprite looks: which way it faces, the walking frames (they flip by while the
    /// unit moves; it watches the position, so movement code doesn't need to report anything), a short
    /// wind-up and lunge for <see cref="PlayAttack"/>, and an idle animation while it stands still:
    /// stepped pixel-art breathing, and now and then a look the other way or a shuffle of the legs.
    /// Add this component to a unit's prefab to tune the idle in the Inspector; otherwise it is added in code
    /// with the defaults below.
    /// </summary>
    public class UnitAnimator : MonoBehaviour
    {
        [Header("Idle (while standing still)")]
        [Tooltip("Idle frames per second. Low numbers give chunky pixel-art timing.")]
        [Range(1f, 12f)] public float idleFps = 4f;
        [Tooltip("Breathing: how many sprite pixels taller the body is on each idle frame, looped (e.g. 0, 0, 1, 1).")]
        public int[] breathPixels = { 0, 0, 1, 1 };
        [Tooltip("Average seconds between fidgets: looking the other way, or shuffling the legs.")]
        [Min(0.5f)] public float fidgetEvery = 3f;
        [Tooltip("Idle frames a look the other way lasts.")]
        [Min(1)] public int lookFrames = 4;
        [Tooltip("Seconds standing still before the idle starts (so short stops don't twitch).")]
        [Min(0f)] public float idleDelay = 0.25f;

        /// <summary>Slower than this (world units per second) counts as standing still, e.g. while the crowd nudges it.</summary>
        const float StillSpeed = 0.15f;

        /// <summary>Walking frames shown per world unit travelled, so faster units step faster.</summary>
        const float FramesPerUnit = 12f;

        const float AttackDuration = 0.32f;
        /// <summary>Fraction of the attack spent winding up before the strike.</summary>
        const float WindUpPart = 0.35f;
        const float WindUpPullBack = 0.08f; // in body widths
        const float LungeDistance = 0.18f;  // in body widths
        const float WindUpTilt = 12f;       // degrees, head up (side view only)

        SpriteRenderer body;
        CreatureFrames frames;
        bool sideFacesLeft;
        float bodyWidth;
        float frameScale;
        Vector3 restPosition;
        Quaternion restRotation = Quaternion.identity;
        Vector3 lastPosition;
        float walkProgress;
        float attackTime = -1f;
        Vector2 facing = Vector2.right;

        // Idle state.
        float stillTime;
        float idleClock;
        int idleFrame = -1;
        int breathStart;
        float nextFidget;
        int lookFramesLeft;
        int shuffleFramesLeft;
        Vector3 baseScale = Vector3.one;
        bool idleApplied;

        // A pose held by other code (e.g. the queen laying an egg); walking and idle wait meanwhile.
        bool posing;
        Vector3 poseScale = Vector3.one;

        public BodyView View { get; private set; }
        /// <summary>Width of the body in world units.</summary>
        public float BodyWidth => bodyWidth;
        /// <summary>The direction the body last turned to.</summary>
        public Vector2 Facing => facing;
        public bool IsAttacking => attackTime >= 0f;

        public static UnitAnimator Attach(Transform unit, SpriteRenderer body, BodyView view, CreatureFrames frames, bool sideFacesLeft = false)
        {
            if (!unit.TryGetComponent(out UnitAnimator animator)) animator = unit.gameObject.AddComponent<UnitAnimator>();
            animator.body = body;
            animator.sideFacesLeft = sideFacesLeft;
            animator.lastPosition = unit.position;
            animator.bodyWidth = body.bounds.size.x;
            // Bigger bodies take longer strides.
            animator.frameScale = FramesPerUnit / Mathf.Max(0.1f, animator.bodyWidth * 2f);
            animator.baseScale = body.transform.localScale;
            animator.breathStart = Random.Range(0, 8); // ants side by side don't breathe in step
            animator.nextFidget = Random.Range(0.5f, 2f) * animator.fidgetEvery;
            animator.SetView(view, frames);
            return animator;
        }

        /// <summary>Switches between side and top-down art, e.g. when an ant goes through the nest entrance.</summary>
        public void SetView(BodyView view, CreatureFrames newFrames)
        {
            StopIdle();
            View = view;
            frames = newFrames;
            attackTime = -1f;
            walkProgress = 0f;
            body.sprite = frames.Walk[0];
            body.flipX = false;
            KeepBodyWidth();
            restPosition = view == BodyView.Side ? new Vector3(0f, GameArt.PivotHeight(body), 0f) : Vector3.zero;
            body.transform.localPosition = restPosition;
            ApplyFacing();
        }

        /// <summary>
        /// The two views' art can be drawn at very different sizes (16 px side-view ants, 500 px painted top-down ones):
        /// scale the body so it stays as wide in the world as when it was attached.
        /// </summary>
        void KeepBodyWidth()
        {
            if (bodyWidth <= 0f || body.sprite == null) return;
            Transform parent = body.transform.parent;
            float parentScale = parent != null ? Mathf.Abs(parent.lossyScale.x) : 1f;
            float spriteWidth = body.sprite.bounds.size.x * parentScale;
            if (spriteWidth < 0.0001f) return;
            float scale = bodyWidth / spriteWidth;
            body.transform.localScale = new Vector3(scale, scale, body.transform.localScale.z);
        }

        /// <summary>Turns the body toward a direction (ignored if the direction is zero).</summary>
        public void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.000001f) return;
            facing = direction.normalized;
            ApplyFacing();
        }

        /// <summary>Starts a bite / ram toward the way the body is facing. Restarts if one is already playing.</summary>
        public void PlayAttack()
        {
            attackTime = 0f;
        }

        void ApplyFacing()
        {
            if (View == BodyView.TopDown)
            {
                restRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg);
            }
            else if (Mathf.Abs(facing.y) > Mathf.Abs(facing.x))
            {
                // Climbing the shaft: point the head (the right side of the art) up or down.
                body.flipX = sideFacesLeft;
                restRotation = Quaternion.Euler(0f, 0f, facing.y > 0f ? 90f : -90f);
            }
            else
            {
                body.flipX = (facing.x < 0f) != sideFacesLeft;
                restRotation = Quaternion.identity;
            }

            if (!IsAttacking && !idleApplied) body.transform.localRotation = restRotation;
        }

        void LateUpdate()
        {
            Vector3 position = transform.position;
            float distance = Vector3.Distance(position, lastPosition);
            lastPosition = position;
            if (posing) return;

            bool still = distance < StillSpeed * Mathf.Max(Time.deltaTime, 0.0001f) && !IsAttacking;
            stillTime = still ? stillTime + Time.deltaTime : 0f;

            if (stillTime > idleDelay)
            {
                UpdateIdle();
                return;
            }

            StopIdle();
            if (IsAttacking) UpdateAttack();
            else UpdateWalk(distance);
        }

        // ---------- Idle ----------

        /// <summary>
        /// Stepped, pixel-art idle: the body changes only on idle frames (a few per second). Breathing makes it a
        /// whole sprite pixel taller and back; now and then it looks the other way for a moment or shuffles its legs.
        /// </summary>
        void UpdateIdle()
        {
            if (!idleApplied)
            {
                baseScale = body.transform.localScale;
                idleApplied = true;
                idleClock = 0f;
                idleFrame = -1;
                walkProgress = 0f;
                body.sprite = frames.Walk[0];
            }

            idleClock += Time.deltaTime;
            int frame = Mathf.FloorToInt(idleClock * idleFps);
            if (frame == idleFrame) return;
            idleFrame = frame;

            // Fidgets start on a frame and last whole frames.
            nextFidget -= 1f / idleFps;
            if (nextFidget <= 0f && lookFramesLeft == 0 && shuffleFramesLeft == 0)
            {
                nextFidget = Random.Range(0.5f, 1.5f) * fidgetEvery;
                if (Random.value < 0.5f) lookFramesLeft = lookFrames;
                else shuffleFramesLeft = Mathf.Min(2, frames.Walk.Length - 1);
            }

            // Look the other way: a flip in side view, a quarter turn seen from above.
            if (lookFramesLeft > 0)
            {
                lookFramesLeft--;
                if (View == BodyView.Side) body.flipX = ((facing.x < 0f) != sideFacesLeft) ^ (lookFramesLeft > 0);
                else body.transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, lookFramesLeft > 0 ? 90f : 0f);
            }

            // Shuffle: the walking frames, one per idle frame.
            if (shuffleFramesLeft > 0)
            {
                body.sprite = frames.Walk[shuffleFramesLeft];
                shuffleFramesLeft--;
            }
            else
            {
                body.sprite = frames.Walk[0];
            }

            // Breathing in whole sprite pixels; the feet stay put in side view.
            int pixels = breathPixels != null && breathPixels.Length > 0 ? breathPixels[(frame + breathStart) % breathPixels.Length] : 0;
            float spriteHeight = Mathf.Max(1f, body.sprite.rect.height);
            float grow = pixels / spriteHeight;
            body.transform.localScale = new Vector3(baseScale.x, baseScale.y * (1f + grow), baseScale.z);
            float lift = View == BodyView.Side ? restPosition.y * grow : 0f;
            body.transform.localPosition = restPosition + new Vector3(0f, lift, 0f);
        }

        /// <summary>Puts the body back to its normal size, angle and place when the unit moves again.</summary>
        void StopIdle()
        {
            if (!idleApplied) return;
            idleApplied = false;
            lookFramesLeft = 0;
            shuffleFramesLeft = 0;
            body.transform.localScale = baseScale;
            body.transform.localPosition = restPosition;
            ApplyFacing();
        }

        void UpdateWalk(float distance)
        {
            // A big jump means the unit went through the entrance, not that it walked.
            if (distance < 0.0001f || distance > 2f)
            {
                walkProgress = 0f;
                body.sprite = frames.Walk[0];
                if (frames.Walk.Length == 1) body.transform.localPosition = restPosition;
                return;
            }

            walkProgress += distance * frameScale;
            int step = Mathf.FloorToInt(walkProgress);
            body.sprite = frames.Walk[step % frames.Walk.Length];

            // A body with a single picture (e.g. the queen) waddles instead: one pixel up on every other step.
            if (frames.Walk.Length == 1)
            {
                float pixel = body.sprite.bounds.size.y / Mathf.Max(1f, body.sprite.rect.height) * body.transform.localScale.y;
                body.transform.localPosition = restPosition + new Vector3(0f, (step & 1) * pixel, 0f);
            }
        }

        // ---------- Poses ----------

        /// <summary>Holds the body still for <see cref="Pose"/> until <see cref="EndPose"/>.</summary>
        public void BeginPose()
        {
            StopIdle();
            attackTime = -1f;
            posing = true;
            poseScale = body.transform.localScale;
            body.sprite = frames.Walk[0];
        }

        /// <summary>
        /// Squashes or stretches the body by whole sprite pixels: <paramref name="tallerPixels"/> up (feet stay on
        /// the ground), <paramref name="widerPixels"/> across, and <paramref name="shiftPixels"/> sideways.
        /// </summary>
        public void Pose(int tallerPixels, int widerPixels, int shiftPixels, float tiltDegrees = 0f)
        {
            if (!posing) BeginPose();
            Rect pixels = body.sprite.rect;
            float taller = tallerPixels / Mathf.Max(1f, pixels.height);
            float wider = widerPixels / Mathf.Max(1f, pixels.width);
            body.transform.localScale = new Vector3(poseScale.x * (1f + wider), poseScale.y * (1f + taller), poseScale.z);

            float pixel = body.sprite.bounds.size.x / Mathf.Max(1f, pixels.width) * poseScale.x;
            float lift = View == BodyView.Side ? restPosition.y * taller : 0f;
            body.transform.localPosition = restPosition + new Vector3(shiftPixels * pixel, lift, 0f);

            // Tilt head up (and the rear down), whichever way the body faces.
            float forward = body.flipX != sideFacesLeft ? -1f : 1f;
            body.transform.localRotation = restRotation * Quaternion.Euler(0f, 0f, tiltDegrees * forward);
        }

        public void EndPose()
        {
            if (!posing) return;
            posing = false;
            body.transform.localScale = poseScale;
            body.transform.localPosition = restPosition;
            ApplyFacing();
        }

        void UpdateAttack()
        {
            attackTime += Time.deltaTime;
            float t = attackTime / AttackDuration;
            if (t >= 1f)
            {
                attackTime = -1f;
                body.transform.localPosition = restPosition;
                body.transform.localRotation = restRotation;
                body.sprite = frames.Walk[0];
                return;
            }

            float offset;
            float tilt;
            if (t < WindUpPart)
            {
                // Rear back (head up in side view).
                float windUp = Mathf.Sin(t / WindUpPart * Mathf.PI * 0.5f);
                offset = -WindUpPullBack * windUp;
                tilt = WindUpTilt * windUp;
                body.sprite = frames.Attack[0];
            }
            else
            {
                // Snap forward past the rest position, then settle back.
                float strike = (t - WindUpPart) / (1f - WindUpPart);
                offset = Mathf.Lerp(-WindUpPullBack, 0f, strike) + LungeDistance * Mathf.Sin(strike * Mathf.PI);
                tilt = WindUpTilt * (1f - strike) * -0.3f;
                body.sprite = frames.Attack[frames.Attack.Length - 1];
            }

            if (View == BodyView.TopDown)
            {
                body.transform.localPosition = restPosition + (Vector3)(facing * (offset * bodyWidth));
                body.transform.localRotation = restRotation;
            }
            else
            {
                float forward = body.flipX != sideFacesLeft ? -1f : 1f;
                body.transform.localPosition = restPosition + new Vector3(forward * offset * bodyWidth, 0f, 0f);
                body.transform.localRotation = Quaternion.Euler(0f, 0f, tilt * forward);
            }
        }
    }
}
