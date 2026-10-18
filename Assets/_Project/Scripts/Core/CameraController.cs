using UnityEngine;

namespace AntColony
{
    public enum CameraView
    {
        /// <summary>Looks down on the overworld; pans in every direction and zooms.</summary>
        Surface,
        /// <summary>Looks at the nest from the side; pans in every direction and zooms.</summary>
        Nest,
    }

    /// <summary>
    /// Orthographic camera with two views, which are separate scenes: the top-down surface and the side-view nest.
    /// Both pan with WASD and zoom with the mouse wheel.
    /// Each view remembers where it was. Switching fades to black, cuts to the other scene and fades back in.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        const float DefaultZoom = 7f;    // half the screen height in world units
        const float NestDefaultZoom = 6f;
        const float MinZoom = 3.5f;
        const float MaxZoom = 12f;
        const float ZoomStep = 0.12f;    // fraction of the view size per mouse-wheel notch
        const float PanSpeed = 15f;      // world units per second while a key is held (at the default zoom)
        const float Smoothing = 10f;     // higher = snappier
        const float EdgeAllowance = 3f;  // how far past the play area the surface view may look
        const float FadeTime = 0.18f;    // seconds to fade out, and again to fade back in

        ColonyGame game;
        Vector2 surfaceFocus;
        float surfaceZoom = DefaultZoom;
        Vector2 nestFocus;
        float nestZoom = NestDefaultZoom;
        CameraView targetView;
        bool switching;

        // Screen shake: a jolt that dies away, stepped (it jumps to a new spot 20 times a second, by whole pixels).
        const float ShakeStepSeconds = 0.05f;
        float shakeStrength;
        float shakeTime;
        float shakeDuration;
        float shakeStepLeft;
        Vector3 shakeOffset;

        public Camera Camera { get; private set; }
        public CameraView View { get; private set; }
        /// <summary>0 = clear picture, 1 = fully black. The HUD draws it while switching views.</summary>
        public float Fade { get; private set; }

        float ViewSize => View == CameraView.Surface ? surfaceZoom : nestZoom;

        public void Init(ColonyGame colonyGame, Camera targetCamera)
        {
            game = colonyGame;
            Camera = targetCamera;

            Camera.orthographic = true;
            Camera.clearFlags = CameraClearFlags.SolidColor;

            surfaceFocus = game.Surface.EntrancePoint;
            surfaceZoom = DefaultZoom;
            nestZoom = NestDefaultZoom;
            nestFocus = new Vector2(game.Nest.ShaftX, game.Nest.TopY - 5f);
            switching = false;
            Fade = 0f;
            CutTo(CameraView.Surface);
        }

        public void ToggleView()
        {
            // Pressing again mid-fade just changes where we're heading.
            targetView = (switching ? targetView : View) == CameraView.Surface ? CameraView.Nest : CameraView.Surface;
            switching = targetView != View || switching;
        }

        /// <summary>Shakes the view by up to <paramref name="strength"/> world units, dying away over <paramref name="seconds"/>. A stronger shake replaces a weaker one.</summary>
        public void Shake(float strength, float seconds)
        {
            float remaining = shakeDuration > 0f ? shakeStrength * (1f - shakeTime / shakeDuration) : 0f;
            if (strength < remaining) return;
            shakeStrength = strength;
            shakeDuration = Mathf.Max(0.05f, seconds);
            shakeTime = 0f;
            shakeStepLeft = 0f;
        }

        /// <summary>Glides the surface view to look at <paramref name="point"/>.</summary>
        public void FocusSurface(Vector2 point)
        {
            surfaceFocus = point;
        }

        /// <summary>Glides the underground view toward the queen or brood chambers.</summary>
        public void FocusNest(Vector2 point)
        {
            nestFocus = point;
        }

        /// <summary>Scrolls the current view in any direction (faster when zoomed out).</summary>
        public void Pan(Vector2 direction)
        {
            float distance = PanSpeed * Time.unscaledDeltaTime * ViewSize / DefaultZoom;
            if (View == CameraView.Surface) surfaceFocus += direction * distance;
            else nestFocus += direction * distance;
        }

        /// <summary>Zooms the current view in (positive notches) or out (negative).</summary>
        public void Zoom(float notches)
        {
            float factor = 1f - notches * ZoomStep;
            if (View == CameraView.Surface) surfaceZoom = Mathf.Clamp(surfaceZoom * factor, MinZoom, MaxZoom);
            else nestZoom = Mathf.Clamp(nestZoom * factor, MinZoom, MaxZoom);
        }

        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            return Camera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -transform.position.z));
        }

        void LateUpdate()
        {
            if (game == null || game.Nest == null) return;

            transform.position -= shakeOffset; // follow from the steady spot
            UpdateFade();

            ClampFocus();
            float blend = 1f - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime);
            transform.position = Vector3.Lerp(transform.position, TargetPosition(), blend);
            Camera.orthographicSize = Mathf.Lerp(Camera.orthographicSize, ViewSize, blend);

            UpdateShake();
            transform.position += shakeOffset;
        }

        void UpdateShake()
        {
            if (shakeDuration <= 0f || Time.timeScale == 0f)
            {
                if (shakeDuration <= 0f) shakeOffset = Vector3.zero;
                return;
            }

            shakeTime += Time.deltaTime;
            if (shakeTime >= shakeDuration)
            {
                shakeDuration = 0f;
                shakeOffset = Vector3.zero;
                return;
            }

            shakeStepLeft -= Time.deltaTime;
            if (shakeStepLeft > 0f) return;
            shakeStepLeft = ShakeStepSeconds;

            const float pixel = 1f / PixelArt.PixelsPerUnit;
            Vector2 jolt = Random.insideUnitCircle.normalized * shakeStrength * (1f - shakeTime / shakeDuration);
            shakeOffset = new Vector3(Mathf.Round(jolt.x / pixel) * pixel, Mathf.Round(jolt.y / pixel) * pixel, 0f);
        }

        void UpdateFade()
        {
            float step = Time.unscaledDeltaTime / FadeTime;
            if (!switching)
            {
                Fade = Mathf.Max(0f, Fade - step);
                return;
            }

            Fade = Mathf.Min(1f, Fade + step);
            if (Fade < 1f) return;

            switching = false;
            if (targetView != View) CutTo(targetView);
        }

        /// <summary>Jumps straight to a view, without gliding across the gap between the scenes.</summary>
        void CutTo(CameraView view)
        {
            View = view;
            targetView = view;
            Camera.backgroundColor = view == CameraView.Surface ? AutumnPalette.GrassDark : AutumnPalette.Tunnel;
            Camera.orthographicSize = ViewSize;
            ClampFocus();
            shakeOffset = Vector3.zero;
            transform.position = TargetPosition();
        }

        Vector3 TargetPosition()
        {
            return View == CameraView.Surface
                ? new Vector3(surfaceFocus.x, surfaceFocus.y, -10f)
                : new Vector3(nestFocus.x, nestFocus.y, -10f);
        }

        /// <summary>Keeps both remembered positions inside their world.</summary>
        void ClampFocus()
        {
            surfaceFocus = ClampToBounds(surfaceFocus, surfaceZoom, game.Surface.Bounds);

            // Leave a little room above the nest so the entrance and its daylight show.
            Rect nest = game.Nest.WorldBounds;
            nest.yMax += 2f;
            nestFocus = ClampToBounds(nestFocus, nestZoom, nest);
        }

        Vector2 ClampToBounds(Vector2 focus, float zoom, Rect bounds)
        {
            float halfHeight = zoom;
            float halfWidth = zoom * Camera.aspect;
            focus.x = ClampOrCentre(focus.x, bounds.xMin - EdgeAllowance + halfWidth, bounds.xMax + EdgeAllowance - halfWidth);
            focus.y = ClampOrCentre(focus.y, bounds.yMin - EdgeAllowance + halfHeight, bounds.yMax + EdgeAllowance - halfHeight);
            return focus;
        }

        static float ClampOrCentre(float value, float min, float max)
        {
            return min <= max ? Mathf.Clamp(value, min, max) : (min + max) * 0.5f;
        }
    }
}
