using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>Short sounds the game plays (see <see cref="GameAudio"/> for which clip each one uses).</summary>
    public enum Sound
    {
        UiClick,
        UiHover,
        UiConfirm,
        UiError,
        DigSelect,
        Dig,
        TunnelComplete,
        StoneFound,
        TreasureFound,
        RoomBuilt,
        WallBuilt,
        TowerBuilt,
        StructureDestroyed,
        TowerFire,
        SoldierBite,
        EnemyHit,
        EnemyDeath,
        EnemyDeathHeavy,
        AntDied,
        QueenDamage,
        QueenAlarm,
        FoodStored,
        LeavesStored,
        EggLaid,
        AntHatched,
        WaveStarted,
    }

    /// <summary>One sound: its clips (one is picked at random), loudness, pitch wobble and how often it may play.</summary>
    [Serializable]
    public class SoundSlot
    {
        public AudioClip[] clips;
        [Range(0f, 1f)] public float volume = 1f;
        [Tooltip("Each play is pitched up or down by up to this much, so repeats don't sound identical.")]
        [Range(0f, 0.3f)] public float pitchVariation = 0.05f;
        [Tooltip("Seconds before this sound may play again. Stops a crowd of ants from stacking the same sound.")]
        [Min(0f)] public float cooldown = 0.05f;

        public SoundSlot() { }

        public SoundSlot(float volume, float cooldown, float pitchVariation = 0.05f)
        {
            this.volume = volume;
            this.cooldown = cooldown;
            this.pitchVariation = pitchVariation;
        }

        public bool HasClips
        {
            get
            {
                if (clips == null) return false;
                foreach (AudioClip clip in clips)
                {
                    if (clip != null) return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Music, sound effects and their volumes, shown on the "Colony Game" object in the Inspector under Audio.
    /// Changes made while playing are heard right away (but, as always in Unity, are lost when play mode ends).
    /// In the editor, empty slots are filled from Assets/_Project/Art/Musics.
    /// The player's own volume choices (Sound in the menus) are separate and multiply these.
    /// </summary>
    [Serializable]
    public class GameAudio
    {
        [Header("Volume (0 - 1)")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Tooltip("Menu music, the colony playlist, battle and queen-in-danger music.")]
        [Range(0f, 1f)] public float musicVolume = 0.55f;
        [Tooltip("The wave warning, the wave cleared jingle and the game over tune.")]
        [Range(0f, 1f)] public float eventMusicVolume = 0.8f;
        [Range(0f, 1f)] public float effectsVolume = 0.8f;
        [Tooltip("Music volume while the game is paused, as a fraction of normal.")]
        [Range(0f, 1f)] public float pausedMusicFraction = 0.4f;
        [Tooltip("Sounds from the part of the world that isn't on screen (the nest while you look at the surface, and the other way round).")]
        [Range(0f, 1f)] public float otherViewVolume = 0.15f;
        [Tooltip("Sounds from the same view but outside the camera.")]
        [Range(0f, 1f)] public float offScreenVolume = 0.45f;

        [Header("Music")]
        public AudioClip mainMenuMusic;
        [Tooltip("Played one after another during normal play (not the menu or battle tracks).")]
        public AudioClip[] colonyPlaylist;
        [Tooltip("Shuffle the playlist (each track once per round, never the same track twice in a row).")]
        public bool shufflePlaylist = true;
        [Tooltip("How many times each colony track loops before the next one starts.")]
        [Min(1)] public int playsPerTrack = 2;
        [Tooltip("Seconds two tracks overlap when the music changes.")]
        [Min(0.05f)] public float crossfadeSeconds = 1f;

        [Header("Waves and events")]
        [Tooltip("Played once, this many seconds before a wave arrives (the clip is exactly 5 s long).")]
        public AudioClip waveWarning;
        [Min(0f)] public float waveWarningLead = 5f;
        [Tooltip("Looped while enemies are attacking. Each wave takes the next one.")]
        public AudioClip[] battleMusic;
        [Tooltip("Played when the last enemy of a wave is defeated, then the playlist goes on with its next track.")]
        public AudioClip waveCleared;
        [Tooltip("Looped while the queen's health is low.")]
        public AudioClip queenInDanger;
        [Tooltip("Queen-in-danger music starts below this fraction of her health...")]
        [Range(0f, 1f)] public float queenDangerBelow = 0.35f;
        [Tooltip("...and stops once she is healed above this fraction...")]
        [Range(0f, 1f)] public float queenSafeAbove = 0.55f;
        [Tooltip("...or once nothing has hurt her for this many seconds.")]
        [Min(1f)] public float queenDangerCalmSeconds = 8f;
        public AudioClip gameOver;

        [Header("Sound effects: interface")]
        public SoundSlot uiClick = new SoundSlot(0.6f, 0.03f, 0.03f);
        public SoundSlot uiHover = new SoundSlot(0.3f, 0.06f, 0.05f);
        [Tooltip("A blueprint, room or wall was placed.")]
        public SoundSlot uiConfirm = new SoundSlot(0.6f, 0.05f, 0.03f);
        [Tooltip("Can't afford it, or the action isn't possible.")]
        public SoundSlot uiError = new SoundSlot(0.6f, 0.2f, 0f);
        [Tooltip("Tiles marked for digging.")]
        public SoundSlot digSelect = new SoundSlot(0.55f, 0.05f, 0.05f);

        [Header("Sound effects: nest")]
        [Tooltip("Each strike of a builder on the soil.")]
        public SoundSlot dig = new SoundSlot(0.35f, 0.09f, 0.12f);
        public SoundSlot tunnelComplete = new SoundSlot(0.45f, 0.25f, 0.05f);
        public SoundSlot stoneFound = new SoundSlot(0.6f, 0.15f, 0.08f);
        [Tooltip("Digging uncovered a seed cache or a grub.")]
        public SoundSlot treasureFound = new SoundSlot(0.7f, 0.3f, 0f);
        public SoundSlot roomBuilt = new SoundSlot(0.7f, 0.2f, 0.03f);
        [Tooltip("The queen lays an egg.")]
        public SoundSlot eggLaid = new SoundSlot(0.5f, 0.2f, 0.05f);
        [Tooltip("A larva grows into an ant.")]
        public SoundSlot antHatched = new SoundSlot(0.6f, 0.2f, 0.05f);
        [Tooltip("A worker brings food home.")]
        public SoundSlot foodStored = new SoundSlot(0.35f, 0.12f, 0.08f);
        [Tooltip("A worker brings leaves home.")]
        public SoundSlot leavesStored = new SoundSlot(0.35f, 0.12f, 0.08f);

        [Header("Sound effects: surface and fighting")]
        public SoundSlot wallBuilt = new SoundSlot(0.6f, 0.08f, 0.06f);
        public SoundSlot towerBuilt = new SoundSlot(0.7f, 0.2f, 0f);
        [Tooltip("A wall or tower is destroyed.")]
        public SoundSlot structureDestroyed = new SoundSlot(0.7f, 0.1f, 0.06f);
        public SoundSlot towerFire = new SoundSlot(0.35f, 0.08f, 0.1f);
        public SoundSlot soldierBite = new SoundSlot(0.4f, 0.07f, 0.12f);
        [Tooltip("An enemy is hit by acid, or bites something.")]
        public SoundSlot enemyHit = new SoundSlot(0.4f, 0.07f, 0.12f);
        public SoundSlot enemyDeath = new SoundSlot(0.6f, 0.08f, 0.08f);
        [Tooltip("A beetle (a big enemy) dies.")]
        public SoundSlot enemyDeathHeavy = new SoundSlot(0.7f, 0.1f, 0.05f);
        [Tooltip("One of your ants dies. Empty by default.")]
        public SoundSlot antDied = new SoundSlot(0.5f, 0.1f, 0.08f);
        public SoundSlot queenDamage = new SoundSlot(0.6f, 0.6f, 0.05f);
        [Tooltip("The queen's health just got low (with the queen-in-danger music).")]
        public SoundSlot queenAlarm = new SoundSlot(0.7f, 3f, 0f);
        [Tooltip("A wave of enemies arrives.")]
        public SoundSlot waveStarted = new SoundSlot(0.6f, 1f, 0f);

        public SoundSlot SlotFor(Sound sound)
        {
            switch (sound)
            {
                case Sound.UiClick: return uiClick;
                case Sound.UiHover: return uiHover;
                case Sound.UiConfirm: return uiConfirm;
                case Sound.UiError: return uiError;
                case Sound.DigSelect: return digSelect;
                case Sound.Dig: return dig;
                case Sound.TunnelComplete: return tunnelComplete;
                case Sound.StoneFound: return stoneFound;
                case Sound.TreasureFound: return treasureFound;
                case Sound.RoomBuilt: return roomBuilt;
                case Sound.WallBuilt: return wallBuilt;
                case Sound.TowerBuilt: return towerBuilt;
                case Sound.StructureDestroyed: return structureDestroyed;
                case Sound.TowerFire: return towerFire;
                case Sound.SoldierBite: return soldierBite;
                case Sound.EnemyHit: return enemyHit;
                case Sound.EnemyDeath: return enemyDeath;
                case Sound.EnemyDeathHeavy: return enemyDeathHeavy;
                case Sound.AntDied: return antDied;
                case Sound.QueenDamage: return queenDamage;
                case Sound.QueenAlarm: return queenAlarm;
                case Sound.FoodStored: return foodStored;
                case Sound.LeavesStored: return leavesStored;
                case Sound.EggLaid: return eggLaid;
                case Sound.AntHatched: return antHatched;
                default: return waveStarted;
            }
        }

#if UNITY_EDITOR
        const string Folder = "Assets/_Project/Art/Musics/";
        const string Music = Folder + "Music/";
        const string Effects = Folder + "SFX_Optional/";

        /// <summary>Fills empty slots with the clips in Assets/_Project/Art/Musics. True if anything changed.</summary>
        public bool FillEmptyFromProject()
        {
            bool changed = false;
            changed |= Fill(ref mainMenuMusic, Music + "Main_Menu/16_autumn_ant_colony_title_loop.ogg");
            changed |= FillMany(ref colonyPlaylist,
                Music + "Colony_Playlist/01_autumn_colony_loop.ogg",
                Music + "Colony_Playlist/02_underground_colony_loop.ogg",
                Music + "Colony_Playlist/03_larvae_nursery_loop.ogg",
                Music + "Colony_Playlist/09_acorn_storehouse_loop.ogg",
                Music + "Colony_Playlist/10_root_tunnels_loop.ogg",
                Music + "Colony_Playlist/11_fallen_leaves_loop.ogg",
                Music + "Colony_Playlist/12_foragers_march_loop.ogg",
                Music + "Colony_Playlist/13_fungus_gardens_loop.ogg",
                Music + "Colony_Playlist/14_queen_and_cradles_loop.ogg");
            changed |= Fill(ref waveWarning, Music + "Waves_And_Events/04_wave_warning_exact_5s.wav");
            changed |= FillMany(ref battleMusic,
                Music + "Waves_And_Events/05_enemy_wave_loop.ogg",
                Music + "Waves_And_Events/15_beetle_raid_alternate_loop.ogg");
            changed |= Fill(ref waveCleared, Music + "Waves_And_Events/06_wave_victory_jingle.ogg");
            changed |= Fill(ref queenInDanger, Music + "Waves_And_Events/07_queen_in_danger_loop.ogg");
            changed |= Fill(ref gameOver, Music + "Waves_And_Events/08_colony_game_over.ogg");

            changed |= FillSlot(uiClick, "ui_click");
            changed |= FillSlot(uiHover, "ui_hover");
            changed |= FillSlot(uiConfirm, "ui_confirm");
            changed |= FillSlot(uiError, "ui_error");
            changed |= FillSlot(digSelect, "click_dig_select");
            changed |= FillSlot(dig, "dig_1", "dig_2", "dig_3");
            changed |= FillSlot(tunnelComplete, "tunnel_complete");
            changed |= FillSlot(stoneFound, "stone_break");
            changed |= FillSlot(treasureFound, "wave_clear_chime");
            changed |= FillSlot(roomBuilt, "nest_placed");
            changed |= FillSlot(eggLaid, "larva_hatch");
            changed |= FillSlot(antHatched, "worker_spawn");
            changed |= FillSlot(foodStored, "food_collect");
            changed |= FillSlot(leavesStored, "leaves_collect");
            changed |= FillSlot(wallBuilt, "wall_place");
            changed |= FillSlot(towerBuilt, "upgrade");
            changed |= FillSlot(structureDestroyed, "wall_break");
            changed |= FillSlot(towerFire, "tower_fire");
            changed |= FillSlot(soldierBite, "ant_attack");
            changed |= FillSlot(enemyHit, "enemy_hit");
            changed |= FillSlot(enemyDeath, "enemy_death");
            changed |= FillSlot(enemyDeathHeavy, "enemy_death_heavy");
            changed |= FillSlot(queenDamage, "queen_damage");
            changed |= FillSlot(queenAlarm, "queen_alarm");
            changed |= FillSlot(waveStarted, "wave_warning_ping");
            return changed;
        }

        static bool Fill(ref AudioClip slot, string path)
        {
            if (slot != null) return false;
            slot = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return slot != null;
        }

        static bool FillMany(ref AudioClip[] slots, params string[] paths)
        {
            if (slots != null && slots.Length > 0) return false;
            var found = new System.Collections.Generic.List<AudioClip>();
            foreach (string path in paths)
            {
                var clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null) found.Add(clip);
            }
            slots = found.ToArray();
            return found.Count > 0;
        }

        static bool FillSlot(SoundSlot slot, params string[] names)
        {
            if (slot == null || (slot.clips != null && slot.clips.Length > 0)) return false;
            var paths = new string[names.Length];
            for (int i = 0; i < names.Length; i++) paths[i] = Effects + names[i] + ".wav";
            return FillMany(ref slot.clips, paths);
        }
#endif
    }
}
