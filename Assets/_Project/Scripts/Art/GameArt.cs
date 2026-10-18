using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Sprite slots shown in the Inspector. Drag sprites from your pixel-art pack here;
    /// any slot left empty falls back to the generated autumn art in <see cref="PixelSprites"/>.
    /// Creature slots take walking frames in order; a single sprite works too (it just won't animate).
    /// Attack slots take a wind-up and a strike frame.
    /// </summary>
    [Serializable]
    public class ArtOverrides
    {
        [Header("Nest (side view)")]
        [Tooltip("Used for workers, soldiers and the queen in the nest (they are tinted differently).")]
        public Sprite[] ant;
        [Tooltip("Optional: wind-up and strike frames.")]
        public Sprite[] antAttack;
        [Tooltip("Tick this if the side-view ant looks to the left instead of to the right.")]
        public bool antFacesLeft;

        [Header("Surface (top-down, drawn facing right)")]
        public Sprite[] antTopDown;
        public Sprite[] antTopDownAttack;
        public Sprite[] beetle;
        public Sprite[] beetleAttack;
        public Sprite[] spider;
        public Sprite[] spiderAttack;

        [Header("Food")]
        public Sprite leaf;
        public Sprite acorn;
        public Sprite berry;

        [Header("Scenery")]
        [Tooltip("Top-down tree canopies, picked at random.")]
        public Sprite[] trees;
    }

    /// <summary>
    /// Sprite slots on a unit's prefab (walking frames in order, then wind-up and strike frames).
    /// Empty slots fall back to the art the game would use otherwise.
    /// </summary>
    [Serializable]
    public class CreatureSprites
    {
        public Sprite[] walk;
        public Sprite[] attack;
    }

    /// <summary>Walking and attack frames of one creature, seen from one point of view.</summary>
    public class CreatureFrames
    {
        public Sprite[] Walk { get; }
        public Sprite[] Attack { get; }

        public CreatureFrames(Sprite[] walk, Sprite[] attack)
        {
            Walk = walk;
            Attack = attack;
        }
    }

    /// <summary>All sprites the game draws with, plus helpers for creating and sizing sprite objects.</summary>
    public class GameArt
    {
        const int TreeVariants = 4;

        /// <summary>Ants in the nest, seen from the side.</summary>
        public CreatureFrames AntSide { get; }
        public bool AntSideFacesLeft { get; }
        /// <summary>Ants on the surface, seen from above.</summary>
        public CreatureFrames AntTop { get; }
        public CreatureFrames Beetle { get; }
        public CreatureFrames Spider { get; }
        /// <summary>The standing side-view ant (used for the queen).</summary>
        public Sprite Ant => AntSide.Walk[0];

        public Sprite Leaf { get; }
        public Sprite Acorn { get; }
        public Sprite Berry { get; }
        public Sprite[] Trees { get; }

        public Sprite Pixel { get; } = PixelSprites.Pixel();
        public Sprite GrassTile { get; } = PixelSprites.GrassTile();
        public Sprite DirtTile { get; } = PixelSprites.DirtTile();
        public Sprite AntHill { get; } = PixelSprites.AntHillTop();
        public Sprite Crumb { get; } = PixelSprites.Crumb();
        public Sprite Egg { get; } = PixelSprites.Egg();
        public Sprite Mushroom { get; } = PixelSprites.MushroomTop();
        public Sprite Pebble { get; } = PixelSprites.Pebble();
        public Sprite GrassTuft { get; } = PixelSprites.GrassTuft();
        public Sprite[] Bushes { get; } = { PixelSprites.BushTop(1), PixelSprites.BushTop(2), PixelSprites.BushTop(3) };
        public Sprite[] Boulders { get; } = { PixelSprites.Boulder(1), PixelSprites.Boulder(2) };
        public Sprite Stump { get; } = PixelSprites.StumpTop();
        public Sprite Log { get; } = PixelSprites.FallenLog();
        public Sprite Flower { get; } = PixelSprites.Flower();
        public Sprite Shadow { get; } = PixelSprites.Shadow();
        public Sprite RangeCircle { get; } = PixelSprites.RangeCircle();
        public Sprite FallingLeaf { get; } = PixelSprites.FallingLeaf();
        public Sprite RallyFlag { get; } = PixelSprites.RallyFlag();
        public Sprite[] RallyFlagWaving { get; } = PixelSprites.RallyFlagWaving();
        public Sprite SelectionRing { get; } = PixelSprites.SelectionRing();
        public Sprite Wall { get; } = PixelSprites.WallBlock();
        public Sprite Tower { get; } = PixelSprites.Tower();
        public Sprite AcidShot { get; } = PixelSprites.AcidShot();
        public Sprite[] Larva { get; } = PixelSprites.Larva();
        public Sprite Cradle { get; } = PixelSprites.Cradle();
        public Sprite StoragePile { get; } = PixelSprites.StoragePile();
        public Sprite FungusGarden { get; } = PixelSprites.FungusGarden();

        public GameArt(ArtOverrides overrides)
        {
            AntSide = Creature(overrides.ant, overrides.antAttack, PixelSprites.AntWalk, PixelSprites.AntAttack);
            AntSideFacesLeft = HasAnySprite(overrides.ant) && overrides.antFacesLeft;
            AntTop = Creature(overrides.antTopDown, overrides.antTopDownAttack, PixelSprites.AntTopWalk, PixelSprites.AntTopAttack);
            Beetle = Creature(overrides.beetle, overrides.beetleAttack, PixelSprites.BeetleTopWalk, PixelSprites.BeetleTopAttack);
            Spider = Creature(overrides.spider, overrides.spiderAttack, PixelSprites.SpiderTopWalk, PixelSprites.SpiderTopAttack);

            Leaf = overrides.leaf != null ? overrides.leaf : PixelSprites.Leaf();
            Acorn = overrides.acorn != null ? overrides.acorn : PixelSprites.Acorn();
            Berry = overrides.berry != null ? overrides.berry : PixelSprites.Berry();
            Trees = HasAnySprite(overrides.trees) ? RemoveEmpty(overrides.trees) : GenerateTrees();
        }

        public CreatureFrames EnemyFrames(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Spider: return Spider;
                case EnemyKind.Boss: return AntTop; // a giant ant for now
                default: return Beetle;
            }
        }

        /// <summary>The frames set on a prefab, or <paramref name="fallback"/> where the prefab leaves them empty.</summary>
        public static CreatureFrames Frames(CreatureSprites custom, CreatureFrames fallback)
        {
            if (custom == null) return fallback;
            return Creature(custom.walk, custom.attack, () => fallback.Walk, () => fallback.Attack);
        }

        /// <summary>
        /// The child sprite called <paramref name="name"/> if the unit's prefab already has one (its sprite is
        /// filled in if empty), otherwise a new one. <paramref name="created"/> tells which, so callers only size new ones.
        /// </summary>
        public SpriteRenderer FindOrCreateSprite(string name, Transform parent, Sprite sprite, int sortingOrder, out bool created)
        {
            Transform existing = parent.Find(name);
            if (existing != null && existing.TryGetComponent(out SpriteRenderer spriteRenderer))
            {
                created = false;
                if (spriteRenderer.sprite == null) spriteRenderer.sprite = sprite;
                return spriteRenderer;
            }

            created = true;
            return CreateSprite(name, parent, sprite, sortingOrder);
        }

        /// <summary>Creates a child GameObject with a SpriteRenderer.</summary>
        public SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int sortingOrder)
        {
            var spriteObject = new GameObject(name);
            spriteObject.transform.SetParent(parent, false);

            SpriteRenderer spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        /// <summary>Scales a sprite uniformly so it is <paramref name="width"/> world units wide.</summary>
        public static void ScaleToWidth(SpriteRenderer spriteRenderer, float width)
        {
            float scale = width / spriteRenderer.sprite.bounds.size.x;
            spriteRenderer.transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Scales a sprite (non-uniformly) to exactly <paramref name="size"/> world units.</summary>
        public static void Stretch(SpriteRenderer spriteRenderer, Vector2 size)
        {
            Vector3 spriteSize = spriteRenderer.sprite.bounds.size;
            spriteRenderer.transform.localScale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
        }

        /// <summary>
        /// Height of the sprite's pivot above its bottom edge, in local units after scaling.
        /// Place a sprite at (groundY + PivotHeight) to make it stand on the ground.
        /// </summary>
        public static float PivotHeight(SpriteRenderer spriteRenderer)
        {
            Bounds bounds = spriteRenderer.sprite.bounds;
            float pivotToBottom = spriteRenderer.flipY ? bounds.max.y : -bounds.min.y;
            return pivotToBottom * spriteRenderer.transform.localScale.y;
        }

        /// <summary>Sprite height in local units after scaling.</summary>
        public static float Height(SpriteRenderer spriteRenderer)
        {
            return spriteRenderer.sprite.bounds.size.y * spriteRenderer.transform.localScale.y;
        }

        /// <summary>
        /// Imported frames where given, generated ones otherwise. If only the walk art was replaced, the
        /// attack reuses its standing pose so the styles don't clash (the lunge still plays).
        /// </summary>
        static CreatureFrames Creature(Sprite[] walkOverride, Sprite[] attackOverride, Func<Sprite[]> generatedWalk, Func<Sprite[]> generatedAttack)
        {
            bool customWalk = HasAnySprite(walkOverride);
            Sprite[] walk = customWalk ? RemoveEmpty(walkOverride) : generatedWalk();

            Sprite[] attack;
            if (HasAnySprite(attackOverride)) attack = RemoveEmpty(attackOverride);
            else if (customWalk) attack = new[] { walk[0] };
            else attack = generatedAttack();

            return new CreatureFrames(walk, attack);
        }

        static Sprite[] GenerateTrees()
        {
            var trees = new Sprite[TreeVariants];
            for (int i = 0; i < TreeVariants; i++)
                trees[i] = PixelSprites.TreeTop(i);
            return trees;
        }

        static bool HasAnySprite(Sprite[] sprites)
        {
            return sprites != null && Array.Exists(sprites, sprite => sprite != null);
        }

        static Sprite[] RemoveEmpty(Sprite[] sprites)
        {
            return Array.FindAll(sprites, sprite => sprite != null);
        }
    }
}
