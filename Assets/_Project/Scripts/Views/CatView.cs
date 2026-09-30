using System.Collections;
using CatMerge.Core;
using CatMerge.Data;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CatMerge.Views
{
    /// <summary>
    /// Visual representation of one <see cref="CatData"/>. Placeholder art: tinted sprite from <see cref="CatConfig"/> + level number.
    /// </summary>
    [RequireComponent(typeof(SortingGroup), typeof(CatWander))]
    public sealed class CatView : MonoBehaviour
    {
        private const int DragSortingOrder = 30000;
        private const float SortingPrecision = 100f;

        [Tooltip("Child that holds body + label; scaled/hopped by animations. Shadow stays on the ground.")]
        [SerializeField] private Transform visual;
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private TMP_Text label;
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float dragScale = 1.15f;
        [SerializeField] private float walkBobHeight = 0.06f;
        [SerializeField] private float walkBobFrequency = 10f;
        [SerializeField] private float meowStretch = 0.15f;

        private SortingGroup _sorting;
        private CatWander _wander;
        private Room _room;
        private Coroutine _scaleRoutine;
        private Coroutine _moveRoutine;
        private float _hopHeight;
        private float _bobPhase;

        public int CatId { get; private set; }
        public int Level { get; private set; }
        public float Radius => radius;
        public bool IsDragging { get; private set; }
        public Vector2 Position => transform.position;

        private void Awake()
        {
            _sorting = GetComponent<SortingGroup>();
            _wander = GetComponent<CatWander>();
        }

        public void Init(CatData data, CatConfig config, Room room)
        {
            CatId = data.Id;
            Level = data.Level;
            _room = room;
            name = $"Cat #{data.Id} (L{data.Level})";

            if (config.sprite != null) body.sprite = config.sprite;
            body.color = config.color;
            label.text = Level.ToString();
            label.color = GetLabelColor(config.color);

            _wander.Init(room, radius);
        }

        public bool Contains(Vector2 worldPoint) => (worldPoint - Position).sqrMagnitude <= radius * radius;

        public void BeginDrag()
        {
            IsDragging = true;
            StopMove();
            _wander.Pause();
            ScaleTo(dragScale, 0.1f);
        }

        public void MoveTo(Vector2 worldPoint)
        {
            transform.position = new Vector3(worldPoint.x, worldPoint.y, transform.position.z);
        }

        /// <summary>Drop without merge: hop to <paramref name="landing"/>, then go back to wandering.</summary>
        public void Bounce(Vector2 landing)
        {
            IsDragging = false;
            ScaleTo(1f, 0.15f);
            StopMove();
            _moveRoutine = StartCoroutine(BounceRoutine(_room.Clamp(landing, radius)));
        }

        public void PlaySpawn()
        {
            visual.localScale = Vector3.zero;
            ScaleTo(1f, 0.25f, overshoot: true);
        }

        /// <summary>Short squash-and-stretch while the cat meows. Skipped while it is held.</summary>
        public void PlayMeow()
        {
            if (IsDragging) return;
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(MeowRoutine());
        }

        private IEnumerator MeowRoutine()
        {
            const float duration = 0.35f;
            float from = visual.localScale.y;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                // Stretch up, settle back with a small overshoot; width does the opposite.
                float wave = Mathf.Sin(t * Mathf.PI * 2f) * (1f - t) * meowStretch;
                float baseScale = Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t * 3f));
                visual.localScale = new Vector3(baseScale * (1f - wave * 0.6f), baseScale * (1f + wave), 1f);
                yield return null;
            }
            visual.localScale = Vector3.one;
            _scaleRoutine = null;
        }

        private void Update()
        {
            float bob = 0f;
            if (_wander.IsWalking)
            {
                _bobPhase += Time.deltaTime * walkBobFrequency;
                bob = Mathf.Abs(Mathf.Sin(_bobPhase)) * walkBobHeight;
            }
            else
            {
                _bobPhase = 0f;
            }
            visual.localPosition = new Vector3(0f, _hopHeight + bob, 0f);
        }

        private void LateUpdate()
        {
            // Lower on screen = closer to the viewer = drawn on top.
            _sorting.sortingOrder = IsDragging
                ? DragSortingOrder
                : Mathf.RoundToInt(-transform.position.y * SortingPrecision);
        }

        private IEnumerator BounceRoutine(Vector2 landing)
        {
            Vector2 start = Position;
            const float duration = 0.3f;
            const float height = 0.35f;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                MoveTo(Vector2.Lerp(start, landing, Mathf.SmoothStep(0f, 1f, t)));
                _hopHeight = Mathf.Sin(t * Mathf.PI) * height;
                yield return null;
            }
            MoveTo(landing);
            _hopHeight = 0f;
            _moveRoutine = null;
            _wander.Resume();
        }

        private void StopMove()
        {
            if (_moveRoutine != null) StopCoroutine(_moveRoutine);
            _moveRoutine = null;
            _hopHeight = 0f;
        }

        private void ScaleTo(float target, float duration, bool overshoot = false)
        {
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(target, duration, overshoot));
        }

        private IEnumerator ScaleRoutine(float target, float duration, bool overshoot)
        {
            float from = visual.localScale.x;
            for (float t = 0f; t < 1f; t += Time.deltaTime / duration)
            {
                float k = overshoot ? EaseOutBack(t) : Mathf.SmoothStep(0f, 1f, t);
                visual.localScale = Vector3.one * Mathf.LerpUnclamped(from, target, k);
                yield return null;
            }
            visual.localScale = Vector3.one * target;
            _scaleRoutine = null;
        }

        /// <summary>Readable text color on top of a cat body of the given color.</summary>
        public static Color GetLabelColor(Color bodyColor)
        {
            Color.RGBToHSV(bodyColor, out _, out _, out float v);
            return v > 0.6f ? new Color(0.18f, 0.14f, 0.12f) : Color.white;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = t - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }
    }
}
