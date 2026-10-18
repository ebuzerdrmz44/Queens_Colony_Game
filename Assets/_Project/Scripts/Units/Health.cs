using System;
using UnityEngine;

namespace AntColony
{
    /// <summary>Hit points for ants and enemies. Flashes the body red when hit and raises <see cref="Died"/> at zero.</summary>
    public class Health : MonoBehaviour
    {
        const float FlashDuration = 0.12f;
        static readonly Color FlashColor = new Color(1f, 0.35f, 0.35f);

        public int LastDamageWave { get; private set; }
        public bool LastDamageWasStarvation { get; private set; }
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;
        public float Fraction => Max > 0f ? Current / Max : 0f;

        public event Action Died;

        HealthBar bar;
        SpriteRenderer body;
        Color bodyColor;
        float flashTimeLeft;

        public void Init(float maxHealth, HealthBar healthBar, SpriteRenderer bodyRenderer)
        {
            Max = maxHealth;
            Current = maxHealth;
            bar = healthBar;
            body = bodyRenderer;
            bodyColor = bodyRenderer.color;
            bar.SetFraction(1f);
        }

        /// <summary>Raised on every hit (for the floating damage numbers).</summary>
        public static event System.Action<Health, float> Damaged;

        public void TakeDamage(float amount, int sourceWave = 0, bool starvation = false)
        {
            if (IsDead || amount <= 0f) return;

            LastDamageWave = sourceWave;
            LastDamageWasStarvation = starvation;
            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(this, amount);
            bar.SetFraction(Fraction);
            body.color = FlashColor;
            flashTimeLeft = FlashDuration;

            if (IsDead) Died?.Invoke();
        }

        /// <summary>The body's normal colour (it returns to it after a hit flash), e.g. when its art changes.</summary>
        public void SetBodyColor(Color color)
        {
            bodyColor = color;
            if (flashTimeLeft <= 0f && body != null) body.color = color;
        }

        /// <summary>Sets the current health directly (used when loading a saved game).</summary>
        public void SetCurrent(float value)
        {
            Current = Mathf.Clamp(value, 0.01f, Max);
            bar.SetFraction(Fraction);
        }

        /// <summary>Restores health, up to the maximum (used by builders repairing structures).</summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;

            Current = Mathf.Min(Max, Current + amount);
            bar.SetFraction(Fraction);
        }

        void Update()
        {
            if (flashTimeLeft <= 0f) return;

            flashTimeLeft -= Time.deltaTime;
            if (flashTimeLeft <= 0f) body.color = bodyColor;
        }
    }
}
