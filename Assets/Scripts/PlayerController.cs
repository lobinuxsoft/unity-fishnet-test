using Cinemachine;
using FishNet.Object;
using UnityEngine;

namespace CryingOnion.MultiplayerTest
{
    public class PlayerController : NetworkBehaviour
    {
        [field: Header("Base setup")]
        [field: SerializeField] public float WalkingSpeed { get; private set; } = 7.5f;

        [field: SerializeField] public float RunningSpeed { get; private set; } = 11.5f;
        [field: SerializeField] public float JumpSpeed { get; private set; } = 8.0f;
        [field: SerializeField] public float GravityIntensity { get; private set; } = 20.0f;
        [field: SerializeField] public float LookSpeed { get; private set; } = 2.0f;
        [field: SerializeField] public float LookXLimit { get; private set; } = 45.0f;
        
        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;

        private CinemachineFreeLook freeLookCamera;
        private Camera mainCamera;
        private Vector3 lastLookDir;

        [Header("Animator Setup")]
        [SerializeField] private Animator animator;

        [Header("Visual identification Setting")]
        [SerializeField] private Gradient colors;
        [SerializeField] private Renderer playerRenderer;
        private readonly int colorProperty = Shader.PropertyToID("_Color");

        public override void OnStartClient()
        {
            base.OnStartClient();

            playerRenderer.material.SetColor(colorProperty, colors.Evaluate(ObjectId / 10.0f));
            
            if (base.IsOwner)
            {
                mainCamera = Camera.main;
                freeLookCamera = FindObjectOfType<CinemachineFreeLook>();
                freeLookCamera.Follow = transform;
                freeLookCamera.LookAt = transform;
            }
            else
            {
                gameObject.GetComponent<PlayerController>().enabled = false;
            }
        }

        private void Start()
        {
            mainCamera = Camera.main;
            characterController = GetComponent<CharacterController>();
            lastLookDir = transform.forward;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            bool isRunning = Input.GetKey(KeyCode.LeftShift);
            
            if (mainCamera != null)
            {
                Vector3 forward = Vector3.ProjectOnPlane(mainCamera.transform.forward, Vector3.up);
                Vector3 right = Vector3.ProjectOnPlane(mainCamera.transform.right, Vector3.up);

                float curSpeedX = (isRunning ? RunningSpeed : WalkingSpeed) * Input.GetAxis("Horizontal");
                float curSpeedY = (isRunning ? RunningSpeed : WalkingSpeed) * Input.GetAxis("Vertical");

                Vector3 direction = right * curSpeedX + forward * curSpeedY;
                
                if (direction.sqrMagnitude > 0)
                    lastLookDir = direction.normalized;
                
                moveDirection = new Vector3(direction.x, moveDirection.y, direction.z);
            }

            if (Input.GetButton("Jump") && characterController.isGrounded)
                moveDirection.y = JumpSpeed;

            if (!characterController.isGrounded)
                moveDirection.y -= GravityIntensity * Time.deltaTime;
            
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(lastLookDir), 10.0f);
            characterController.Move(moveDirection * Time.deltaTime);
        }
    }
}