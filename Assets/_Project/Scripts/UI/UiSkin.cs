using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Images for the HUD, shown on the "Colony Game" object in the Inspector. Each frame is drawn 9-sliced:
    /// its corners keep their shape, the edges stretch one way and the middle fills the rest, so one picture fits
    /// panels of any size. The borders are in pixels of the image (left, right, top, bottom): everything inside them
    /// may be stretched, so keep the moss and other details in the corners. Empty slots use the built-in pixel frames.
    /// In the editor, empty slots are filled from Assets/_Project/Art/UI.
    /// </summary>
    [Serializable]
    public class UiSkin
    {
        [Tooltip("Large panels: resources, report, missions, help, tooltips.")]
        public Texture2D panel;
        [Tooltip("Corner sizes of the panel image in its pixels: x = left, y = right, z = top, w = bottom.")]
        public Vector4 panelBorder = new Vector4(230f, 200f, 150f, 170f);
        [Tooltip("How far the panel image reaches past the panel's content, in UI units (so the rim sits around it).")]
        public float panelBleed = 16f;

        [Tooltip("The advisor's tip card. Text on it is drawn in dark ink.")]
        public Texture2D card;
        public Vector4 cardBorder = new Vector4(40f, 40f, 40f, 40f);

        [Tooltip("The bar behind the action buttons at the bottom.")]
        public Texture2D actionBar;
        public Vector4 actionBarBorder = new Vector4(140f, 190f, 62f, 55f);
        [Tooltip("Where the bar's inner groove is, in its pixels: x = left, y = right, z = top, w = bottom. The buttons sit in it.")]
        public Vector4 actionBarGroove = new Vector4(60f, 60f, 62f, 55f);

        [Tooltip("Shown next to the advisor's warnings.")]
        public Texture2D warningIcon;

        [Header("Buttons")]
        public Texture2D slotNormal;
        [Tooltip("Under the mouse, and while a button's tool is in hand.")]
        public Texture2D slotHover;
        [Tooltip("While the mouse button is held on it.")]
        public Texture2D slotPressed;
        [Tooltip("Action buttons you can't use right now (e.g. can't afford). Drawn whole, not stretched, so its picture keeps its shape.")]
        public Texture2D slotLocked;
        [Tooltip("Corner sizes of the slot images in their pixels: x = left, y = right, z = top, w = bottom.")]
        public Vector4 slotBorder = new Vector4(80f, 80f, 80f, 80f);

        /// <summary>True once the normal slot is set: buttons are drawn with the slot images.</summary>
        public bool HasSlots => slotNormal != null;

        [Header("Menu buttons")]
        [Tooltip("Wide buttons in the main menu, pause menu and end screen (New game, Save game...).")]
        public Texture2D menuButton;
        public Texture2D menuButtonHover;
        public Texture2D menuButtonPressed;
        [Tooltip("Widths of the left and right ends of the menu button image, in pixels of the normal image; " +
                 "only the plank between them stretches (z, w unused). The other images are scaled to match.")]
        public Vector4 menuButtonBorder = new Vector4(130f, 140f, 0f, 0f);

        /// <summary>True once the normal menu button is set: wide buttons are drawn with the menu button images.</summary>
        public bool HasMenuButtons => menuButton != null;

        [Header("Titles")]
        [Tooltip("The plank behind the game's title in the main menu, and behind \"Paused\" and the end screen's title.")]
        public Texture2D headerBanner;
        [Tooltip("Where the banner's plank is, in its pixels: x = left, y = right, z = top, w = bottom. The title is centred in it.")]
        public Vector4 headerBannerPlank = new Vector4(160f, 145f, 100f, 50f);

        [Header("Resource panel icons (top left)")]
        public Sprite iconFood;
        public Sprite iconLeaves;
        public Sprite iconStone;
        [Tooltip("Next to the food per minute.")]
        public Sprite iconRate;
        [Tooltip("Next to the storage capacity.")]
        public Sprite iconStorage;
        [Tooltip("Empty: the game's ant sprite.")]
        public Sprite iconWorker;
        public Sprite iconSoldier;
        public Sprite iconBuilder;
        [Tooltip("Empty: the game's ant sprite, tinted green.")]
        public Sprite iconSpitter;
        [Tooltip("Brood: eggs only.")]
        public Sprite iconEgg;
        [Tooltip("Brood: once a larva is growing.")]
        public Sprite iconEggHatching;

        [Header("Hover particles")]
        [Tooltip("A particle prefab played at both sides of a button when the mouse moves onto it (e.g. falling leaves). Empty: none.")]
        public GameObject buttonHoverParticles;
        [Tooltip("HUD units per world unit of the particle prefab (how big the effect looks on the HUD).")]
        public float buttonHoverParticleScale = 36f;
        [Tooltip("Seconds before moving onto another button plays the effect again.")]
        public float buttonHoverParticleCooldown = 0.12f;

        [Tooltip("UI units per image pixel for the frames (smaller panels shrink it further so the corners fit).")]
        [Range(0.05f, 1f)] public float frameScale = 0.32f;

#if UNITY_EDITOR
        const string Folder = "Assets/_Project/Art/UI/";

        /// <summary>Fills empty slots with the images in Assets/_Project/Art/UI. True if anything changed.</summary>
        public bool FillEmptyFromProject()
        {
            bool changed = false;
            changed |= Fill(ref panel, "Top_Left_HUD.png");
            changed |= Fill(ref card, "Top_Right_HUD.png");
            changed |= Fill(ref actionBar, "Bottom_Main_HUD.png");
            changed |= Fill(ref warningIcon, "Warning Sign.png");
            changed |= Fill(ref slotNormal, "Normal_Slot.png");
            changed |= Fill(ref slotHover, "Hover_Slot.png");
            changed |= Fill(ref slotPressed, "Pressed_Slot.png");
            changed |= Fill(ref slotLocked, "CoolDown_or_CantClick.png");
            changed |= Fill(ref menuButton, "MenuButtons.png");
            changed |= Fill(ref menuButtonHover, "MenuButtonsHOVER.png");
            changed |= Fill(ref menuButtonPressed, "MenuButtonsPRESSED.png");
            changed |= Fill(ref headerBanner, "Header-Banner.png");
            changed |= Fill(ref buttonHoverParticles, "Assets/_Project/Prefabs/Particles/LeafParticle.prefab");
            const string sprites = "Assets/_Project/Art/Sprites/";
            changed |= Fill(ref iconFood, sprites + "Food1.png");
            changed |= Fill(ref iconLeaves, sprites + "Leaf_3.png");
            changed |= Fill(ref iconStone, sprites + "Stone.png");
            changed |= Fill(ref iconRate, sprites + "Clock.png");
            changed |= Fill(ref iconStorage, sprites + "Bag.png");
            changed |= Fill(ref iconSoldier, sprites + "Swordd.png");
            changed |= Fill(ref iconBuilder, sprites + "Hammer.png");
            changed |= Fill(ref iconEgg, sprites + "Egg.png");
            changed |= Fill(ref iconEggHatching, sprites + "Egg_Half_Opened.png");
            return changed;
        }

        static bool Fill<T>(ref T slot, string file) where T : UnityEngine.Object
        {
            if (slot != null) return false;
            slot = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(file.StartsWith("Assets/") ? file : Folder + file);
            return slot != null;
        }
#endif
    }
}
