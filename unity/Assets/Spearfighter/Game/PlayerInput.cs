using UnityEngine;
using Spearfighter.Simulation;

namespace Spearfighter.Game
{
    /// <summary>
    /// Scheme B input -> InputCommand. This is the ONLY place raw device input is
    /// read; it produces the same struct a bot or the network produces, so the sim
    /// never knows the difference (WS1).
    ///
    /// Touch (the real target):
    ///   left half   = floating move joystick
    ///   right side  = look drag
    ///   ATTACK btn  = tap => jab, hold => charge; dragging while held aims (sim
    ///                 reduces sensitivity mid-charge). JUMP / BUILD / ROTATE btns.
    /// Desktop (editor sanity): WASD + mouse look + LMB attack + Space/B/R/T.
    ///
    /// Uses the legacy Input Manager so it runs with zero Editor wiring. Migrating
    /// to the Input System (robust multitouch / palm rejection) is WS1 P1 polish.
    /// </summary>
    public sealed class PlayerInput : MonoBehaviour
    {
        [Header("Look sensitivity")]
        public float lookTouchSens = 0.0045f;   // rad per pixel (prototype LOOK_TOUCH)
        public float lookDesktopSens = 0.15f;   // rad per Mouse-axis unit (locked cursor)

        public bool IsTouch { get; private set; }
        public Rect AttackRect { get; private set; }
        public Rect JumpRect { get; private set; }
        public Rect BuildRect { get; private set; }
        public Rect RotateRect { get; private set; }
        public bool JoyActive { get; private set; }
        public Vector2 JoyCenter { get; private set; }
        public Vector2 JoyKnob { get; private set; }

        // per-tick accumulators
        private float _pendYaw, _pendPitch, _pendDrag;
        private Vector2 _move;
        private bool _attackHeld, _jumpHeld, _buildHeld, _rotateLatched, _trajLatched;

        // touch finger tracking
        private int _moveId = -1, _lookId = -1, _atkId = -1, _jumpId = -1, _buildId = -1;
        private Vector2 _moveOrigin, _lookLast, _atkLast;
        private const float JoyRadius = 55f;

        private void Awake()
        {
            IsTouch = Application.isMobilePlatform || (Input.touchSupported && Input.touchCount > 0);
            RecomputeRects();
        }

        public void RecomputeRects()
        {
            float w = Screen.width, h = Screen.height;
            float s = Mathf.Min(w, h);
            float d = s * 0.16f;
            AttackRect = new Rect(w - d * 1.15f, h - d * 1.25f, d, d);
            JumpRect   = new Rect(w - d * 2.35f, h - d * 1.05f, d * 0.72f, d * 0.72f);
            BuildRect  = new Rect(w - d * 1.15f, h - d * 2.5f, d * 0.72f, d * 0.72f);
            RotateRect = new Rect(w - d * 2.1f, h - d * 2.15f, d * 0.6f, d * 0.6f);
        }

        /// <summary>Accumulate raw input for this frame. Called every Update before ticking.</summary>
        public void Sample()
        {
            RecomputeRects(); // track screen size / orientation so buttons stay placed
            if (IsTouch) SampleTouch();
            else SampleDesktop();
        }

        /// <summary>Build a command for one tick and reset per-tick accumulators.</summary>
        public InputCommand Consume()
        {
            // X is negated on both look and move: the camera-facing convention mirrors
            // left/right relative to the sim frame, so "drag/push right" => go right.
            var cmd = new InputCommand
            {
                Move = new System.Numerics.Vector2(-_move.x, _move.y),
                LookYawDelta = -_pendYaw,
                LookPitchDelta = _pendPitch,
                AttackDragPixels = _pendDrag,
                AttackHeld = _attackHeld,
                JumpHeld = _jumpHeld,
                BuildHeld = _buildHeld,
                RotateBuildHeld = _rotateLatched,
                TrajectoryToggleHeld = _trajLatched,
            };
            _pendYaw = _pendPitch = _pendDrag = 0f;
            _rotateLatched = _trajLatched = false; // one-shot edges
            return cmd;
        }

        // ---- desktop ----
        private void SampleDesktop()
        {
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
                Cursor.lockState = CursorLockMode.Locked;
            if (Input.GetKeyDown(KeyCode.Escape))
                Cursor.lockState = CursorLockMode.None;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                _pendYaw += Input.GetAxis("Mouse X") * lookDesktopSens;
                _pendPitch += -Input.GetAxis("Mouse Y") * lookDesktopSens;
            }

            float mx = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            float my = (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f);
            _move = new Vector2(mx, my);

            _attackHeld = Input.GetMouseButton(0) && Cursor.lockState == CursorLockMode.Locked;
            _jumpHeld = Input.GetKey(KeyCode.Space);
            _buildHeld = Input.GetKey(KeyCode.B);
            if (Input.GetKeyDown(KeyCode.R)) _rotateLatched = true;
            if (Input.GetKeyDown(KeyCode.T)) _trajLatched = true;
        }

        // ---- touch (Scheme B) ----
        private void SampleTouch()
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch t = Input.GetTouch(i);
                Vector2 p = t.position;
                switch (t.phase)
                {
                    case TouchPhase.Began: OnTouchBegan(t.fingerId, p); break;
                    case TouchPhase.Moved:
                    case TouchPhase.Stationary: OnTouchMoved(t.fingerId, p); break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled: OnTouchEnded(t.fingerId); break;
                }
            }
        }

        private void OnTouchBegan(int id, Vector2 p)
        {
            if (AttackRect.Contains(p)) { _atkId = id; _attackHeld = true; _atkLast = p; _pendDrag = 0f; return; }
            if (JumpRect.Contains(p)) { _jumpId = id; _jumpHeld = true; return; }
            if (BuildRect.Contains(p)) { _buildId = id; _buildHeld = true; return; }
            if (RotateRect.Contains(p)) { _rotateLatched = true; return; }
            if (p.x < Screen.width * 0.5f && _moveId < 0)
            {
                _moveId = id; _moveOrigin = p; JoyActive = true; JoyCenter = p; JoyKnob = p; _move = Vector2.zero;
            }
            else if (_lookId < 0) { _lookId = id; _lookLast = p; }
        }

        private void OnTouchMoved(int id, Vector2 p)
        {
            if (id == _moveId)
            {
                Vector2 d = p - _moveOrigin;
                if (d.magnitude > JoyRadius) d = d.normalized * JoyRadius;
                JoyKnob = _moveOrigin + d;
                _move = new Vector2(d.x / JoyRadius, d.y / JoyRadius); // y up = forward
            }
            else if (id == _lookId)
            {
                Vector2 d = p - _lookLast;
                _pendYaw += d.x * lookTouchSens;
                _pendPitch += -d.y * lookTouchSens;
                _lookLast = p;
            }
            else if (id == _atkId)
            {
                Vector2 d = p - _atkLast;
                _pendDrag += d.magnitude;
                _pendYaw += d.x * lookTouchSens;
                _pendPitch += -d.y * lookTouchSens;
                _atkLast = p;
            }
        }

        private void OnTouchEnded(int id)
        {
            if (id == _moveId) { _moveId = -1; _move = Vector2.zero; JoyActive = false; }
            else if (id == _lookId) { _lookId = -1; }
            else if (id == _atkId) { _atkId = -1; _attackHeld = false; } // release => sim resolves jab/throw
            else if (id == _jumpId) { _jumpId = -1; _jumpHeld = false; }
            else if (id == _buildId) { _buildId = -1; _buildHeld = false; }
        }
    }
}
