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
        [field: SerializeField] public float Gravity { get; private set; } = 20.0f;
        [field: SerializeField] public float LookSpeed { get; private set; } = 2.0f;
        [field: SerializeField] public float LookXLimit { get; private set; } = 45.0f;
        
        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private float rotationX = 0;

        [HideInInspector] public bool canMove = true;

        [SerializeField] private float cameraYOffset = .4f;
        private Camera ownerCamera;

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
                ownerCamera = Camera.main;
                ownerCamera.transform.position = new Vector3(transform.position.x, transform.position.y + cameraYOffset,
                    transform.position.z);
                ownerCamera.transform.SetParent(transform);
            }
            else
            {
                gameObject.GetComponent<PlayerController>().enabled = false;
            }
        }

        private void Start()
        {
            characterController = GetComponent<CharacterController>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            bool isRunning = Input.GetKey(KeyCode.LeftShift);

            Vector3 forward = transform.TransformDirection(Vector3.forward);
            Vector3 right = transform.TransformDirection(Vector3.right);

            float curSpeedX = canMove ? (isRunning ? RunningSpeed : WalkingSpeed) * Input.GetAxis("Vertical") : 0;
            float curSpeedY = canMove ? (isRunning ? RunningSpeed : WalkingSpeed) * Input.GetAxis("Horizontal") : 0;

            float movementDirectionY = moveDirection.y;
            moveDirection = (forward * curSpeedX) + (right * curSpeedY);

            if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
                moveDirection.y = JumpSpeed;
            else
                moveDirection.y = movementDirectionY;

            if (!characterController.isGrounded)
                moveDirection.y -= Gravity * Time.deltaTime;

            characterController.Move(moveDirection * Time.deltaTime);

            if (canMove && ownerCamera != null)
            {
                rotationX -= Input.GetAxis("Mouse Y") * LookSpeed;
                rotationX = Mathf.Clamp(rotationX, -LookXLimit, LookXLimit);
                ownerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
                transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * LookSpeed, 0);
            }
        }
    }
}