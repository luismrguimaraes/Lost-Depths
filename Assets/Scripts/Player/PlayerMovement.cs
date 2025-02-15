using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class PlayerMovement : MonoBehaviour
{
    const double DOUBLE_MINIMUM_VALUE = 0.01;

    [SerializeField]
    [Range(0, 3)]
    [Tooltip("0: Concrete, 1: Grass, 2: Water, 3: Rock")]
    private int surfaceType;

    public Transform playerCamera;
    [SerializeField] bool cursorLock = true;
    [SerializeField] [Range(0.0f, 0.5f)] float mouseSmoothTime = 0.03f;
    [SerializeField] float mouseSensitivity;
    [SerializeField] float moveSpeed = 6.0f;
    [SerializeField] [Range(0.0f, 0.5f)] float moveSmoothTime = 0.3f;
    public float gravity = -10f;
    [SerializeField] Transform groundCheck; 
    [SerializeField] LayerMask ground;

    public float jumpHeight = 6f;
    Vector3 velocity;
    float velocityY;
    public readonly static float hVelMagMax = 7.5f;
    bool isGrounded;


    [SerializeField] float cameraCap;
    Vector2 currentMouseDelta;
    Vector2 currentMouseDeltaVelocity;

    Vector2 targetDir;
    Vector2 targetDirVelocity;

    CharacterController controller;
    Cloning cloningScript;
    Recorder recorder;

    Vector2 cameraInputValue;
    Vector2 moveInputValue;

    float cameraRotY;

    public InputActionAsset actions;
    public InputAction cameraInput, jumpButton, moveInput, gravButton;

    private Oxygen oxy;
    public float jetCost;
    public GameObject waterParticle;

    private string sceneName;
    private bool isWorld;
    private PlayerInventory inv;

    CanvasScript canvasScript;

    public FMODUnity.EventReference jumpEvent;
    public FMODUnity.EventReference landingEvent;
    public FMODUnity.EventReference jetpackEvent;

    private FootSteps footstepsScript;
    public FMODUnity.EventReference footstepsEvent;

    [SerializeField] private Animator anim;
    private bool hasGravved = false;

    void Start()
    {
        Application.targetFrameRate = 60;

        sceneName = SceneManager.GetActiveScene().name;
        isWorld = sceneName == "World";
        inv = GetComponent<PlayerInventory>();

        oxy = GetComponent<Oxygen>();
        cameraInput = actions.FindActionMap("movement", true).FindAction("camera", true);
        jumpButton = actions.FindActionMap("movement", true).FindAction("jump", true);
        gravButton = actions.FindActionMap("movement", true).FindAction("gravity", true);
        moveInput = actions.FindActionMap("movement", true).FindAction("move", true);
        actions.FindActionMap("movement").Enable();

        controller = GetComponent<CharacterController>();
        recorder = GetComponent<Recorder>();
        cloningScript = GetComponent<Cloning>();

        canvasScript = GameObject.FindGameObjectWithTag("UI Canvas").GetComponent<CanvasScript>();

        if (cursorLock)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        moveInputValue = new();

        footstepsScript = GetComponent<FootSteps>();
    }

    void Update()
    {
        if (gravity == 0)
            Debug.Log("GRAVITY IS ZEROOO");

        if (canvasScript.isPaused) return;
        UpdateMouse();
        UpdateMove();
    }

    void UpdateMouse()
    {
        cameraInputValue = cameraInput.ReadValue<Vector2>();

        currentMouseDelta = Vector2.SmoothDamp(currentMouseDelta, cameraInputValue, ref currentMouseDeltaVelocity, mouseSmoothTime);

        transform.Rotate(Vector3.up, currentMouseDelta.x * mouseSensitivity);

        cameraRotY += currentMouseDelta.y * mouseSensitivity;
        cameraRotY = Mathf.Clamp(cameraRotY, -cameraCap, cameraCap);
        playerCamera.localEulerAngles = (-Vector3.right * cameraRotY);
    }

    void UpdateMove()
    {
        var previousIsGrounded = isGrounded;
        isGrounded = Physics.CheckSphere(groundCheck.position, 0.2f, ground);

        if (isGrounded && !previousIsGrounded)
        {
            // Play landing sound
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Surface", footstepsScript.GetSurfaceType(sceneName));
            FMODUnity.RuntimeManager.PlayOneShotAttached(landingEvent, gameObject);

            if (recorder.isRecording) recorder.eventArray.Add(new PlayerEvent(PlayerEvent.EventType.JumpLanding, Time.time - recorder.GetRecordingStartTime()));

            hasGravved = false;
        }

        Vector2 newMoveInputValue;

        newMoveInputValue = moveInput.ReadValue<Vector2>();
        newMoveInputValue.Normalize();

        moveInputValue = newMoveInputValue;

        targetDir = Vector2.SmoothDamp(targetDir, moveInputValue, ref targetDirVelocity, moveSmoothTime);

        if (!isGrounded)
            velocityY += gravity * Time.deltaTime;

        velocity = (transform.forward * targetDir.y + transform.right * targetDir.x) * moveSpeed + Vector3.up * velocityY;
        controller.Move(velocity * Time.deltaTime);
        double currentHVelMag = Math.Sqrt(Math.Pow(controller.velocity.x, 2) + Math.Pow(controller.velocity.z, 2));
        FMODUnity.RuntimeManager.StudioSystem.setParameterByName("MoveSpeed", (float)currentHVelMag / hVelMagMax);

        if (currentHVelMag <= DOUBLE_MINIMUM_VALUE)
            anim.SetFloat("horspeed", 0.01f, 0.1f, Time.deltaTime);
        else anim.SetFloat("horspeed", (float)currentHVelMag / hVelMagMax, 0.1f, Time.deltaTime);

        // JUMP
        if (jumpButton.WasPressedThisFrame() && (isGrounded || (!isGrounded && isWorld && inv.HasItem("Jetpack"))))
        {
            int mul = 1;
            if (!isGrounded)
            {
                mul = 3;
                oxy.oxygenValue -= jetCost;
                FMODUnity.RuntimeManager.PlayOneShotAttached(jetpackEvent, gameObject);
                JetPackParticles();
            }
            velocityY = Jump(jumpHeight * mul);
        }
        // GRAVITY Switch
        if (gravButton.WasPressedThisFrame() && !isWorld && inv.HasItem("Gravity Boot") && !hasGravved)
        {
            hasGravved = true;
            gravity *= -1;

            transform.Rotate(Vector3.forward, 180f);
            cameraRotY *= -1;
            playerCamera.localEulerAngles = (-Vector3.right * cameraRotY);

            velocityY = 0f;
        }
    }

    public float Jump(float height)
    {
        FMODUnity.RuntimeManager.PlayOneShotAttached(jumpEvent, gameObject);
        if (recorder.isRecording) recorder.eventArray.Add(new PlayerEvent(PlayerEvent.EventType.Jump, Time.time - recorder.GetRecordingStartTime()));

        float velocityY = Mathf.Sqrt(height * Mathf.Abs(gravity)) * -Mathf.Sign(gravity);
        return velocityY;
    }

    private void JetPackParticles()
    {
        for (int i = 0; i < 30; i++)
        {
            Instantiate(waterParticle, transform.position, Quaternion.identity);
        }
    }

    public void CallFootsteps()
    {
        double currentHVelMag = Math.Sqrt(Math.Pow(controller.velocity.x, 2) + Math.Pow(controller.velocity.z, 2));

        if (isGrounded && currentHVelMag > DOUBLE_MINIMUM_VALUE)
        {
            FMODUnity.RuntimeManager.StudioSystem.setParameterByName("Surface", footstepsScript.GetSurfaceType(SceneManager.GetActiveScene().name));
            FMODUnity.RuntimeManager.PlayOneShotAttached(footstepsEvent, gameObject);

            if (recorder.isRecording) recorder.eventArray.Add(new PlayerEvent(PlayerEvent.EventType.FootstepsSound, Time.time - recorder.GetRecordingStartTime()));
            // ToDo: save footsteps parameters (maybe using a dictionary is the best choice for this)
        }
    }

    public void ResetMovement()
    {
        velocity = Vector3.zero;
        targetDir = Vector3.zero;
        moveInputValue = Vector2.zero;
        velocityY = 0f;
    }
}