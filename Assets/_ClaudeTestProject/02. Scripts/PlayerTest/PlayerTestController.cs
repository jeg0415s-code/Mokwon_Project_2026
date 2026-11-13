using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 테스트용 1인칭 플레이어: 기본 이동(WASD), 점프(Space), 마우스 카메라 회전만 담당.
///
/// Level Design 씬의 Player 구조에 맞춤
///   Player (발 위치가 피벗, 이 스크립트를 붙이는 곳)
///   └─ Main Camera (눈 높이, 상하 회전만 담당)
///
/// - 좌우 회전(Yaw)은 Player 본체를, 상하 회전(Pitch)은 카메라를 돌린다.
/// - CharacterController 가 없으면 자동으로 추가되고, 카메라 높이에 맞춰 크기가 설정된다.
/// - 게임 시작 시 마우스 커서를 잠그고, Esc 로 풀고, 화면을 클릭하면 다시 잠근다.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerTestController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("상하 회전시킬 카메라 Transform. 비워두면 자식 Camera 를 자동으로 찾는다.")]
    public Transform cameraTransform;

    [Header("Move")]
    [Tooltip("걷기 속도 (m/s)")]
    public float moveSpeed = 4f;
    [Tooltip("땅에서 속도가 목표값에 도달하는 빠르기. 클수록 즉각적")]
    public float groundAcceleration = 14f;
    [Tooltip("공중에서의 방향 전환 정도 (0 = 불가, 1 = 땅과 동일)")]
    [Range(0f, 1f)] public float airControl = 0.35f;

    [Header("Jump / Gravity")]
    [Tooltip("점프 높이 (m)")]
    public float jumpHeight = 1.1f;
    [Tooltip("중력 가속도 (음수)")]
    public float gravity = -20f;
    [Tooltip("땅에서 막 떨어진 뒤에도 점프를 허용하는 시간 (초)")]
    public float coyoteTime = 0.1f;
    [Tooltip("착지 직전에 누른 점프를 기억하는 시간 (초)")]
    public float jumpBufferTime = 0.1f;

    [Header("Look")]
    [Tooltip("마우스 감도 (마우스 이동 1픽셀당 회전 각도)")]
    public float mouseSensitivity = 0.1f;
    [Tooltip("상하 회전 제한 각도")]
    [Range(0f, 89f)] public float pitchLimit = 85f;
    public bool invertY = false;

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float yaw;
    float pitch;
    float lastGroundedTime = -1f;
    float lastJumpPressedTime = -1f;

    // 스크립트를 처음 붙였을 때 (또는 Reset 했을 때) CharacterController 를 Player 구조에 맞게 설정
    void Reset()
    {
        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null)
        {
            float eyeHeight = cameraTransform != null ? cameraTransform.localPosition.y : 1.6f;
            float height = Mathf.Max(1.0f, eyeHeight + 0.2f);   // 머리 위 약간의 여유
            cc.height = height;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, height * 0.5f, 0f);     // 피벗이 발 위치가 되도록
            cc.stepOffset = Mathf.Min(0.3f, height * 0.3f);
            cc.slopeLimit = 45f;
            cc.skinWidth = 0.03f;
        }
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraTransform = cam.transform;
        }

        yaw = transform.eulerAngles.y;
        if (cameraTransform != null)
        {
            pitch = cameraTransform.localEulerAngles.x;
            if (pitch > 180f) pitch -= 360f;
        }
    }

    void OnEnable() => LockCursor(true);

    void OnDisable() => LockCursor(false);

    void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        UpdateCursorLock(keyboard, mouse);
        UpdateLook(mouse);
        UpdateMovement(keyboard);
    }

    // ---------------------------------------------------------------- Cursor
    void UpdateCursorLock(Keyboard keyboard, Mouse mouse)
    {
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            LockCursor(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            LockCursor(true);
    }

    static void LockCursor(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    // ---------------------------------------------------------------- Look
    void UpdateLook(Mouse mouse)
    {
        if (mouse == null || Cursor.lockState != CursorLockMode.Locked) return;

        // 마우스 델타는 이미 프레임당 이동량이므로 deltaTime 을 곱하지 않는다
        Vector2 delta = mouse.delta.ReadValue() * mouseSensitivity;

        yaw += delta.x;
        pitch += invertY ? delta.y : -delta.y;
        pitch = Mathf.Clamp(pitch, -pitchLimit, pitchLimit);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (cameraTransform != null)
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    // ---------------------------------------------------------------- Move / Jump
    void UpdateMovement(Keyboard keyboard)
    {
        float dt = Time.deltaTime;

        // 입력 (WASD + 방향키)
        Vector2 input = Vector2.zero;
        bool jumpPressed = false;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            jumpPressed = keyboard.spaceKey.wasPressedThisFrame;
        }
        input = Vector2.ClampMagnitude(input, 1f);   // 대각선이 더 빠르지 않도록

        bool grounded = controller.isGrounded;
        if (grounded) lastGroundedTime = Time.time;
        if (jumpPressed) lastJumpPressedTime = Time.time;

        // 수평 이동: 플레이어가 바라보는 방향 기준
        Vector3 wishDir = transform.right * input.x + transform.forward * input.y;
        Vector3 targetVelocity = wishDir * moveSpeed;
        float accel = groundAcceleration * (grounded ? 1f : airControl);
        horizontalVelocity = Vector3.MoveTowards(horizontalVelocity, targetVelocity, accel * moveSpeed * dt);

        // 땅에 붙어 있게 하는 작은 하강 속도 (경사로/계단에서 떠오르지 않도록)
        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        // 점프 (코요테 타임 + 입력 버퍼)
        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool wantsJump = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (canJump && wantsJump)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = -1f;
            lastJumpPressedTime = -1f;
        }

        verticalVelocity += gravity * dt;

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(velocity * dt);

        // 천장에 머리를 부딪히면 상승 중단
        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f;
    }
}
