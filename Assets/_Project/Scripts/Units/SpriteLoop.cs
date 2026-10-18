using UnityEngine;

namespace AntColony
{
    /// <summary>Plays a few sprites in a loop on a sprite renderer, stepped (e.g. the rally flag waving). Pauses with the game.</summary>
    public class SpriteLoop : MonoBehaviour
    {
        Sprite[] frames;
        float fps;
        SpriteRenderer target;
        float clock;

        public void Init(Sprite[] loopFrames, float framesPerSecond)
        {
            frames = loopFrames;
            fps = framesPerSecond;
            target = GetComponent<SpriteRenderer>();
            clock = Random.value * 10f; // flags side by side don't wave in step
        }

        void Update()
        {
            if (target == null || frames == null || frames.Length == 0) return;
            clock += Time.deltaTime;
            Sprite frame = frames[Mathf.FloorToInt(clock * fps) % frames.Length];
            if (frame != null && target.sprite != frame) target.sprite = frame;
        }
    }
}
