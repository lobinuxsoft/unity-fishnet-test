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
    public class PlayerController : NetworkBehaviour
    {
        private const float RUNNING_MULTIPLIER = 1.25f;
        private readonly int isGroundedHash = Animator.StringToHash("IS_GROUNDED");
        private readonly int velocityHash = Animator.StringToHash("VELOCITY");
        private readonly int attackingHash = Animator.StringToHash("ATTACKING");
        private readonly int attackTimeHash = Animator.StringToHash("ATTACK_TIME");
        private readonly int textureProperty = Shader.PropertyToID("_MainTex");
        private readonly SyncVar<int> playerId = new();

        [field: Header("Base Setup")]
        [field: SerializeField] public LayerMask AttackLayerMask { get; private set; }
        [field: SerializeField] public float MoveSpeed { get; private set; } = 7.5f;
        [field: SerializeField] public float JumpSpeed { get; private set; } = 8.0f;
        [field: SerializeField] public float GravityIntensity { get; private set; } = 20.0f;

        [Header("Animator Setup")]
        [SerializeField] private Animator animator;

        [Header("Skins Setup")]
        [Tooltip("The skins that will be used to distinguish the players are decided by the OwnerId")]
        [SerializeField] private Texture[] skinsTextures;
        [SerializeField] private Renderer playerRenderer;

        [Header("Camera Target Setup")]
        [Tooltip("This is the point that the client-side camera will follow.")]
        [SerializeField] private Transform cameraTarget;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private Vector3 currentVelocity = Vector3.zero;

        private CinemachineFreeLook freeLookCamera;
        private Camera mainCamera;
        private Vector3 lastLookDir;

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
            playerId.Value = OwnerId;
            playerRenderer.material.SetTexture(textureProperty, skinsTextures[playerId.Value % skinsTextures.Length]);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // The corresponding skin is chosen for the client's character.
            playerRenderer.material.SetTexture(textureProperty, skinsTextures[playerId.Value % skinsTextures.Length]);

            characterController.enabled = (base.IsServerStarted || base.IsOwner);

            if (!base.IsOwner) return;
            
            mainCamera = Camera.main;
            lastLookDir = transform.forward;
            freeLookCamera ??= FindObjectOfType<CinemachineFreeLook>();
            freeLookCamera.Follow = cameraTarget;
            freeLookCamera.LookAt = cameraTarget;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public override void OnStopClient()
        {
            if(!base.IsOwner) return;
            
            freeLookCamera.Follow = cameraTarget;
            freeLookCamera.LookAt = cameraTarget;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
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

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
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
                float strategySpeed = md.Running ? MoveSpeed * RUNNING_MULTIPLIER : MoveSpeed;
                Vector3 targetVelocity = md.MoveDirection * strategySpeed;

                // This section is responsible for reaching the desired speed gradually.
                currentVelocity = Vector3.MoveTowards(currentVelocity, targetVelocity, 5.0f);

                // It is analyzed if the magnitude of the movement to be made is greater than 0, with this we change the orientation of the player.
                if (currentVelocity.sqrMagnitude > 0)
                    lastLookDir = currentVelocity.normalized;

                moveDirection =  md.Attacking && characterController.isGrounded ? new Vector3(0, moveDirection.y, 0) : new Vector3(currentVelocity.x, moveDirection.y, currentVelocity.z);

                if (md.Jump && characterController.isGrounded)
                    moveDirection.y = JumpSpeed;

                if (!characterController.isGrounded)
                    moveDirection.y -= GravityIntensity * delta;

                transform.rotation = Quaternion.LookRotation(lastLookDir, Vector3.up);
                characterController.Move(moveDirection * delta);

                // The corresponding animations of the player are triggered, these are automatically synchronized thanks to the NetworkAnimator component.
                animator.SetBool(isGroundedHash, characterController.isGrounded);
                animator.SetFloat(velocityHash, md.Attacking ? 0 : currentVelocity.magnitude / MoveSpeed);
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
            int amount = Physics.OverlapSphereNonAlloc(transform.position + transform.up * 0.5f, 1f, colliders, AttackLayerMask);

            for (int i = 0; i < amount; i++)
            {
                if(colliders[i].TryGetComponent(out PlayerController other))
                    if (other.OwnerId != OwnerId)
                        NetworkManager.Log($"Hit Player: {other.OwnerId}");
            }
            
            // if (Physics.SphereCast(transform.position + transform.up * 0.5f, 1f, transform.forward, out RaycastHit hit, 1f))
            // {
            //     PlayerController other = hit.transform.GetComponent<PlayerController>();
            //     NetworkManager.Log($"Hit Player: {other.playerId.Value}");
            // }
            
            RollbackManager.Return();
        }
    }
}