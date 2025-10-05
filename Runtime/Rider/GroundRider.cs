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

        [Header("Jump")]
        [SerializeField] private float _jumpForce = 10f;

        [SerializeField] private float _jumpCooldown = 0.2f;

        [Header("Raycast")]
        [SerializeField] private Vector3 _rayOffset = Vector3.zero;

        [SerializeField] private float _maxRayDistance = 2f;
        [SerializeField] private LayerMask _rayMask = -1;

        [Header("Ground Check")]
        [SerializeField] private float _groundCheckDistance = 0.1f;

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
            var otherVelocity = hitBody != null ? hitBody.linearVelocity : Vector3.zero;

            var rayDirVelocity = Vector3.Dot(rayDirection, velocity - otherVelocity);
            var displacement = _rideHeight - hit.distance;

            var springForce = (displacement * _rideSpringStrength) - (rayDirVelocity * _rideSpringDamper);
            springForce = Mathf.Clamp(springForce, -_maxSpringForce, _maxSpringForce);

            var force = rayDirection * -springForce;
            _motorBody.AddForce(force, ForceMode.Force);

            if (hitBody != null)
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

            var goalVelocity = _moveInput * _moveSpeed;
            var linearVelocity = _motorBody.linearVelocity;
            goalVelocity.y = linearVelocity.y;

            var neededAccel = (goalVelocity - linearVelocity) / Time.fixedDeltaTime;
            neededAccel = Vector3.ClampMagnitude(neededAccel, _acceleration);

            _motorBody.AddForce(neededAccel, ForceMode.Acceleration);
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