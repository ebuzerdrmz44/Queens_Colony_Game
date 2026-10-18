using System;
using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// The short story at the start of a new game (about 20 s, skippable with Esc, Space, Enter or a click):
    /// 1. A young queen flies in over the meadow (wings flapping, her shadow on the grass), lands on the spot of the
    ///    future ant hill and digs in, sinking out of sight in a spray of dirt.
    /// 2. Underground she digs her way down the shaft and hollows out her chamber, the soil falling away as she goes,
    ///    then lays her first three eggs.
    /// 3. "Days later..." the starter nest, the first workers and the queen appear as a normal game begins.
    /// The game is frozen meanwhile (it runs on real time); everything goes back to its normal state when it ends.
    /// </summary>
    public class IntroCutscene : MonoBehaviour
    {
        // Timeline, in seconds.
        const float FlyEnd = 4.0f;
        const float LandEnd = 4.5f;
        const float SettleEnd = 5.6f;
        const float DigEnd = 7.9f;
        const float FadeOutEnd = 8.5f;
        const float NestFadeInEnd = 9.1f;
        const float ShaftEnd = 11.9f;
        const float ChamberEnd = 14.5f;
        const float EggsEnd = 17.5f;
        const float LaterFadeOutEnd = 18.1f;
        const float LaterHoldEnd = 19.5f;
        const float LaterFadeInEnd = 20.1f;
        const float ShowColonyEnd = 21.5f;
        const float Total = 22.1f;

        // The top-down queen art: 40 px wide, her middle at x 20, the tip of her head at x 37;
        // the dig-in frames have this many pixels of her front gone into the hole.
        const float TopMiddlePx = 20f;
        const float TopHeadTipPx = 37f;
        static readonly float[] DigHiddenPx = { 8f, 14f, 20f, 26f, 31f };

        const float SurfaceZoom = 4.5f;
        const float NestZoom = 4.2f;
        const float FlapFps = 14f;

        static readonly Color[] SoilChips = { new Color(0.45f, 0.3f, 0.18f), new Color(0.33f, 0.22f, 0.13f), new Color(0.56f, 0.4f, 0.24f) };

        ColonyGame game;
        Action onFinished;
        float time;

        // Actors and props, all removed at the end.
        SpriteRenderer queenActor;
        SpriteRenderer shadow;
        readonly Dictionary<Vector2Int, SpriteRenderer> covers = new Dictionary<Vector2Int, SpriteRenderer>();
        readonly List<SpriteRenderer> eggs = new List<SpriteRenderer>();
        readonly List<GameObject> hidden = new List<GameObject>();
        QueenAnimator.QueenFrame[] flyFrames;
        QueenAnimator.QueenFrame[] walkFrames;
        QueenAnimator.QueenFrame[] layFrames;
        Sprite fallbackQueen;
        Sprite[] topFly;
        Sprite[] topDig;
        Sprite topLanded;
        Sprite topMound;

        Vector3 shadowScale = Vector3.one;
        Transform antHill;
        Vector3 antHillScale = Vector3.one;
        SpriteRenderer hole;
        /// <summary>The strike whose blow has landed (so each blow's dirt, sound and jolt happen once).</summary>
        int landedStrike = -1;
        int landedScene = -1;
        float shakeUntil;
        List<List<Vector2Int>> chamberColumns;
        float chamberMiddleX;
        Vector2 flyFrom;
        Vector2 landAt;
        Vector2 shaftTop;
        Vector2 chamberFloor;
        List<Vector2Int> chamberTiles;
        List<Vector2Int> shaftTiles;
        bool inNest;
        int eggsLaid;

        public bool IsPlaying { get; private set; }
        /// <summary>0 clear .. 1 black: the fades between scenes (drawn by the HUD).</summary>
        public float Blackness { get; private set; }
        public string Caption { get; private set; } = "";
        public float CaptionAlpha { get; private set; }

        public void Init(ColonyGame colonyGame) => game = colonyGame;

        /// <summary>Plays the intro over a freshly built world. <paramref name="finished"/> runs once it ends or is skipped.</summary>
        public void Play(Action finished)
        {
            onFinished = finished;
            time = 0f;
            eggsLaid = 0;
            inNest = false;
            IsPlaying = true;
            Time.timeScale = 0f;

            ReadQueenArt();
            landAt = game.Surface.EntrancePoint;
            flyFrom = landAt + new Vector2(-10f, 5.5f);
            shaftTop = game.Nest.StandPoint(game.Nest.EntranceTile);
            chamberFloor = game.Nest.FootprintBottomCenter(game.Nest.QueenRoom.Origin, game.Nest.QueenRoom.Size);

            queenActor = game.Art.CreateSprite("Intro Queen", game.SurfaceRoot, TopSprite(topFly, 0, topLanded), SortingOrder.FallingLeaves - 1);
            if (topLanded == null && layFrames == null) GameArt.ScaleToWidth(queenActor, Colony.QueenWidth);
            shadow = game.Art.CreateSprite("Intro Shadow", game.SurfaceRoot, game.Art.Shadow, SortingOrder.Shadows);
            shadow.color = new Color(0f, 0f, 0f, 0.35f);
            GameArt.ScaleToWidth(shadow, 1.6f);
            shadowScale = shadow.transform.localScale;

            // The ant hill isn't there yet: it rises as she digs in.
            antHill = game.Surface.transform.Find("Ant Hill");
            if (antHill != null)
            {
                antHillScale = antHill.localScale;
                antHill.gameObject.SetActive(false);
            }
            // The patch of loose earth with the hole she'll dig into (the storyboard's dirt mound).
            hole = game.Art.CreateSprite("Intro Mound", game.SurfaceRoot, topMound != null ? topMound : game.Art.Shadow, SortingOrder.AntHill + 1);
            if (topMound == null)
            {
                hole.color = new Color(0.12f, 0.07f, 0.03f, 0.95f);
                GameArt.ScaleToWidth(hole, 1.3f);
            }
            hole.transform.position = game.Surface.EntrancePoint;
            landedStrike = -1;
            landedScene = -1;
            shakeUntil = 0f;

            HideColony();
            CoverStarterNest();
            game.CameraRig.Cinematic = true;
            game.CameraRig.ShowView(CameraView.Surface);
            game.CameraRig.CinematicShot(landAt + new Vector2(-3f, 1.5f), SurfaceZoom);
        }

        /// <summary>Ends the intro at once (the rest of the story is skipped).</summary>
        public void Skip()
        {
            if (IsPlaying) Finish();
        }

        void Update()
        {
            if (!IsPlaying) return;
            time += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Time.timeScale = 0f; // keep the world frozen, whatever else asks

            if (time < FadeOutEnd) SurfaceScene();
            else if (time < LaterFadeOutEnd) NestScene();
            else EndingScene();

            if (time >= Total) Finish();
        }

        // ---------- 1. The meadow ----------

        /// <summary>
        /// The meadow, after the storyboard, seen from above: she flies in (wings beating, her shadow on the grass)
        /// and lands with her head at the hole's rim, folding her wings (1, 2); then she digs in head-first, blow by
        /// blow, dirt flying, until only the end of her abdomen and wings show (3, 4).
        /// </summary>
        void SurfaceScene()
        {
            Blackness = time < 0.6f ? 1f - time / 0.6f : time > DigEnd ? (time - DigEnd) / (FadeOutEnd - DigEnd) : 0f;
            string caption = time < SettleEnd ? "A young queen searches for a home..." : "...and digs in.";
            SetCaption(caption, time, 0.4f, DigEnd);

            const float pixel = 1f / PixelArt.PixelsPerUnit;
            Vector2 headStop = landAt + new Vector2(-0.55f, 0f);        // at the hole's near rim
            Vector2 view = landAt + new Vector2(-1.0f, 0.3f);

            if (time < FlyEnd)
            {
                // Swooping down, turned the way she flies (in 22.5-degree steps), levelling out to land.
                float t = Mathf.SmoothStep(0f, 1f, time / FlyEnd);
                Vector2 head = Vector2.Lerp(flyFrom, headStop, t);
                float height = Mathf.Lerp(2.4f, 0f, t);
                Vector2 travel = headStop - flyFrom;
                float angle = Mathf.Atan2(travel.y, travel.x) * Mathf.Rad2Deg * (1f - t);
                angle = Mathf.Round(angle / 22.5f) * 22.5f;
                queenActor.sprite = TopSprite(topFly, Mathf.FloorToInt(time * FlapFps), topLanded);
                float scale = 1f + height * 0.12f;                           // nearer the camera while high
                PlaceTop(head + Vector2.up * height * 0.35f, TopHeadTipPx, angle, scale);
                shadow.transform.position = head + new Vector2(-0.9f + height * 0.3f, -0.15f - height * 0.35f);
                shadow.transform.localScale = shadowScale * Mathf.Lerp(0.6f, 1f, t);
                game.CameraRig.CinematicShot(Vector2.Lerp(landAt + new Vector2(-4f, 2f), view, t), SurfaceZoom);
                return;
            }

            shadow.transform.position = headStop + new Vector2(-1.0f, -0.2f);
            if (time < LandEnd)
            {
                // Landed: wings folded, head at the rim.
                queenActor.sprite = TopSprite(null, 0, topLanded);
                PlaceTop(headStop, TopHeadTipPx, 0f, 1f);
            }
            else if (time < SettleEnd)
            {
                // Settling at the spot: a last flutter of her wings, then she folds them and shuffles to the rim.
                float t = (time - LandEnd) / (SettleEnd - LandEnd);
                bool flutter = t > 0.2f && t < 0.45f;
                queenActor.sprite = flutter ? TopSprite(topFly, Mathf.FloorToInt(time * FlapFps), topLanded) : TopSprite(null, 0, topLanded);
                bool shuffle = t > 0.6f && Mathf.FloorToInt(time * 8f) % 2 == 0;
                PlaceTop(headStop + (shuffle ? Vector2.right * pixel : Vector2.zero), TopHeadTipPx, 0f, 1f);
            }
            else
            {
                // Digging in, blow by blow: each blow takes her a little further in, head first.
                const float period = 0.38f;
                float elapsed = time - SettleEnd;
                int strike = Mathf.FloorToInt(elapsed / period);
                float phase = elapsed / period - strike;
                int frame = Mathf.Min(strike, DigHiddenPx.Length);               // 0: still whole, then the dig-in frames

                if (Landed(0, strike, phase))
                {
                    DirtChips.Burst(game, landAt + new Vector2(0f, 0.25f), new Vector2(-1f, 0.8f), SoilChips, 7);
                    DirtChips.Burst(game, landAt + new Vector2(0f, -0.25f), new Vector2(-1f, -0.4f), SoilChips, 7);
                    if (game.Audio != null) game.Audio.Play(Sound.Dig);
                    shakeUntil = time + 0.12f;
                }
                StrikePose(phase, out float push, out float lift, out _);

                float frontPx = TopHeadTipPx;
                Sprite sprite = TopSprite(null, 0, topLanded);
                Vector2 front = headStop;
                if (frame > 0 && topDig != null && topDig.Length > 0)
                {
                    int dig = Mathf.Min(frame - 1, topDig.Length - 1);
                    if (topDig[dig] != null) sprite = topDig[dig];
                    frontPx = 40f - DigHiddenPx[Mathf.Min(dig, DigHiddenPx.Length - 1)];
                    front = landAt;                                              // what's left meets the hole's middle
                }
                queenActor.sprite = sprite;
                PlaceTop(front + new Vector2(push * pixel, lift * pixel), frontPx, 0f, 1f);
                shadow.color = new Color(0f, 0f, 0f, 0.35f * Mathf.Clamp01(1f - elapsed / (DigEnd - SettleEnd)));
            }
            game.CameraRig.CinematicShot(view + Shake(0.06f), SurfaceZoom);
        }

        /// <summary>
        /// Places the top-down queen (facing right, turned by <paramref name="angle"/>) so the point
        /// <paramref name="frontPx"/> pixels from the left of her art lands on <paramref name="front"/>.
        /// </summary>
        void PlaceTop(Vector2 front, float frontPx, float angle, float scale)
        {
            float reach = (frontPx - TopMiddlePx) / PixelArt.PixelsPerUnit * scale;
            Vector2 offset = Quaternion.Euler(0f, 0f, angle) * new Vector2(reach, 0f);
            queenActor.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            queenActor.transform.localScale = Vector3.one * scale;
            queenActor.transform.position = front - offset;
        }

        Sprite TopSprite(Sprite[] frames, int index, Sprite single)
        {
            if (frames != null && frames.Length > 0)
            {
                Sprite sprite = frames[((index % frames.Length) + frames.Length) % frames.Length];
                if (sprite != null) return sprite;
            }
            return single != null ? single : FrameSprite(flyFrames, 0, 0);
        }

        /// <summary>
        /// One blow of the dig cycle at <paramref name="phase"/> (0..1): rear back with the head up, plunge
        /// head-down and forward, then recover. In whole pixels along her facing (push), up (lift), and degrees (tilt).
        /// </summary>
        static void StrikePose(float phase, out float push, out float lift, out float tilt)
        {
            if (phase < 0.35f) { push = -1f; lift = 1f; tilt = 12f; }        // rear back
            else if (phase < 0.55f) { push = 2f; lift = -1f; tilt = -24f; }  // plunge
            else { push = 0f; lift = 0f; tilt = -6f; }                       // recover
        }

        /// <summary>True once per strike, the moment its blow lands (the plunge).</summary>
        bool Landed(int scene, int strike, float phase)
        {
            if (phase < 0.35f) return false;
            if (scene == landedScene && strike <= landedStrike) return false;
            landedScene = scene;
            landedStrike = strike;
            return true;
        }

        // ---------- 2. Underground ----------

        void NestScene()
        {
            if (!inNest) EnterNest();

            Blackness = time < NestFadeInEnd ? 1f - (time - FadeOutEnd) / (NestFadeInEnd - FadeOutEnd) : 0f;

            if (time < ShaftEnd)
            {
                // Down the shaft, head first, one tile per blow: rear up, strike (the tile below breaks, dirt flies
                // up past her), then drop into the hole she made.
                SetCaption("Underground, she digs her first chamber...", time - FadeOutEnd, 0.4f, ChamberEnd - FadeOutEnd);
                queenActor.flipX = false;
                const float pixel = 1f / PixelArt.PixelsPerUnit;
                Vector2 start = shaftTop + Vector2.up * 0.8f;
                if (time < NestFadeInEnd || shaftTiles.Count == 0)
                {
                    PlaceByHead(start, -90f);
                    game.CameraRig.CinematicShot(start + new Vector2(0f, -0.5f), NestZoom);
                    return;
                }
                float period = (ShaftEnd - NestFadeInEnd) / shaftTiles.Count;
                float elapsed = time - NestFadeInEnd;
                int index = Mathf.Min(shaftTiles.Count - 1, Mathf.FloorToInt(elapsed / period));
                float phase = Mathf.Clamp01(elapsed / period - index);
                Vector2 from = index == 0 ? start : game.Nest.TileCenter(shaftTiles[index - 1]);
                Vector2 to = game.Nest.TileCenter(shaftTiles[index]);

                if (Landed(1, index, phase))
                {
                    Uncover(shaftTiles[index], burst: false);
                    DirtChips.Burst(game, to + Vector2.up * 0.3f, Vector2.up, SoilChips, 9);
                    if (game.Audio != null) game.Audio.Play(Sound.Dig);
                    shakeUntil = time + 0.1f;
                }
                Vector2 at;
                if (phase < 0.35f) at = from + Vector2.up * 2f * pixel;               // rear up
                else if (phase < 0.55f) at = from + Vector2.down * 3f * pixel;        // strike
                else at = Vector2.Lerp(from, to, (phase - 0.55f) / 0.45f);           // drop in
                at = new Vector2(at.x, Mathf.Round(at.y / pixel) * pixel);
                PlaceByHead(at, -90f);                                                // head first, on the tile she digs
                queenActor.sprite = FrameSprite(walkFrames ?? flyFrames, index * 2 + (phase < 0.55f ? 0 : 1), 0);
                game.CameraRig.CinematicShot(at + new Vector2(0f, -0.5f) + Shake(0.04f), NestZoom);
            }
            else if (time < ChamberEnd)
            {
                // The chamber, column by column out from the middle, left and right in turn: she steps up to the
                // next column, faces it, strikes, and the column of soil breaks away (flung back behind her).
                foreach (Vector2Int tile in shaftTiles) Uncover(tile, burst: false);
                SetCaption("Underground, she digs her first chamber...", time - FadeOutEnd, 0.4f, ChamberEnd - FadeOutEnd);
                if (chamberColumns == null || chamberColumns.Count == 0) PlanChamberColumns();
                float period = (ChamberEnd - ShaftEnd) / Mathf.Max(1, chamberColumns.Count);
                float elapsed = time - ShaftEnd;
                int index = Mathf.Min(chamberColumns.Count - 1, Mathf.FloorToInt(elapsed / period));
                float phase = Mathf.Clamp01(elapsed / period - index);
                List<Vector2Int> column = chamberColumns[index];
                float columnX = game.Nest.TileCenter(column[0]).x;
                float side = columnX >= chamberMiddleX ? 1f : -1f;

                if (Landed(2, index, phase))
                {
                    foreach (Vector2Int tile in column) Uncover(tile, burst: false);
                    Vector2 middle = new Vector2(columnX, chamberFloor.y + 1f);
                    DirtChips.Burst(game, middle, new Vector2(-side, 0.7f), SoilChips, 10);
                    if (game.Audio != null) game.Audio.Play(Sound.Dig);
                    shakeUntil = time + 0.1f;
                }
                StrikePose(phase, out float push, out float lift, out float tilt);
                const float pixel = 1f / PixelArt.PixelsPerUnit;
                // Her head right at the face of the column she breaks.
                queenActor.flipX = side < 0f;
                float headX = columnX - side * 0.55f;
                PlaceByHead(new Vector2(headX + push * side * pixel, chamberFloor.y + StandHeight() + lift * pixel), tilt * side);
                queenActor.sprite = FrameSprite(walkFrames ?? flyFrames, index, 0);
                game.CameraRig.CinematicShot(chamberFloor + new Vector2(0f, 1.2f) + Shake(0.03f), NestZoom);
            }
            else
            {
                // Three eggs, one after another, laid on the chamber floor.
                foreach (Vector2Int tile in new List<Vector2Int>(chamberTiles)) Uncover(tile, burst: false);
                SetCaption("...and lays her first eggs.", time - ChamberEnd, 0.3f, EggsEnd - ChamberEnd);
                queenActor.flipX = false;
                // Where she'll rest when the game begins: facing right, head over the middle of the nest.
                PlaceByHead(new Vector2(chamberFloor.x, chamberFloor.y + StandHeight()), 0f);
                float t = (time - ChamberEnd) / (EggsEnd - ChamberEnd);
                int frame = Mathf.FloorToInt(t * 3f * 4f) % 4;
                queenActor.sprite = FrameSprite(layFrames ?? flyFrames, frame, 0);
                int due = Mathf.Min(3, Mathf.FloorToInt(t * 3f + 0.35f));
                while (eggsLaid < due) LayEgg();
                game.CameraRig.CinematicShot(chamberFloor + new Vector2(0f, 1.2f), NestZoom);
                Blackness = time > EggsEnd ? Mathf.Clamp01((time - EggsEnd) / (LaterFadeOutEnd - EggsEnd)) : 0f;
            }
        }

        void EnterNest()
        {
            inNest = true;
            queenActor.transform.SetParent(game.NestRoot, true);
            queenActor.transform.localScale = Vector3.one;
            if (layFrames == null) GameArt.ScaleToWidth(queenActor, Colony.QueenWidth);
            queenActor.color = Color.white;
            queenActor.sortingOrder = SortingOrder.Ants + 2;
            shadow.enabled = false;
            game.CameraRig.ShowView(CameraView.Nest);
            game.CameraRig.CinematicShot(shaftTop + new Vector2(0f, -0.5f), NestZoom);
        }

        void LayEgg()
        {
            // Out at her tail, each new egg a little further back.
            float tail = -Colony.QueenHeadOffset(queenActor) - (queenActor != null ? queenActor.bounds.size.x * 0.5f : 1f);
            Vector2 spot = chamberFloor + new Vector2(tail - 0.15f - eggsLaid * 0.45f, 0.12f);
            SpriteRenderer egg = game.Art.CreateSprite("Intro Egg", game.NestRoot, game.Art.Egg, SortingOrder.Ants + 1);
            GameArt.ScaleToWidth(egg, 0.42f);
            egg.transform.position = spot + Vector2.up * GameArt.PivotHeight(egg);
            eggs.Add(egg);
            eggsLaid++;
            // Only the sound: effect prefabs run on game time, which is frozen during the intro.
            if (game.Audio != null) game.Audio.PlayFor(EffectKind.EggLaid, spot);
        }

        // ---------- 3. Days later ----------

        void EndingScene()
        {
            if (queenActor != null) RevealColony();
            SetCaption("Days later, her first workers hatch.", time - LaterFadeOutEnd, 0.3f, ShowColonyEnd - LaterFadeOutEnd);
            if (time < LaterHoldEnd) Blackness = 1f;
            else if (time < LaterFadeInEnd) Blackness = 1f - (time - LaterHoldEnd) / (LaterFadeInEnd - LaterHoldEnd);
            else if (time < ShowColonyEnd) Blackness = 0f;
            else Blackness = Mathf.Clamp01((time - ShowColonyEnd) / (Total - ShowColonyEnd));
            game.CameraRig.CinematicShot(chamberFloor + new Vector2(0f, 2.2f), NestZoom + 0.8f);
        }

        // ---------- Hiding and revealing the starting colony ----------

        void HideColony()
        {
            hidden.Clear();
            foreach (Ant ant in game.Colony.Ants)
            {
                if (ant != null && ant.gameObject.activeSelf)
                {
                    ant.gameObject.SetActive(false);
                    hidden.Add(ant.gameObject);
                }
            }
            Transform realQueen = game.Colony.QueenTransform;
            if (realQueen != null && realQueen.gameObject.activeSelf)
            {
                realQueen.gameObject.SetActive(false);
                hidden.Add(realQueen.gameObject);
            }
        }

        /// <summary>Soil tiles over every tunnel of the starter nest (and its rooms), cleared as she digs.</summary>
        void CoverStarterNest()
        {
            covers.Clear();
            Nest nest = game.Nest;
            chamberTiles = new List<Vector2Int>();
            shaftTiles = new List<Vector2Int>();
            RectInt chamber = new RectInt(nest.QueenRoom.Origin - new Vector2Int(2, 0), nest.QueenRoom.Size + new Vector2Int(4, 1));
            for (int x = 0; x < nest.Width; x++)
            {
                for (int y = 0; y < nest.Height; y++)
                {
                    var tile = new Vector2Int(x, y);
                    if (!nest.IsTunnel(tile)) continue;
                    SpriteRenderer cover = game.Art.CreateSprite("Intro Soil", game.NestRoot, game.Art.DirtTile, SortingOrder.Queen + 1);
                    GameArt.ScaleToWidth(cover, Nest.TileSize * 1.02f);
                    cover.transform.position = nest.TileCenter(tile);
                    covers[tile] = cover;
                    if (chamber.Contains(tile)) chamberTiles.Add(tile);
                    else if (x == nest.EntranceTile.x) shaftTiles.Add(tile);
                }
            }
            shaftTiles.Sort((a, b) => b.y.CompareTo(a.y));   // from the top down
            chamberColumns = null;
        }

        void Uncover(Vector2Int tile, bool burst)
        {
            if (!covers.TryGetValue(tile, out SpriteRenderer cover)) return;
            covers.Remove(tile);
            if (cover == null) return;
            if (burst) DirtChips.Burst(game, cover.transform.position, Vector2.up, SoilChips, 5);
            Destroy(cover.gameObject);
        }

        /// <summary>The real starter colony appears (in the black): soil, eggs and the stand-in queen go.</summary>
        void RevealColony()
        {
            foreach (SpriteRenderer cover in covers.Values) if (cover != null) Destroy(cover.gameObject);
            covers.Clear();
            foreach (SpriteRenderer egg in eggs) if (egg != null) Destroy(egg.gameObject);
            eggs.Clear();
            foreach (GameObject thing in hidden) if (thing != null) thing.SetActive(true);
            hidden.Clear();
            if (queenActor != null) Destroy(queenActor.gameObject);
            if (shadow != null) Destroy(shadow.gameObject);
            if (antHill != null)
            {
                antHill.localScale = antHillScale;
                antHill.gameObject.SetActive(true);
            }
            if (hole != null) Destroy(hole.gameObject);
            hole = null;
            queenActor = null;
            shadow = null;
        }

        void Finish()
        {
            RevealColony();
            IsPlaying = false;
            Blackness = 0f;
            Caption = "";
            CaptionAlpha = 0f;
            game.CameraRig.Cinematic = false;
            game.CameraRig.ShowView(CameraView.Surface);
            // Back to normal time, unless the game is paused or a tutorial pop-up is waiting.
            bool hold = game.IsPaused || (game.Tutorial != null && game.Tutorial.ShowingPopup) || game.State != GameState.Playing;
            Time.timeScale = hold ? 0f : 1f;
            Action done = onFinished;
            onFinished = null;
            done?.Invoke();
        }

        // ---------- Helpers ----------

        void SetCaption(string text, float sinceStart, float fade, float length)
        {
            Caption = text;
            CaptionAlpha = Mathf.Clamp01(Mathf.Min(sinceStart / fade, (length - sinceStart) / fade));
        }

        int FlapPhase()
        {
            int[] beat = { 0, 1, 2, 1 };
            return beat[Mathf.FloorToInt(time * FlapFps) % beat.Length];
        }

        /// <summary>How far her head is in front of the middle of her body (from her art, not turned).</summary>
        float HeadReach() => queenActor != null && queenActor.sprite != null
            ? queenActor.sprite.bounds.size.x * Mathf.Abs(queenActor.transform.localScale.x) * 0.26f : 0.6f;

        /// <summary>Half her height (from her art, not turned): how high her middle stands above the floor.</summary>
        float StandHeight() => queenActor != null && queenActor.sprite != null
            ? queenActor.sprite.bounds.size.y * Mathf.Abs(queenActor.transform.localScale.y) * 0.5f : 0.6f;

        /// <summary>
        /// Puts the stand-in queen so that her head is at <paramref name="head"/>, turned by <paramref name="angle"/>
        /// degrees about her head: her body follows behind it, whichever way she faces or tips.
        /// </summary>
        void PlaceByHead(Vector2 head, float angle)
        {
            float facing = queenActor.flipX ? -1f : 1f;
            Vector2 headFromMiddle = Quaternion.Euler(0f, 0f, angle) * new Vector2(HeadReach() * facing, 0f);
            queenActor.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            queenActor.transform.position = head - headFromMiddle;
        }


        /// <summary>The chamber's tiles in columns, ordered out from the middle, right and left in turn.</summary>
        void PlanChamberColumns()
        {
            var byX = new SortedDictionary<int, List<Vector2Int>>();
            foreach (Vector2Int tile in chamberTiles)
            {
                if (!byX.TryGetValue(tile.x, out List<Vector2Int> list)) byX[tile.x] = list = new List<Vector2Int>();
                list.Add(tile);
            }
            chamberMiddleX = game.Nest.TileCenter(new Vector2Int(game.Nest.EntranceTile.x, 0)).x;
            int middle = game.Nest.EntranceTile.x;
            chamberColumns = new List<List<Vector2Int>>(byX.Values);
            chamberColumns.Sort((a, b) =>
            {
                int da = Mathf.Abs(a[0].x - middle), db = Mathf.Abs(b[0].x - middle);
                return da != db ? da.CompareTo(db) : b[0].x.CompareTo(a[0].x);   // nearer first; right before left
            });
        }

        /// <summary>A short jolt after each blow (stepped, whole pixels).</summary>
        Vector2 Shake(float strength)
        {
            if (time >= shakeUntil) return Vector2.zero;
            const float pixel = 1f / PixelArt.PixelsPerUnit;
            int step = Mathf.FloorToInt(time * 20f);
            float x = ((step * 7) % 3 - 1) * strength;
            float y = ((step * 5) % 3 - 1) * strength;
            return new Vector2(Mathf.Round(x / pixel) * pixel, Mathf.Round(y / pixel) * pixel);
        }

        /// <summary>The queen's own frames from her prefab (idle for flying, walk, laying), or her plain sprite.</summary>
        void ReadQueenArt()
        {
            flyFrames = walkFrames = layFrames = null;
            topFly = topDig = null;
            topLanded = topMound = null;
            fallbackQueen = game.Art.Ant;
            GameObject prefab = game.Prefabs.queen;
            if (prefab != null && prefab.TryGetComponent(out QueenAnimator frames))
            {
                flyFrames = frames.idle != null && frames.idle.Length > 0 ? frames.idle : null;
                walkFrames = frames.walk != null && frames.walk.Length > 0 ? frames.walk : null;
                layFrames = frames.lay != null && frames.lay.Length > 0 ? frames.lay : null;
                topFly = frames.introFly;
                topDig = frames.introDig;
                topLanded = frames.introLanded;
                topMound = frames.introMound;
            }
            if (prefab != null)
            {
                SpriteRenderer body = prefab.GetComponentInChildren<SpriteRenderer>();
                if (body != null && body.sprite != null) fallbackQueen = body.sprite;
            }
        }

        Sprite FrameSprite(QueenAnimator.QueenFrame[] frames, int index, int wing)
        {
            if (frames == null || frames.Length == 0) return fallbackQueen;
            Sprite sprite = frames[((index % frames.Length) + frames.Length) % frames.Length].Get(wing);
            return sprite != null ? sprite : fallbackQueen;
        }
    }
}
