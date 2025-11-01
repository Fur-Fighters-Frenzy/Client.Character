using UnityEngine;

namespace Validosik.Client.Character
{
    public interface ICharacterKinematics
    {
        void ApplyInputStep(Vector2 dir, float dt);

        KinematicState GetState();

        void SetState(in KinematicState s);
    }
}