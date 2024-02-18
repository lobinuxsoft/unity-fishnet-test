using System;
using Cinemachine;
using FishNet;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Object.Synchronizing;
using FishNet.Transporting;
using UnityEngine;

namespace CryingOnion.MultiplayerTest
{
    public class PlayerController : NetworkBehaviour
    {
        [field: Header("Base Setup")]
        [field: SerializeField]
        public float WalkingSpeed { get; private set; } = 7.5f;

        [field: SerializeField] public float RunningSpeed { get; private set; } = 11.5f;
        [field: SerializeField] public float JumpSpeed { get; private set; } = 8.0f;
        [field: SerializeField] public float GravityIntensity { get; private set; } = 20.0f;
        [field: SerializeField] public float LookSpeed { get; private set; } = 2.0f;
        [field: SerializeField] public float LookXLimit { get; private set; } = 45.0f;

        [Header("Animator Setup")] [SerializeField]
        private Animator animator;

        [Header("Skins Setup")] [SerializeField]
        private Texture[] skinsTextures;

        [SerializeField] private Renderer playerRenderer;

        [Header("Camera Target Setup")] [SerializeField]
        private Transform cameraTarget;

        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;

        private CinemachineFreeLook freeLookCamera;
        private Camera mainCamera;
        private Vector3 lastLookDir;

        private readonly int textureProperty = Shader.PropertyToID("_MainTex");


        private readonly SyncVar<int> playerId = new();

        public struct MoveData : IReplicateData
        {
            public Vector3 MoveDirection;
            public bool Jump;
            public bool Running;

            public MoveData(Vector3 moveDirection, bool jump, bool running)
            {
                MoveDirection = moveDirection;
                Jump = jump;
                Running = running;
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
            playerId.Value = OwnerId;
            lastLookDir = transform.forward;
            playerRenderer.material.SetTexture(textureProperty, skinsTextures[playerId.Value % skinsTextures.Length]);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            playerRenderer.material.SetTexture(textureProperty, skinsTextures[playerId.Value % skinsTextures.Length]);

            characterController.enabled = (base.IsServerStarted || base.IsOwner);

            if (base.IsOwner)
            {
                mainCamera = Camera.main;
                lastLookDir = transform.forward;
                freeLookCamera = FindObjectOfType<CinemachineFreeLook>();
                freeLookCamera.Follow = cameraTarget;
                freeLookCamera.LookAt = cameraTarget;
                
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
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

            if (mainCamera != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
                Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up);

                direction = right * Input.GetAxis("Horizontal") + forward * Input.GetAxis("Vertical");
            }

            md = new MoveData(direction, Input.GetButton("Jump"), Input.GetKey(KeyCode.LeftShift));
        }

        [Replicate]
        private void Move(MoveData md, bool asServer, Channel channel = Channel.Unreliable, bool replaying = false)
        {
            if (asServer)
            {
                float delta = (float)base.TimeManager.TickDelta;
                Vector3 movDir = md.MoveDirection * (md.Running ? RunningSpeed : WalkingSpeed);

                if (movDir.sqrMagnitude > 0)
                    lastLookDir = movDir.normalized;

                moveDirection = new Vector3(movDir.x, moveDirection.y, movDir.z);

                if (md.Jump && characterController.isGrounded)
                    moveDirection.y = JumpSpeed;

                if (!characterController.isGrounded)
                    moveDirection.y -= GravityIntensity * delta;

                transform.rotation = Quaternion.LookRotation(lastLookDir, Vector3.up);
                characterController.Move(moveDirection * delta);
            }
        }

        [Reconcile]
        private void Reconciliation(ReconcileData rd, bool asServer, Channel channel = Channel.Unreliable)
        {
            transform.position = rd.Position;
            transform.rotation = rd.Rotation;
        }
    }
}