using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Smooth vertical follow + screen shake + adaptive ortho size for phone vs desktop.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        static CameraFollow active;

        [SerializeField] Transform target;
        [SerializeField] float smoothTime = 0.18f;
        [SerializeField] float yOffset = 1.5f;
        [SerializeField] float minY = 0f;
        [SerializeField] bool adaptOrthoSize = true;

        float velocityY;
        float shakeTime;
        float shakeDuration;
        float shakeMagnitude;
        float baseOrtho = 5f;
        Camera cam;
        Vector3 shakeOffset;

        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
        }

        void OnEnable()
        {
            active = this;
            cam = GetComponent<Camera>();
            if (cam != null && cam.orthographic)
                baseOrtho = cam.orthographicSize;
        }

        void OnDisable()
        {
            if (active == this)
                active = null;
        }

        void Start()
        {
            ApplyAdaptiveOrtho();
        }

        void LateUpdate()
        {
            if (adaptOrthoSize)
                ApplyAdaptiveOrtho();

            var basePos = transform.position;

            // Keep playfield horizontally centered in the phone frame.
            basePos.x = 0f;

            if (target != null)
            {
                var desiredY = Mathf.Max(minY, target.position.y + yOffset);
                var newY = Mathf.SmoothDamp(basePos.y, desiredY, ref velocityY, smoothTime);
                basePos = new Vector3(0f, newY, basePos.z);
            }

            // Decay shake in unscaled time so pause still settles.
            if (shakeTime > 0f && shakeDuration > 0.001f)
            {
                shakeTime -= Time.unscaledDeltaTime;
                var n = Mathf.Clamp01(shakeTime / shakeDuration);
                var damper = n * n;
                shakeOffset = new Vector3(
                    (Mathf.PerlinNoise(Time.unscaledTime * 40f, 0.1f) * 2f - 1f) * shakeMagnitude * damper,
                    (Mathf.PerlinNoise(0.3f, Time.unscaledTime * 40f) * 2f - 1f) * shakeMagnitude * damper,
                    0f);
            }
            else
            {
                shakeOffset = Vector3.zero;
            }

            // Pixel-snap the camera so sprites/UI don't swim on half-pixels.
            transform.position = CrispVisuals.SnapWorldPosition(basePos + shakeOffset, cam);
        }

        void ApplyAdaptiveOrtho()
        {
            if (cam == null || !cam.orthographic) return;
            var recommended = PlatformRuntime.RecommendedOrthoSize(cam);
            // Snap to phone ortho so left/right lane math stays exact.
            cam.orthographicSize = recommended;
            baseOrtho = recommended;
        }

        /// <summary>World-space camera punch. Safe to call when no camera follow exists.</summary>
        public static void Shake(float magnitude, float duration)
        {
            if (active == null) return;
            // Keep the stronger of overlapping shakes.
            if (magnitude >= active.shakeMagnitude || active.shakeTime <= 0f)
            {
                active.shakeMagnitude = magnitude;
                active.shakeDuration = duration;
                active.shakeTime = duration;
            }
            else
            {
                active.shakeTime = Mathf.Max(active.shakeTime, duration * 0.5f);
            }
        }
    }
}
