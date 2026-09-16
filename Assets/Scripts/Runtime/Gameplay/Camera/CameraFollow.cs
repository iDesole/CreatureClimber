using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Smooth vertical follow + screen shake.
    /// Climb: smooth follow with a floor. Fall: lock to player until ground, then ease to a stop.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        static CameraFollow active;

        [SerializeField] Transform target;
        [SerializeField] float smoothTime = 0.18f;
        [SerializeField] float yOffset = 1.5f;
        [SerializeField] float minY = 0f;
        [SerializeField] float groundSettleTime = 0.22f;
        [SerializeField] bool adaptOrthoSize = true;

        float velocityY;
        float shakeTime;
        float shakeDuration;
        float shakeMagnitude;
        float baseOrtho = 5f;
        Camera cam;
        Vector3 shakeOffset;

        bool fallFollow;
        float fallGroundY;

        bool autoScroll;
        float autoScrollY;
        float autoScrollSpeed;
        int lastScreenH = -1;
        int lastScreenW = -1;
        public void SetTarget(Transform followTarget)
        {
            target = followTarget;
        }

        /// <summary>True while Tempo (or similar) owns vertical camera motion.</summary>
        public static bool IsAutoScrolling => active != null && active.autoScroll;

        public static float AutoScrollY => active != null ? active.autoScrollY : 0f;

        /// <summary>
        /// Start independent upward scroll (Tempo). Y only ever increases while active.
        /// </summary>
        public static void BeginAutoScroll(float startY)
        {
            if (active == null) return;
            active.autoScroll = true;
            active.autoScrollY = startY;
            active.autoScrollSpeed = 0f;
            active.fallFollow = false;
            active.velocityY = 0f;
        }

        public static void SetAutoScrollSpeed(float unitsPerSecond)
        {
            if (active == null) return;
            active.autoScrollSpeed = Mathf.Max(0f, unitsPerSecond);
        }

        public static void EndAutoScroll()
        {
            if (active == null) return;
            active.autoScroll = false;
            active.autoScrollSpeed = 0f;
            active.velocityY = 0f;
        }

        /// <summary>
        /// Follow the player down. Camera never goes below the starter-platform ground Y;
        /// it eases to a stop when it reaches that level.
        /// </summary>
        public static void BeginFallFollow(float groundWorldY)
        {
            if (active == null) return;
            // Tempo owns the camera — don't yank it down with the player.
            if (active.autoScroll) return;
            active.fallFollow = true;
            active.fallGroundY = groundWorldY;
            active.velocityY = 0f;
        }

        public static void EndFallFollow()
        {
            if (active == null) return;
            active.fallFollow = false;
            active.velocityY = 0f;
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
            basePos.x = 0f;

            var sign = GameSettings.ClimbSign;

            if (autoScroll)
            {
                // Tempo owns the frame: pure scroll speed, never tracks the player.
                autoScrollY += autoScrollSpeed * Time.deltaTime * sign;
                basePos = new Vector3(0f, autoScrollY, basePos.z);
            }
            else if (target != null)
            {
                float newY;

                if (fallFollow)
                {
                    // Ground = starter platform Y. Camera floors there (or ceilings in Mirror).
                    var floorY = fallGroundY;
                    var followY = target.position.y + yOffset * sign;

                    if ((followY - floorY) * sign > 0f)
                    {
                        // Still on the climb side of ground — stay locked on the player.
                        newY = followY;
                        velocityY = 0f;
                    }
                    else
                    {
                        // Hit ground — ease to a stop instead of a hard clamp.
                        newY = Mathf.SmoothDamp(
                            basePos.y,
                            floorY,
                            ref velocityY,
                            Mathf.Max(0.05f, groundSettleTime));
                    }
                }
                else
                {
                    var desiredY = target.position.y + yOffset * sign;
                    desiredY = sign > 0f
                        ? Mathf.Max(minY, desiredY)
                        : Mathf.Min(-minY, desiredY);
                    newY = Mathf.SmoothDamp(basePos.y, desiredY, ref velocityY, smoothTime);
                }

                basePos = new Vector3(0f, newY, basePos.z);
            }

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

            transform.position = CrispVisuals.SnapWorldPosition(basePos + shakeOffset, cam);
        }

        void ApplyAdaptiveOrtho()
        {
            if (cam == null || !cam.orthographic) return;
            // Match-width ortho — recompute when the window / device resolution changes.
            if (Screen.height == lastScreenH && Screen.width == lastScreenW) return;
            lastScreenH = Screen.height;
            lastScreenW = Screen.width;
            var recommended = PlatformRuntime.RecommendedOrthoSize(cam);
            cam.orthographicSize = recommended;
            baseOrtho = recommended;
        }

        public static void Shake(float magnitude, float duration)
        {
            if (active == null) return;
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
