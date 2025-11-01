using UnityEngine;

namespace Validosik.Client.Character.Rider
{
    public partial class GroundRider : ICharacterKinematics
    {

        public void ApplyInputStep(Vector2 dir, float dt)
        {
            var v = new Vector3(dir.x, 0f, dir.y);
            _moveInput = Vector3.ClampMagnitude(v, 1f);
            // FixedUpdate will process forces; dt provided for symmetry with server loop.
        }

        public KinematicState GetState()
        {
            return new KinematicState
            {
                Position = _tr.position,
                Velocity = _motorBody.linearVelocity,
                Yaw = _tr.eulerAngles.y
            };
        }

        public void SetState(in KinematicState s)
        {
            _motorBody.position = s.Position;
            _motorBody.linearVelocity = s.Velocity;
            var e = _tr.eulerAngles;
            e.y = s.Yaw;
            _tr.eulerAngles = e;
        }
    }
}