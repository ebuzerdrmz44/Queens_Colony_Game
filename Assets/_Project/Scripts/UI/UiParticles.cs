using System.Collections.Generic;
using UnityEngine;

namespace AntColony
{
    /// <summary>
    /// Plays particle-system prefabs on the HUD. The HUD is drawn with the immediate-mode GUI, on top of every camera,
    /// so a particle system in the world would be hidden behind the panels. Instead each effect is simulated out of
    /// sight (its renderers switched off, on unscaled time so it also plays in menus and while paused) and its
    /// particles are drawn by the HUD with their sprite, size, rotation and colour.
    /// </summary>
    public class UiParticles
    {
        /// <summary>Where the hidden effects live, far from anything a camera looks at.</summary>
        static readonly Vector3 HiddenPosition = new Vector3(0f, -10000f, 0f);

        class Playing
        {
            public GameObject Root;
            public ParticleSystem[] Systems;
            public Vector2 Origin;
            public float Scale;
            public bool Mirrored;
        }

        readonly List<Playing> playing = new List<Playing>();
        ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[64];
        Transform parent;

        public UiParticles(Transform owner)
        {
            parent = owner;
        }

        /// <summary>
        /// Starts <paramref name="prefab"/> at a HUD point. <paramref name="scale"/> is HUD units per world unit of the effect.
        /// With <paramref name="mirrored"/> the effect is flipped left-right (e.g. for the right side of a button).
        /// </summary>
        public void Play(GameObject prefab, Vector2 origin, float scale, bool mirrored = false)
        {
            if (prefab == null) return;

            GameObject root = Object.Instantiate(prefab, HiddenPosition, prefab.transform.rotation, parent);
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>();
            if (systems.Length == 0)
            {
                Object.Destroy(root);
                return;
            }

            foreach (ParticleSystemRenderer particleRenderer in root.GetComponentsInChildren<ParticleSystemRenderer>())
                particleRenderer.enabled = false;
            foreach (ParticleSystem system in systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.playOnAwake = false;
                main.useUnscaledTime = true;
                main.stopAction = ParticleSystemStopAction.None;
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            // Restart from the beginning (a hair past zero, so a burst at time 0 fires); Update advances it.
            systems[0].Simulate(0.001f, true, true, false);
            playing.Add(new Playing { Root = root, Systems = systems, Origin = origin, Scale = scale, Mirrored = mirrored });
        }

        /// <summary>Advances every effect; call once per frame.</summary>
        public void Update(float deltaTime)
        {
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                Playing effect = playing[i];
                if (effect.Root == null)
                {
                    playing.RemoveAt(i);
                    continue;
                }

                effect.Systems[0].Simulate(deltaTime, true, false, false);
                if (!effect.Systems[0].IsAlive(true))
                {
                    Object.Destroy(effect.Root);
                    playing.RemoveAt(i);
                }
            }
        }

        /// <summary>Draws the particles; call from OnGUI during the repaint event, after everything else.</summary>
        public void Draw()
        {
            foreach (Playing effect in playing)
            {
                if (effect.Root == null) continue;
                foreach (ParticleSystem system in effect.Systems) DrawSystem(system, effect);
            }
        }

        public void Clear()
        {
            foreach (Playing effect in playing)
                if (effect.Root != null) Object.Destroy(effect.Root);
            playing.Clear();
        }

        void DrawSystem(ParticleSystem system, Playing effect)
        {
            int count = system.particleCount;
            if (count == 0) return;
            if (buffer.Length < count) buffer = new ParticleSystem.Particle[count];
            count = system.GetParticles(buffer, count);

            bool local = system.main.simulationSpace == ParticleSystemSimulationSpace.Local;
            Transform emitter = system.transform;
            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            int spriteCount = sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Sprites ? sheet.spriteCount : 0;
            Texture fallbackTexture = null;
            if (spriteCount == 0)
            {
                var particleRenderer = system.GetComponent<ParticleSystemRenderer>();
                if (particleRenderer != null && particleRenderer.sharedMaterial != null) fallbackTexture = particleRenderer.sharedMaterial.mainTexture;
            }

            Matrix4x4 baseMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float flip = effect.Mirrored ? -1f : 1f;

            for (int i = 0; i < count; i++)
            {
                ParticleSystem.Particle particle = buffer[i];
                Vector3 world = local ? emitter.TransformPoint(particle.position) : particle.position;
                Vector3 offset = world - HiddenPosition;
                // World y goes up, HUD y goes down.
                var center = new Vector2(effect.Origin.x + offset.x * flip * effect.Scale, effect.Origin.y - offset.y * effect.Scale);
                float size = particle.GetCurrentSize(system) * effect.Scale;
                if (size <= 0.01f) continue;

                Texture texture = fallbackTexture;
                Rect uv = new Rect(0f, 0f, 1f, 1f);
                float aspect = 1f;
                if (spriteCount > 0)
                {
                    Sprite sprite = sheet.GetSprite((int)(particle.randomSeed % (uint)spriteCount));
                    if (sprite == null) continue;
                    texture = sprite.texture;
                    Rect pixels = sprite.textureRect;
                    uv = new Rect(pixels.x / texture.width, pixels.y / texture.height, pixels.width / texture.width, pixels.height / texture.height);
                    aspect = pixels.height / Mathf.Max(1f, pixels.width);
                }
                if (texture == null) continue;

                var rect = new Rect(center.x - size * 0.5f, center.y - size * aspect * 0.5f, size, size * aspect);
                float angle = -particle.rotation * flip;
                GUI.matrix = baseMatrix * Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one) * Matrix4x4.TRS(-center, Quaternion.identity, Vector3.one);
                GUI.color = particle.GetCurrentColor(system);
                if (effect.Mirrored) uv = new Rect(uv.xMax, uv.y, -uv.width, uv.height);
                GUI.DrawTextureWithTexCoords(rect, texture, uv);
            }

            GUI.matrix = baseMatrix;
            GUI.color = previousColor;
        }
    }
}
