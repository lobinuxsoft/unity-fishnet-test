using Cinemachine;
using FishNet;
using FishNet.Component.ColliderRollback;
using FishNet.Managing;
using FishNet.Managing.Timing;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace CryingOnion.MultiplayerTest
{
    public class NetworkPlayerController : NetworkBehaviour
    {
        private readonly int isGroundedHash = Animator.StringToHash("IS_GROUNDED");
        private readonly int velocityHash = Animator.StringToHash("VELOCITY");
        private readonly int attackingHash = Animator.StringToHash("ATTACKING");
        private readonly int attackTimeHash = Animator.StringToHash("ATTACK_TIME");
        private readonly int textureProperty = Shader.PropertyToID("_BaseMap");

        private readonly SyncVar<ushort> playerHealth = new(100);
        private readonly SyncVar<ushort> playerMaxHealth = new(100);

        public SyncVar<ushort> PlayerHealth => playerHealth;
        public SyncVar<ushort> PlayerMaxHealth => playerMaxHealth;

        [field: Header("Base Setup")] [field: SerializeField]
        public NetworkPlayerConfig NetworkPlayerConfig { get; private set; }

        [Header("Animator Setup")] [Tooltip("Necessary reference to the character animator.")] [SerializeField]
        private Animator animator;

        [Header("Player Graphics Setup")] [Tooltip("Necessary reference so that the character can change skin.")] [SerializeField]
        private Renderer playerRenderer;

        [Header("Camera Target Setup")] [Tooltip("This is the point that the client-side camera will follow.")] [SerializeField]
        private Transform cameraTarget;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private Vector3 currentVelocity = Vector3.zero;

        private CinemachineFreeLook freeLookCamera;
        private Camera mainCamera;
        private Vector3 lastLookDir;

        private void Awake()
        {
            InstanceFinder.TimeManager.OnTick += OnTimeManagerTick;
            characterController = GetComponent<CharacterController>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            lastLookDir = transform.forward;

            // In this section we save a synchronization variable so that both the server and the clients can see specific skins on each player.
            playerRenderer.material.SetTexture(textureProperty, NetworkPlayerConfig.SkinsTextures[OwnerId % NetworkPlayerConfig.SkinsTextures.Length]);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // The corresponding skin is chosen for the client's character.
            playerRenderer.material.SetTexture(textureProperty, NetworkPlayerConfig.SkinsTextures[OwnerId % NetworkPlayerConfig.SkinsTextures.Length]);

            characterController.enabled = (base.IsServerStarted || base.IsOwner);

            if (!base.IsOwner) return;

            mainCamera = Camera.main;
            lastLookDir = transform.forward;
            freeLookCamera ??= FindObjectOfType<CinemachineFreeLook>();
            freeLookCamera.Follow = cameraTarget;
            freeLookCamera.LookAt = cameraTarget;
            freeLookCamera.enabled = true;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnStopClient()
        {
            if (!base.IsOwner) return;

            freeLookCamera.enabled = false;
            freeLookCamera.Follow = null;
            freeLookCamera.LookAt = null;
        }

        private void OnDestroy()
        {
            if (InstanceFinder.TimeManager != null)
                InstanceFinder.TimeManager.OnTick -= OnTimeManagerTick;
        }

        private void OnTimeManagerTick()
        {
            if (base.IsOwner)
            {
                Reconciliation(default, false);
                CheckInput(out MoveData md);
                Move(md, false);

                if (TimeManager.Tick % 3 == 0 && animator.GetFloat(attackTimeHash) > 0.5f)
                    Attack();

                if (playerHealth.Value == ushort.MinValue)
                    NetworkManager.ClientManager.StopConnection();
            }

            if (base.IsServerStarted)
            {
                Move(default, true);
                ReconcileData rd = new ReconcileData(transform.position, transform.rotation);
                Reconciliation(rd, true);
            }
        }

        private void CheckInput(out MoveData md)
        {
            md = default;
            Vector3 direction = Vector3.zero;

            // The direction in which the character should move is calculated using the camera orientation as a reference and that direction is projected on a plane.
            if (mainCamera != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
                Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up);

                /*
                 * In this section I calculate the direction in which the player is going to move, and I make sure that the magnitude of the direction is not greater than 1,
                 * since diagonal movements have a magnitude greater than 1.
                 */
                direction = Vector3.ClampMagnitude(right * Input.GetAxis("Horizontal") + forward * Input.GetAxis("Vertical"), 1.0f);
            }

            // The necessary data is created to replicate the character's movement
            md = new MoveData(direction, Input.GetButton("Jump"), Input.GetKey(KeyCode.LeftShift), Input.GetButton("Fire1"));
        }

        /// <summary>
        /// This function is responsible for making the client's prediction.
        /// </summary>
        [Replicate]
        private void Move(MoveData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false)
        {
            if (asServer || IsHostInitialized)
            {
                float delta = (float)base.TimeManager.TickDelta;
                float strategySpeed = md.Running ? NetworkPlayerConfig.MoveSpeed * NetworkPlayerConfig.RunningMultiplier : NetworkPlayerConfig.MoveSpeed;
                Vector3 targetVelocity = md.MoveDirection * strategySpeed;

                // This section is responsible for reaching the desired speed gradually.
                currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, 5.0f);

                // It is analyzed if the magnitude of the movement to be made is greater than 0, with this we change the orientation of the player.
                if (currentVelocity.sqrMagnitude > 0)
                    lastLookDir = currentVelocity.normalized;

                moveDirection = md.Attacking && characterController.isGrounded ? new Vector3(0, moveDirection.y, 0) : new Vector3(currentVelocity.x, moveDirection.y, currentVelocity.z);

                if (md.Jump && characterController.isGrounded)
                    moveDirection.y = NetworkPlayerConfig.JumpSpeed;

                if (!characterController.isGrounded)
                    moveDirection.y -= NetworkPlayerConfig.GravityIntensity * delta;

                transform.rotation = Quaternion.LookRotation(lastLookDir, Vector3.up);
                characterController.Move(moveDirection * delta);

                // The corresponding animations of the player are triggered, these are automatically synchronized thanks to the NetworkAnimator component.
                animator.SetBool(isGroundedHash, characterController.isGrounded);
                animator.SetFloat(velocityHash, md.Attacking ? 0 : currentVelocity.magnitude / NetworkPlayerConfig.MoveSpeed);
                animator.SetBool(attackingHash, md.Attacking);
            }
        }

        /// <summary>
        /// This function is responsible for Reconciling with the server.
        /// </summary>
        [Reconcile]
        private void Reconciliation(ReconcileData rd, bool asServer, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
        }

        [Client]
        private void Attack()
        {
            PreciseTick pt = TimeManager.GetPreciseTick(TimeManager.LastPacketTick.Value());
            ServerAttack(pt);
        }

        [ServerRpc]
        private void ServerAttack(PreciseTick pt)
        {
            RollbackManager.Rollback(pt, RollbackPhysicsType.Physics, IsOwner);
            Collider[] colliders = new Collider[10];
            int amount = Physics.OverlapSphereNonAlloc(transform.position + transform.up * 0.5f, 1f, colliders, NetworkPlayerConfig.AttackLayerMask);

            for (int i = 0; i < amount; i++)
            {
                if (colliders[i].TryGetComponent(out NetworkPlayerController other))
                    if (other.OwnerId != OwnerId)
                    {
                        other.playerHealth.Value -= 10;
                        NetworkManager.Log($"<b>Client {other.OwnerId}</b> <color=green>Health: {other.playerHealth.Value} / {100}</color> -> <color=blue>% {100 * (other.playerHealth.Value / 100.0f):000}</color>");
                    }
            }

            RollbackManager.Return();
        }
    }
}