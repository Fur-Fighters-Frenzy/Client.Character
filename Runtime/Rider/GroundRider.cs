using UnityEngine;

namespace Calidosik.Client.Character.Rider
{
    [RequireComponent(typeof(Rigidbody))]
    public class GroundRider : MonoBehaviour
    {
        [Header("Motor Body")]
        [SerializeField] private Rigidbody _motorBody;

        [Header("Ride Settings")]
        [SerializeField] private float _rideHeight = 1f;

        [SerializeField] private float _rideSpringStrength = 50f;
        [SerializeField] private float _rideSpringDamper = 5f;
        [SerializeField] private float _maxSpringForce = 100f;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 8f;

        [SerializeField] private float _maxSpeed = 10f;
        [SerializeField] private float _acceleration = 200f;
        [SerializeField] private float _airControlMultiplier = 0.2f;

        [Header("Jump")]
        [SerializeField] private float _jumpForce = 10f;

        [SerializeField] private float _jumpCooldown = 0.2f;
        [SerializeField] private float _jumpGravityThreshold = 0.2f;

        [Header("Raycast")]
        [SerializeField] private Vector3 _rayOffset = Vector3.zero;

        [SerializeField] private float _maxRayDistance = 2f;
        [SerializeField] private LayerMask _rayMask = -1;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 0.1f;

        [Header("Gravity")]
        [SerializeField] private float _fallGravityMultiplier = 2f;

        [Header("Wall Slide")]
        [SerializeField] private float _wallCheckDistance = 0.5f;

        [SerializeField] private float _wallSlideThreshold = 0.7f;

        private bool _isGrounded;
        private float _lastJumpTime;
        private Vector3 _moveInput;

        public bool IsGrounded => _isGrounded;

        private void Awake()
        {
            if (_motorBody == null)
            {
                _motorBody = GetComponent<Rigidbody>();
            }

            _motorBody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Update()
        {
            Move(new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical")));

            if (Input.GetKeyDown(KeyCode.Space))
            {
                Jump();
            }
        }

        private void FixedUpdate()
        {
            ApplyRideForce();
            ApplyMovement();
            ApplyAdditionalGravity();
            HandleWallSlide();
        }

        private void ApplyRideForce()
        {
            var rayOrigin = transform.TransformPoint(_rayOffset);
            var rayDirection = transform.TransformDirection(Vector3.down);

            _isGrounded = Physics.Raycast(rayOrigin, rayDirection, out var hit,
                _maxRayDistance, _rayMask);
            if (!_isGrounded)
            {
                return;
            }

            Debug.DrawRay(rayOrigin, rayDirection * hit.distance, Color.yellow);

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

        private void HandleWallSlide()
        {
            var velocity = _motorBody.linearVelocity;
            if (_isGrounded || velocity.sqrMagnitude < 0.1f)
            {
                return;
            }

            var horizontalVelocity = new Vector3(velocity.x, 0, velocity.z);
            if (horizontalVelocity.sqrMagnitude < 0.1f)
            {
                return;
            }

            var checkDirection = horizontalVelocity.normalized;
            var rayOrigin = transform.position;

            if (!Physics.Raycast(rayOrigin, checkDirection, out var hit, _wallCheckDistance, _rayMask))
            {
                return;
            }

            var wallDot = Vector3.Dot(hit.normal, Vector3.up);
            if (Mathf.Abs(wallDot) >= _wallSlideThreshold)
            {
                return;
            }

            var normalVelocity = Vector3.Dot(velocity, hit.normal);
            if (normalVelocity >= 0)
            {
                return;
            }

            velocity -= hit.normal * normalVelocity;
            _motorBody.linearVelocity = velocity;
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
        }

        private bool CanJump()
        {
            if (!_isGrounded)
            {
                return false;
            }

            if (Time.time - _lastJumpTime < _jumpCooldown)
            {
                return false;
            }

            var rayOrigin = transform.TransformPoint(_rayOffset);
            return Physics.Raycast(rayOrigin, Vector3.down, _rideHeight + _groundCheckDistance, _rayMask);
        }
    }
}