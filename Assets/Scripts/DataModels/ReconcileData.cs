using FishNet.Object.Prediction;
using UnityEngine;

namespace CryingOnion.MultiplayerTest
{
    public struct ReconcileData : IReconcileData
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public ReconcileData(Vector3 position, Quaternion rotation)
        {
            Position = position;
            Rotation = rotation;
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