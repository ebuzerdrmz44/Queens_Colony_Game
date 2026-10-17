using UnityEngine;

namespace AntColony
{
    public enum FoodKind
    {
        /// <summary>Eaten as it is.</summary>
        Food,
        /// <summary>Has to be turned into food in a fungus garden.</summary>
        Leaves,
    }

    /// <summary>
    /// A pile of food on the surface (a leaf, acorn, berry or a defeated enemy). Each worker trip
    /// takes one unit and carries <see cref="Load"/> home. Workers claim a unit before walking over,
    /// so they don't all chase the same last bite.
    /// </summary>
    public class FoodSource : MonoBehaviour
    {
        const float SmallestSize = 0.5f;
        /// <summary>Workers pick up from a random spot this close to the middle of the pile, so they don't stack on one pixel.</summary>
        const float PickupSpread = 0.35f;

        [Tooltip("Optional other looks for this pile (e.g. several leaves). Each pile picks one at random, " +
                 "drawn as wide as the prefab's own sprite.")]
        [SerializeField] Sprite[] looks;

        FoodSpawner owner;
        SpriteRenderer visual;
        Vector3 fullScale;
        int startingUnits;
        int unitsLeft;
        int claimedUnits;

        public FoodKind Kind { get; private set; }
        public int Load { get; private set; }
        public Vector2 Position => transform.position;
        public bool HasUnclaimedUnits => unitsLeft > claimedUnits;
        /// <summary>Picked with the Gather tool: gatherers only take from marked piles.</summary>
        public bool IsMarked { get; set; }
        /// <summary>Workers already on their way here.</summary>
        public int ClaimedUnits => claimedUnits;

        public Vector2 RandomPickupPoint() => (Vector2)transform.position + Random.insideUnitCircle * PickupSpread;

        /// <summary>Swaps the pile's sprite for one of its <see cref="looks"/> at random, keeping its width. Call before <see cref="Init"/>.</summary>
        public void PickRandomLook(SpriteRenderer foodVisual)
        {
            if (looks == null || looks.Length == 0 || foodVisual.sprite == null) return;
            Sprite look = looks[Random.Range(0, looks.Length)];
            if (look == null || look == foodVisual.sprite) return;

            float width = foodVisual.sprite.bounds.size.x * foodVisual.transform.localScale.x;
            foodVisual.sprite = look;
            float scale = width / Mathf.Max(0.001f, look.bounds.size.x);
            foodVisual.transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void Init(FoodSpawner spawner, SpriteRenderer foodVisual, int units, FoodKind kind, int load)
        {
            Kind = kind;
            Load = load;
            owner = spawner;
            visual = foodVisual;
            fullScale = foodVisual.transform.localScale;
            startingUnits = Mathf.Max(1, units);
            unitsLeft = startingUnits;
            UpdateVisual();
        }

        public int UnitsLeft => unitsLeft;
        public int StartingUnits => startingUnits;

        /// <summary>Puts a loaded pile back to how much was left.</summary>
        public void RestoreUnits(int left, int starting)
        {
            startingUnits = Mathf.Max(1, starting);
            unitsLeft = Mathf.Clamp(left, 1, startingUnits);
            UpdateVisual();
        }

        public bool TryClaim()
        {
            if (!HasUnclaimedUnits) return false;
            claimedUnits++;
            return true;
        }

        /// <summary>Gives back a claim that will not be collected (for example, the worker died).</summary>
        public void ReleaseClaim()
        {
            claimedUnits = Mathf.Max(0, claimedUnits - 1);
        }

        /// <summary>Removes one previously claimed unit. The pile disappears when it is empty.</summary>
        public void TakeClaimedUnit()
        {
            claimedUnits = Mathf.Max(0, claimedUnits - 1);
            unitsLeft--;

            if (unitsLeft <= 0)
            {
                owner.Remove(this);
                Destroy(gameObject);
                return;
            }

            UpdateVisual();
        }

        /// <summary>Shrinks the pile as it gets eaten.</summary>
        void UpdateVisual()
        {
            float size = Mathf.Lerp(SmallestSize, 1f, (float)unitsLeft / startingUnits);
            visual.transform.localScale = fullScale * size;
        }
    }
}
