using UnityEngine;

namespace CreatureClimb
{
    /// <summary>
    /// Mario Kart–style ghost — follows the same pre-built race pads as the player.
    /// </summary>
    public class RaceGhost : MonoBehaviour
    {
        SpriteRenderer spriteRenderer;
        LeafSpawner spawner;
        float standOffset = 0.35f;
        float displayHeight;
        float targetHeight;
        int finishHeight = 100;
        float baseScale = 1f;

        public static RaceGhost Create(Transform parent)
        {
            var go = new GameObject("RaceGhost");
            if (parent != null)
                go.transform.SetParent(parent, false);
            var ghost = go.AddComponent<RaceGhost>();
            ghost.Build();
            return ghost;
        }

        void Build()
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sortingOrder = 4;
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.45f);
        }

        public void Configure(Creature opponent, LeafSpawner leafSpawner, int raceFinishHeight)
        {
            spawner = leafSpawner;
            finishHeight = Mathf.Max(1, raceFinishHeight);
            standOffset = opponent != null ? opponent.standOffsetY : 0.35f;

            if (spriteRenderer == null)
                Build();

            if (opponent != null)
            {
                var sprite = opponent.ResolveSprite();
                if (sprite != null)
                    spriteRenderer.sprite = sprite;

                var tint = opponent.tint;
                tint.a = 0.45f;
                spriteRenderer.color = tint;

                var req = Mathf.Max(0.1f, opponent.scale);
                var fit = PlatformRuntime.FitScaleForCreature(spriteRenderer.sprite, req);
                baseScale = fit;
                transform.localScale = Vector3.one * fit;
                standOffset = opponent.standOffsetY * (fit / req);
                CrispVisuals.MakeSpriteCrisp(spriteRenderer);
            }

            displayHeight = 0f;
            targetHeight = 0f;
            gameObject.SetActive(true);
            SnapToHeight(0f);
        }

        public void SetAiHeight(int height)
        {
            targetHeight = Mathf.Clamp(height, 0, finishHeight);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!gameObject.activeSelf) return;

            displayHeight = Mathf.MoveTowards(displayHeight, targetHeight, 6f * Time.deltaTime);
            SnapToHeight(displayHeight);
        }

        void SnapToHeight(float height)
        {
            var h = Mathf.Clamp(height, 0f, finishHeight);
            var h0 = Mathf.FloorToInt(h);
            var h1 = Mathf.Min(h0 + 1, finishHeight);
            var t = h - h0;

            Vector3 p0;
            Vector3 p1;

            if (spawner != null && spawner.HasRaceCourse)
            {
                p0 = spawner.GetRaceStandPosition(h0, standOffset);
                p1 = spawner.GetRaceStandPosition(h1, standOffset);
            }
            else
            {
                var spacing = spawner != null ? spawner.VerticalSpacing : 2.2f;
                p0 = new Vector3(0f, 0f, 0f) + GameSettings.StandOffset(h0 * spacing + standOffset);
                p1 = new Vector3(0f, 0f, 0f) + GameSettings.StandOffset(h1 * spacing + standOffset);
            }

            transform.position = CrispVisuals.SnapWorldPosition(Vector3.Lerp(p0, p1, t));
            transform.localScale = Vector3.one * baseScale;
            if (spriteRenderer != null)
                spriteRenderer.flipY = false;
        }
    }
}
