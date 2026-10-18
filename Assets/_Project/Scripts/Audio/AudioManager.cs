using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Plays the music and sound effects set in <see cref="GameAudio"/> (Colony Game > Audio in the Inspector).
    /// The music follows the game by itself:
    /// - the main menu has its own loop,
    /// - normal play goes through the colony playlist, crossfading from track to track,
    /// - a few seconds before a wave the warning plays, then battle music loops while enemies are around,
    /// - when the wave is beaten a short jingle plays and the playlist goes on with its next track,
    /// - when the queen's health is low the queen-in-danger loop takes over; losing plays the game over tune.
    /// Sound effects come from <see cref="ColonyGame.PlayEffect"/> and a few direct calls, quieter when they happen
    /// off screen or in the other view (nest / surface).
    /// The player's volume choices (Sound panel in the menus) are kept in PlayerPrefs.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        const int EffectVoices = 12;
        const string MasterKey = "audio.master";
        const string MusicKey = "audio.music";
        const string EffectsKey = "audio.effects";
        const string MutedKey = "audio.muted";
        const string WaveMusicKey = "audio.waveMusic";

        enum MusicMode { None, Menu, Colony, Warning, Battle, WaveCleared, QueenDanger, GameOver, Won }

        /// <summary>One of the two music players that crossfade into each other.</summary>
        class MusicVoice
        {
            public AudioSource Source;
            public float Level;
            public float Target;
        }

        ColonyGame game;
        GameAudio settings;

        readonly MusicVoice[] music = new MusicVoice[2];
        int currentVoice;
        AudioSource eventSource;
        /// <summary>The event music (e.g. the 5 s wave warning) is held while the game is paused, to stay in step with the countdown.</summary>
        bool eventPaused;
        readonly List<AudioSource> effectVoices = new List<AudioSource>();
        int nextEffectVoice;
        readonly Dictionary<Sound, float> lastPlayed = new Dictionary<Sound, float>();

        MusicMode mode = MusicMode.None;
        readonly List<int> shuffleBag = new List<int>();
        int playlistIndex = -1;
        /// <summary>A track the player picked while other music was on; it plays when the playlist comes back.</summary>
        int pickedTrack = -1;
        float colonyTrackTime;
        int battleIndex = -1;
        /// <summary>The wave the battle music last started for, so turning wave music off and on doesn't count as a new wave.</summary>
        int battleWave = -1;
        /// <summary>The colony track last played, so coming back to the playlist (e.g. after a toggle) picks it up again.</summary>
        AudioClip colonyClip;

        // What the music last saw of the world, to notice changes.
        WaveSpawner trackedWaves;
        bool waveWasActive;
        int warnedForWave = -1;
        bool waveClearedPending;
        bool queenInDanger;

        /// <summary>The player's own volumes (0 - 1), set in the Sound panel and remembered between sessions.</summary>
        public float PlayerMaster { get; private set; } = 1f;
        public float PlayerMusic { get; private set; } = 1f;
        public float PlayerEffects { get; private set; } = 1f;
        public bool Muted { get; private set; }
        /// <summary>Off: no wave warning, battle music or wave cleared jingle; the playlist just goes on.</summary>
        public bool WaveMusic { get; private set; } = true;

        public void SetWaveMusic(bool enabled)
        {
            WaveMusic = enabled;
            PlayerPrefs.SetInt(WaveMusicKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>The colony playlist (for the track picker).</summary>
        public AudioClip[] Playlist => settings != null && settings.colonyPlaylist != null ? settings.colonyPlaylist : new AudioClip[0];

        /// <summary>The playlist track playing now, or the one picked to play next; -1 if none yet.</summary>
        public int PlaylistTrack => pickedTrack >= 0 ? pickedTrack : playlistIndex;

        /// <summary>True while the colony playlist is what's playing (not the menu, battle or event music).</summary>
        public bool PlaylistPlaying => mode == MusicMode.Colony;

        /// <summary>
        /// Plays a playlist track now (crossfading), or, while battle or other music is on, as soon as the playlist
        /// comes back. The playlist then goes on from there.
        /// </summary>
        public void PickTrack(int index)
        {
            AudioClip[] tracks = Playlist;
            if (tracks.Length == 0) return;
            index = ((index % tracks.Length) + tracks.Length) % tracks.Length;
            shuffleBag.Remove(index);

            if (mode == MusicMode.Colony)
            {
                playlistIndex = index;
                pickedTrack = -1;
                colonyClip = tracks[index];
                PlayMusic(colonyClip);
                colonyTrackTime = 0f;
            }
            else
            {
                pickedTrack = index;
            }
        }

        /// <summary>The track playing now (for the Sound panel).</summary>
        public AudioClip NowPlaying => music[currentVoice].Source.isPlaying ? music[currentVoice].Source.clip : null;

        public void Init(ColonyGame colonyGame, GameAudio audioSettings)
        {
            game = colonyGame;
            settings = audioSettings;

            for (int i = 0; i < music.Length; i++) music[i] = new MusicVoice { Source = CreateSource($"Music {i + 1}") };
            eventSource = CreateSource("Event Music");
            for (int i = 0; i < EffectVoices; i++) effectVoices.Add(CreateSource($"Effects {i + 1}"));

            PlayerMaster = PlayerPrefs.GetFloat(MasterKey, 1f);
            PlayerMusic = PlayerPrefs.GetFloat(MusicKey, 1f);
            PlayerEffects = PlayerPrefs.GetFloat(EffectsKey, 1f);
            Muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
            WaveMusic = PlayerPrefs.GetInt(WaveMusicKey, 1) == 1;
        }

        AudioSource CreateSource(string sourceName)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        // ---------- Player volume ----------

        public void SetPlayerVolumes(float master, float musicVolume, float effects)
        {
            PlayerMaster = Mathf.Clamp01(master);
            PlayerMusic = Mathf.Clamp01(musicVolume);
            PlayerEffects = Mathf.Clamp01(effects);
            PlayerPrefs.SetFloat(MasterKey, PlayerMaster);
            PlayerPrefs.SetFloat(MusicKey, PlayerMusic);
            PlayerPrefs.SetFloat(EffectsKey, PlayerEffects);
            PlayerPrefs.Save();
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        float MasterGain => Muted ? 0f : settings.masterVolume * PlayerMaster;

        // ---------- Sound effects ----------

        /// <summary>The sound for a moment that also has a visual effect (called by <see cref="ColonyGame.PlayEffect"/>).</summary>
        public void PlayFor(EffectKind kind, Vector2 position)
        {
            switch (kind)
            {
                case EffectKind.SoldierBite: Play(Sound.SoldierBite, position); break;
                case EffectKind.EnemyBite:
                case EffectKind.AcidHit: Play(Sound.EnemyHit, position); break;
                case EffectKind.QueenHit: Play(Sound.QueenDamage, position); break;
                case EffectKind.AntDied: Play(Sound.AntDied, position); break;
                case EffectKind.EggLaid: Play(Sound.EggLaid, position); break;
                case EffectKind.AntHatched: Play(Sound.AntHatched, position); break;
                case EffectKind.TileDug: Play(Sound.TunnelComplete, position); break;
                case EffectKind.DigHit: Play(Sound.Dig, position); break;
                case EffectKind.StoneFound: Play(Sound.StoneFound, position); break;
                case EffectKind.TreasureFound: Play(Sound.TreasureFound, position); break;
                case EffectKind.RoomBuilt: Play(Sound.RoomBuilt, position); break;
                case EffectKind.StructureDestroyed: Play(Sound.StructureDestroyed, position); break;
                case EffectKind.SpitterSpit: Play(Sound.TowerFire, position); break;
                case EffectKind.SpitterAcidHit: Play(Sound.EnemyHit, position); break;
                case EffectKind.BossArrived: Play(Sound.QueenAlarm); break;
                case EffectKind.BossSummon: Play(Sound.WaveStarted, position); break;
                case EffectKind.BossDied: Play(Sound.EnemyDeathHeavy, position); break;
                case EffectKind.BossQuake: Play(Sound.StructureDestroyed); Play(Sound.StoneFound); break;
                case EffectKind.BossMegaSpit: Play(Sound.TowerFire, position); break;
                case EffectKind.BossMegaSpitHit: Play(Sound.StructureDestroyed, position); break;
                // Enemy deaths, finished structures and stored food pick their sound where they happen.
            }
        }

        /// <summary>Plays a sound; with a world position it is quieter when that spot isn't on screen.</summary>
        public void Play(Sound sound, Vector2? worldPosition = null)
        {
            if (settings == null) return;
            SoundSlot slot = settings.SlotFor(sound);
            if (slot == null || !slot.HasClips) return;

            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(sound, out float last) && now - last < slot.cooldown) return;

            float volume = MasterGain * settings.effectsVolume * PlayerEffects * slot.volume;
            if (worldPosition.HasValue) volume *= PlaceVolume(worldPosition.Value);
            if (volume <= 0.001f) return;

            AudioClip clip = PickClip(slot.clips);
            if (clip == null) return;
            lastPlayed[sound] = now;

            AudioSource voice = effectVoices[nextEffectVoice];
            nextEffectVoice = (nextEffectVoice + 1) % effectVoices.Count;
            voice.pitch = 1f + Random.Range(-slot.pitchVariation, slot.pitchVariation);
            voice.PlayOneShot(clip, volume);
        }

        static AudioClip PickClip(AudioClip[] clips)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                AudioClip clip = clips[Random.Range(0, clips.Length)];
                if (clip != null) return clip;
            }
            foreach (AudioClip clip in clips)
            {
                if (clip != null) return clip;
            }
            return null;
        }

        /// <summary>1 on screen, quieter off screen, quieter still in the view (surface / nest) that isn't shown.</summary>
        float PlaceVolume(Vector2 position)
        {
            if (game.Surface == null || game.CameraRig == null || game.CameraRig.Camera == null) return 1f;

            bool onSurface = game.Surface.IsOnSurface(position);
            bool lookingAtSurface = game.CameraRig.View == CameraView.Surface;
            if (onSurface != lookingAtSurface) return settings.otherViewVolume;

            Vector3 viewport = game.CameraRig.Camera.WorldToViewportPoint(position);
            bool visible = viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
            return visible ? 1f : settings.offScreenVolume;
        }

        // ---------- Music ----------

        void Update()
        {
            if (game == null || settings == null) return;
            float deltaTime = Time.unscaledDeltaTime;

            ChooseMusic(deltaTime);
            UpdateVoices(deltaTime);
        }

        void ChooseMusic(float deltaTime)
        {
            WaveSpawner waves = game.Waves;
            if (waves != trackedWaves)
            {
                // A new or loaded game: forget the old one's waves.
                trackedWaves = waves;
                waveWasActive = waves != null && waves.IsWaveActive;
                warnedForWave = -1;
                battleWave = -1;
                waveClearedPending = false;
                queenInDanger = false;
                lastQueenHealth = -1f;
                lastQueenHurt = -100f;
            }

            bool waveActive = waves != null && waves.IsWaveActive;
            if (waveWasActive && !waveActive && game.State == GameState.Playing && WaveMusic) waveClearedPending = true;
            waveWasActive = waveActive;
            if (!WaveMusic)
            {
                // The playlist carries on through waves.
                waveActive = false;
                waveClearedPending = false;
            }

            UpdateQueenDanger();

            MusicMode wanted;
            if (game.InMainMenu) wanted = MusicMode.Menu;
            else if (game.State == GameState.Lost) wanted = MusicMode.GameOver;
            else if (game.State == GameState.Won) wanted = MusicMode.Won;
            else if (queenInDanger && WaveMusic) wanted = MusicMode.QueenDanger;
            else if (waveActive) wanted = MusicMode.Battle;
            else if (waveClearedPending) wanted = MusicMode.WaveCleared;
            else if (mode == MusicMode.WaveCleared && EventBusy && WaveMusic) wanted = MusicMode.WaveCleared;
            else if (IsWarningDue(waves)) wanted = MusicMode.Warning;
            else if (mode == MusicMode.Warning && EventBusy && WaveMusic) wanted = MusicMode.Warning;
            else wanted = MusicMode.Colony;

            if (wanted != mode) Enter(wanted, waves);
            else if (mode == MusicMode.Colony) AdvancePlaylist(deltaTime);
            else if (mode == MusicMode.Won && !EventBusy && settings.mainMenuMusic != null && NowPlaying == null)
                PlayMusic(settings.mainMenuMusic);
        }

        bool IsWarningDue(WaveSpawner waves)
        {
            if (!WaveMusic || waves == null || !waves.HasMoreWaves || settings.waveWarning == null) return false;
            float timeLeft = waves.TimeUntilNextWave;
            return timeLeft > 0f && timeLeft <= settings.waveWarningLead && warnedForWave != waves.CurrentWave + 1;
        }

        /// <summary>
        /// The queen-in-danger music is for while she is actually being hurt: it starts when her health is low and she
        /// was just hurt, and stops once she has healed past the safe line or hasn't been hurt for a while (she heals
        /// slowly, so waiting for the safe line alone could keep it playing for minutes). Off with the wave music tick.
        /// </summary>
        void UpdateQueenDanger()
        {
            Colony colony = game.Colony;
            if (colony == null || game.State != GameState.Playing || colony.QueenMaxHealth <= 0f)
            {
                queenInDanger = false;
                lastQueenHealth = -1f;
                return;
            }

            float health = colony.QueenHealth / colony.QueenMaxHealth;
            if (lastQueenHealth >= 0f && health < lastQueenHealth - 0.0001f) lastQueenHurt = Time.time;
            lastQueenHealth = health;
            bool beingHurt = Time.time - lastQueenHurt < QueenHurtMemory;

            if (!queenInDanger && health < settings.queenDangerBelow && beingHurt)
            {
                queenInDanger = true;
                Play(Sound.QueenAlarm);
            }
            else if (queenInDanger && (health > settings.queenSafeAbove || Time.time - lastQueenHurt > settings.queenDangerCalmSeconds))
            {
                queenInDanger = false;
            }
        }

        /// <summary>A hit this recent counts as "being hurt" for starting the danger music.</summary>
        const float QueenHurtMemory = 3f;
        float lastQueenHealth = -1f;
        float lastQueenHurt = -100f;

        void Enter(MusicMode next, WaveSpawner waves)
        {
            MusicMode previous = mode;
            mode = next;
            switch (next)
            {
                case MusicMode.Menu:
                    StopEvent();
                    PlayMusic(settings.mainMenuMusic);
                    break;

                case MusicMode.Colony:
                    StopEvent();
                    if (colonyClip != null && pickedTrack < 0 && IsStillPlaying(colonyClip))
                    {
                        // Back before the old track faded out (wave music toggled): carry on with it.
                        PlayMusic(colonyClip);
                    }
                    else
                    {
                        colonyClip = NextPlaylistTrack();
                        PlayMusic(colonyClip);
                        colonyTrackTime = 0f;
                    }
                    break;

                case MusicMode.Warning:
                    warnedForWave = waves.CurrentWave + 1;
                    PlayMusic(null);
                    PlayEvent(settings.waveWarning);
                    break;

                case MusicMode.Battle:
                    // A wave called early (N) cuts the warning short.
                    if (previous == MusicMode.Warning) StopEvent();
                    if (waves != null && waves.CurrentWave != battleWave)
                    {
                        // A new wave (not the same one again after a toggle or the queen's danger music).
                        battleWave = waves.CurrentWave;
                        Play(Sound.WaveStarted);
                        battleIndex++;
                    }
                    PlayMusic(PickBattleTrack());
                    break;

                case MusicMode.WaveCleared:
                    waveClearedPending = false;
                    PlayMusic(null);
                    PlayEvent(settings.waveCleared);
                    break;

                case MusicMode.QueenDanger:
                    PlayMusic(settings.queenInDanger != null ? settings.queenInDanger : PickBattleTrack());
                    break;

                case MusicMode.GameOver:
                    PlayMusic(null);
                    PlayEvent(settings.gameOver);
                    break;

                case MusicMode.Won:
                    PlayMusic(null);
                    PlayEvent(settings.waveCleared);
                    break;
            }
        }

        AudioClip PickBattleTrack()
        {
            AudioClip[] tracks = settings.battleMusic;
            if (tracks == null || tracks.Length == 0) return null;
            AudioClip clip = tracks[Mathf.Abs(battleIndex) % tracks.Length];
            return clip != null ? clip : PickClip(tracks);
        }

        /// <summary>Moves on to the next colony track once the current one has played its loops.</summary>
        void AdvancePlaylist(float deltaTime)
        {
            AudioSource source = music[currentVoice].Source;
            if (source.clip == null)
            {
                colonyTrackTime += deltaTime;
                if (colonyTrackTime > 1f) Enter(MusicMode.Colony, game.Waves); // e.g. the playlist was filled in while playing
                return;
            }

            colonyTrackTime += deltaTime;
            float trackLength = source.clip.length * Mathf.Max(1, settings.playsPerTrack);
            if (colonyTrackTime >= trackLength - settings.crossfadeSeconds)
            {
                colonyClip = NextPlaylistTrack();
                PlayMusic(colonyClip);
                colonyTrackTime = 0f;
            }
        }

        /// <summary>The next track: in order, or from a shuffle bag that never repeats the last track first.</summary>
        AudioClip NextPlaylistTrack()
        {
            AudioClip[] tracks = settings.colonyPlaylist;
            if (tracks == null || tracks.Length == 0) return null;

            if (pickedTrack >= 0 && pickedTrack < tracks.Length)
            {
                playlistIndex = pickedTrack;
                pickedTrack = -1;
                return tracks[playlistIndex];
            }
            pickedTrack = -1;

            if (!settings.shufflePlaylist)
            {
                playlistIndex = (playlistIndex + 1) % tracks.Length;
                return tracks[playlistIndex];
            }

            if (shuffleBag.Count == 0)
            {
                for (int i = 0; i < tracks.Length; i++) shuffleBag.Add(i);
                for (int i = shuffleBag.Count - 1; i > 0; i--)
                {
                    int j = Random.Range(0, i + 1);
                    (shuffleBag[i], shuffleBag[j]) = (shuffleBag[j], shuffleBag[i]);
                }
                if (shuffleBag.Count > 1 && shuffleBag[0] == playlistIndex)
                    (shuffleBag[0], shuffleBag[1]) = (shuffleBag[1], shuffleBag[0]);
            }

            playlistIndex = Mathf.Min(shuffleBag[0], tracks.Length - 1);
            shuffleBag.RemoveAt(0);
            return tracks[playlistIndex];
        }

        /// <summary>Crossfades to a looping track; null fades the music out.</summary>
        void PlayMusic(AudioClip clip)
        {
            MusicVoice current = music[currentVoice];
            if (clip != null && current.Source.clip == clip && current.Source.isPlaying)
            {
                current.Target = 1f; // already on (maybe fading out): fade it back in where it is
                return;
            }

            current.Target = 0f;
            if (clip == null) return;

            currentVoice = 1 - currentVoice;
            MusicVoice next = music[currentVoice];
            if (next.Source.clip == clip && next.Source.isPlaying)
            {
                // Still fading out: turn it around instead of restarting it.
                next.Target = 1f;
                return;
            }
            next.Source.Stop();
            next.Source.clip = clip;
            next.Source.loop = true;
            next.Level = 0f;
            next.Target = 1f;
            next.Source.volume = 0f;
            next.Source.Play();
        }

        /// <summary>True if one of the music players still has this clip going (even if fading out).</summary>
        bool IsStillPlaying(AudioClip clip)
        {
            foreach (MusicVoice voice in music)
            {
                if (voice.Source.clip == clip && voice.Source.isPlaying) return true;
            }
            return false;
        }

        bool EventBusy => eventSource.isPlaying || eventPaused;

        void PlayEvent(AudioClip clip)
        {
            eventSource.Stop();
            eventPaused = false;
            if (clip == null) return;
            eventSource.clip = clip;
            eventSource.loop = false;
            eventSource.volume = MasterGain * settings.eventMusicVolume * PlayerMusic;
            eventSource.Play();
        }

        void StopEvent()
        {
            eventSource.Stop();
            eventPaused = false;
        }

        void UpdateVoices(float deltaTime)
        {
            float step = deltaTime / Mathf.Max(0.05f, settings.crossfadeSeconds);
            float pause = game.IsPaused ? settings.pausedMusicFraction : 1f;
            float musicGain = MasterGain * settings.musicVolume * PlayerMusic * pause;

            foreach (MusicVoice voice in music)
            {
                voice.Level = Mathf.MoveTowards(voice.Level, voice.Target, step);
                voice.Source.volume = voice.Level * musicGain;
                if (voice.Level <= 0f && voice.Target <= 0f && voice.Source.isPlaying) voice.Source.Stop();
            }
            if (game.IsPaused && eventSource.isPlaying)
            {
                eventSource.Pause();
                eventPaused = true;
            }
            else if (!game.IsPaused && eventPaused)
            {
                eventSource.UnPause();
                eventPaused = false;
            }
            if (eventSource.isPlaying) eventSource.volume = MasterGain * settings.eventMusicVolume * PlayerMusic;
        }
    }
}
