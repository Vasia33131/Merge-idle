using UnityEngine;

namespace CatMerge.Views
{
    /// <summary>
    /// Idle behaviour: pick a random point in the room, walk there, sit for a while, repeat.
    /// </summary>
    public sealed class CatWander : MonoBehaviour
    {
        private enum State { Paused, Sitting, Walking }

        [SerializeField] private float speed = 1.2f;
        [SerializeField, Range(0f, 0.5f)] private float speedVariance = 0.2f;
        [SerializeField] private Vector2 sitDuration = new Vector2(2f, 5f);

        private Room _room;
        private float _padding;
        private float _speed;
        private State _state = State.Paused;
        private Vector2 _target;
        private float _sitTimer;

        public bool IsWalking => _state == State.Walking;

        public void Init(Room room, float padding)
        {
            if (_room != null) _room.BoundsChanged -= OnRoomChanged;
            _room = room;
            _room.BoundsChanged += OnRoomChanged;
            _padding = padding;
            _speed = speed * Random.Range(1f - speedVariance, 1f + speedVariance);
            // Short random first sit so freshly spawned cats don't move in lockstep.
            Sit(Random.Range(0.3f, sitDuration.x));
        }

        private void OnDestroy()
        {
            if (_room != null) _room.BoundsChanged -= OnRoomChanged;
        }

        /// <summary>The room changed shape (screen rotated): walk back inside instead of popping.</summary>
        private void OnRoomChanged()
        {
            if (_state == State.Paused) return; // held or hopping; the drag and the landing clamp on their own

            Vector2 pos = transform.position;
            Vector2 inside = _room.Clamp(pos, _padding);
            if (_state == State.Walking) _target = _room.Clamp(_target, _padding);
            if ((inside - pos).sqrMagnitude < 0.0001f) return;
            _target = inside;
            _state = State.Walking;
        }

        public void Pause()
        {
            _state = State.Paused;
        }

        public void Resume() => Sit(Random.Range(sitDuration.x, sitDuration.y));

        private void Sit(float duration)
        {
            _state = State.Sitting;
            _sitTimer = duration;
        }

        private void Update()
        {
            if (_room == null) return;

            switch (_state)
            {
                case State.Sitting:
                    _sitTimer -= Time.deltaTime;
                    if (_sitTimer <= 0f)
                    {
                        _target = _room.GetRandomPoint(_padding);
                        _state = State.Walking;
                    }
                    break;

                case State.Walking:
                    Vector2 pos = transform.position;
                    Vector2 next = Vector2.MoveTowards(pos, _target, _speed * Time.deltaTime);
                    transform.position = new Vector3(next.x, next.y, transform.position.z);
                    if ((next - _target).sqrMagnitude < 0.0001f) Resume();
                    break;
            }
        }
    }
}
