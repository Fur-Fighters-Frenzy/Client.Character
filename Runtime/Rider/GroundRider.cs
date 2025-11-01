using UnityEngine;

namespace Validosik.Client.Character.Rider
{
    [RequireComponent(typeof(Rigidbody))]
    public partial class GroundRider : MonoBehaviour
    {
        [Header("Motor Body")]
        [SerializeField] private Rigidbody _motorBody;

        [Header("Ride Settings")]
        [SerializeField] private float _rideHeight = 0.5f;

        [SerializeField] private float _rideSpringStrength = 2000f;
        [SerializeField] private float _rideSpringDamper = 0.1f;
        [SerializeField] private float _maxSpringForce = 300f;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 10f;

        [SerializeField] private float _maxSpeed = 10f;
        [SerializeField] private float _acceleration = 200f;
        [SerializeField] private float _airControlMultiplier = 0.25f;

        [Header("Jump")]
        [SerializeField] private float _jumpForce = 15f;

        [SerializeField] private float _jumpCooldown = 0.2f;

        [Tooltip("How long after leaving ground jump is still allowed")]
        [SerializeField] private float _jumpAllowedDuration = 0.15f;

        [SerializeField] private float _jumpLinearDamping = 0.5f;
        [SerializeField] private float _jumpDampingIgnoreDuration = 0.15f;
        [SerializeField] private float _jumpGravityThreshold = 50f;
        [SerializeField] private float _fallGravityMultiplier = 6f;

        [Header("Raycast")]
        [SerializeField] private Vector3 _rayOffset = new Vector3(0, -0.9f, 0);

        [SerializeField] private float _maxRayDistance = 0.6f;
        [SerializeField] private LayerMask _rayMask = -1;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 0.1f;

        [Header("Curves")]
        [Tooltip(
            "Multiplier applied to acceleration when reversing direction. X=-1..1 (dot product), Y=multiplier 1..N")]
        [SerializeField] private AnimationCurve _accelDirectionCurve = AnimationCurve.Linear(-1, 5f, 1, 1f);

        [Tooltip("Multiplier for Rigidbody.linearDamping based on normalized speed 0..1")]
        [SerializeField] private AnimationCurve _dampingCurve = AnimationCurve.EaseInOut(0, 15f, 1, 0.1f);

        private Transform _tr;
        private bool _isGrounded;
        private float _lastGroundedTime;
        private float _lastJumpTime;
        private float _jumpDisableDampingUntil;
        private Vector3 _moveInput;
        private Vector3 _previousInput;

        public bool IsGrounded => _isGrounded;

        private void Awake()
        {
            _tr = transform;

            if (_motorBody == null)
            {
                _motorBody = GetComponent<Rigidbody>();
            }

            _motorBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void FixedUpdate()
        {
            ApplyRideForce();
            ApplyMovement();
            ApplyAdditionalGravity();

            ApplyDamping();
        }

        private void ApplyRideForce()
        {
            var rayOrigin = _tr.TransformPoint(_rayOffset);
            var rayDirection = _tr.TransformDirection(Vector3.down);

            _isGrounded = Physics.Raycast(rayOrigin, rayDirection, out var hit,
                _maxRayDistance, _rayMask);
            if (!_isGrounded)
            {
                return;
            }

            _lastGroundedTime = Time.fixedTime;

            var hitBody = hit.rigidbody;
            var velocity = _motorBody.linearVelocity;
            var hasBody = hitBody != null;
            var otherVelocity = hasBody ? hitBody.linearVelocity : Vector3.zero;

            var rayDirVelocity = Vector3.Dot(rayDirection, velocity - otherVelocity);
            var displacement = _rideHeight - hit.distance;

            var springForce = (displacement * _rideSpringStrength) - (rayDirVelocity * _rideSpringDamper);
            springForce = Mathf.Clamp(springForce, -_maxSpringForce, _maxSpringForce);

            var force = rayDirection * -springForce;
            _motorBody.AddForce(force, ForceMode.Force);

            if (hasBody)
            {
                hitBody.AddForceAtPosition(-force, hit.point, ForceMode.Force);
            }

            Debug.DrawRay(rayOrigin, rayDirection * hit.distance, Color.yellow);
            Debug.DrawRay(rayOrigin, Vector3.up * (springForce * 0.01f), Color.red);
        }

        private void ApplyMovement()
        {
            if (_moveInput.sqrMagnitude < 0.01f)
            {
                return;
            }

            var velocity = _motorBody.linearVelocity;
            var controlMultiplier = _isGrounded ? 1f : _airControlMultiplier;
            var goalVelocity = _moveInput * _moveSpeed;
            goalVelocity.y = velocity.y;

            // Directional reversal boost
            if (_previousInput.sqrMagnitude > 0.01f)
            {
                var dot = Vector3.Dot(_previousInput.normalized, _moveInput.normalized);
                var dirMult = _accelDirectionCurve.Evaluate(dot);
                controlMultiplier *= dirMult;
            }

            _previousInput = _moveInput;

            var neededAccel = (goalVelocity - velocity) / Time.fixedDeltaTime;
            neededAccel = Vector3.ClampMagnitude(neededAccel, _acceleration * controlMultiplier);

            _motorBody.AddForce(neededAccel, ForceMode.Acceleration);
        }

        private void ApplyAdditionalGravity()
        {
            if (_isGrounded || _motorBody.linearVelocity.y >= _jumpGravityThreshold)
            {
                return;
            }

            var gravity = Physics.gravity * (_fallGravityMultiplier - 1f);
            _motorBody.AddForce(gravity, ForceMode.Acceleration);
        }

        private void ApplyDamping()
        {
            var disableDamping = Time.fixedTime < _jumpDisableDampingUntil;
            if (!_isGrounded || disableDamping)
            {
                _motorBody.linearDamping = _jumpLinearDamping;
                return;
            }

            // Damping modulation
            var velocity = _motorBody.linearVelocity;
            var horizontalVel = new Vector3(velocity.x, 0, velocity.z);
            var speedNorm = Mathf.Clamp01(horizontalVel.magnitude / _maxSpeed);
            var dampingMult = _dampingCurve.Evaluate(speedNorm);
            _motorBody.linearDamping = dampingMult; // modulate damping dynamically
        }

        public void Move(Vector3 direction)
        {
            _moveInput = Vector3.ClampMagnitude(direction, 1f);
        }

        public void Jump()
        {
            if (!CanJump())
            {
                return;
            }

            _motorBody.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);
            _lastJumpTime = Time.time;
            _jumpDisableDampingUntil = _lastJumpTime + _jumpDampingIgnoreDuration;
        }

        private bool CanJump()
        {
            if (Time.time - _lastJumpTime < _jumpCooldown)
            {
                return false;
            }

            var recentlyGrounded = (Time.time - _lastGroundedTime) <= _jumpAllowedDuration;
            if (recentlyGrounded)
            {
                return true;
            }

            if (!_isGrounded)
            {
                return false;
            }

            var rayOrigin = _tr.TransformPoint(_rayOffset);
            return Physics.Raycast(rayOrigin, Vector3.down, _rideHeight + _groundCheckDistance, _rayMask);
        }
    }
}