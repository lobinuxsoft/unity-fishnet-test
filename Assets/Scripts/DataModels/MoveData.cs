using FishNet.Object.Prediction;
using UnityEngine;

namespace CryingOnion.MultiplayerTest
{
    public struct MoveData : IReplicateData
    {
        public Vector3 MoveDirection;
        public bool Jump;
        public bool Running;
        public bool Attacking;

        public MoveData(Vector3 moveDirection, bool jump, bool running, bool attacking)
        {
            MoveDirection = moveDirection;
            Jump = jump;
            Running = running;
            Attacking = attacking;
            _tick = 0;
        }

        private uint _tick;

        public void Dispose()
        {
        }

        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}