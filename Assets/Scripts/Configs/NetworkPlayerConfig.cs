using UnityEngine;

namespace CryingOnion.MultiplayerTest
{
    [CreateAssetMenu(fileName = nameof(NetworkPlayerConfig), menuName = "Crying Onion/Configs/NetworkPlayerConfig")]
    public class NetworkPlayerConfig : ScriptableObject
    {
        [field: Tooltip("Layer mask to detect other players when an attack is being carried out.")]
        [field: SerializeField]
        public LayerMask AttackLayerMask { get; private set; }

        [field: Tooltip("Base speed to move the player.")]
        [field: SerializeField]
        public float MoveSpeed { get; private set; } = 7.5f;

        [field: Tooltip("Multiplier that modifies the movement speed when the character runs.")]
        [field: SerializeField, Range(1.0f, 2.0f)]
        public float RunningMultiplier { get; private set; } = 1.25f;

        [field: Tooltip("Character's jumping speed.")]
        [field: SerializeField]
        public float JumpSpeed { get; private set; } = 8.0f;

        [field: Tooltip("Intensity with which gravity affects the character.")]
        [field: SerializeField]
        public float GravityIntensity { get; private set; } = 20.0f;

        [field: Tooltip("The skins that will be used to distinguish the players are decided by the OwnerId.")]
        [field: SerializeField]
        public Texture[] SkinsTextures { get; private set; }
    }
}