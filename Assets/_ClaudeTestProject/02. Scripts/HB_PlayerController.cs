using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyBalloon
{
    /// <summary>
    /// 1인칭 플레이어: 이동/달리기(스태미나)/점프, 마우스 시점, 손전등, E 상호작용, 움직이는 발판 탑승.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HB_PlayerController : MonoBehaviour
    {
        public static HB_PlayerController I;

        public Camera cam;
        public Light flashlight;
        public float walkSpeed = 3.4f, sprintSpeed = 6.3f, jumpHeight = 1.05f, gravity = -22f;
        public float mouseSensitivity = 0.11f;
        public float staminaMax = 6.5f, staminaRegen = 1.25f;
        public float interactDistance = 3.4f;

        [HideInInspector] public bool locked;
        public bool IsSprinting { get; private set; }
        public float Stamina01 => stamina / staminaMax;

        CharacterController cc;
        float yaw, pitch, vy, stamina, shake, bobT, stepDist, camBaseY;
        bool exhausted, groundValid;
        Vector3 impulse, lastPos, groundPrev;
        Transform ground, lookTarget;
        HB_Interactable focus;

        void Awake()
        {
            I = this;
            cc = GetComponent<CharacterController>();
            stamina = staminaMax;
            yaw = transform.eulerAngles.y;
            if (cam == null) cam = GetComponentInChildren<Camera>();
            camBaseY = cam.transform.localPosition.y;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            lastPos = transform.position;
        }

        public void Teleport(Vector3 pos, float rotY)
        {
            cc.enabled = false; transform.position = pos; cc.enabled = true;
            yaw = rotY; pitch = 0f; vy = 0f; impulse = Vector3.zero; ground = null; groundValid = false;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            lastPos = pos;
        }

        public void AddImpulse(Vector3 v) { impulse += v; }
        public void Shake(float a) { shake = Mathf.Max(shake, a); }
        public void ForceLook(Transform t) { lookTarget = t; }
        public void RefillStamina() { stamina = staminaMax; exhausted = false; }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            var dir = HB_GameDirector.I;
            if (dir != null && dir.Paused) return;
            var kb = Keyboard.current; var mouse = Mouse.current;

            // 움직이는 발판(풍선 발판)에 탑승
            if (ground != null && groundValid)
            {
                var d = ground.position - groundPrev;
                if (d.sqrMagnitude > 0f && d.sqrMagnitude < 1f) cc.Move(d);
            }

            // 시점
            if (!locked && mouse != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var md = mouse.delta.ReadValue();
                yaw += md.x * mouseSensitivity;
                pitch = Mathf.Clamp(pitch - md.y * mouseSensitivity, -85f, 85f);
            }
            if (lookTarget != null)
            {
                var to = lookTarget.position - cam.transform.position;
                if (to.sqrMagnitude > 0.001f)
                {
                    var e = Quaternion.LookRotation(to).eulerAngles;
                    float tp = e.x > 180f ? e.x - 360f : e.x;
                    yaw = Mathf.LerpAngle(yaw, e.y, dt * 14f);
                    pitch = Mathf.Lerp(pitch, tp, dt * 14f);
                }
            }
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            // 이동
            Vector2 mv = Vector2.zero; bool wantSprint = false, jump = false;
            if (!locked && kb != null)
            {
                if (kb.wKey.isPressed) mv.y += 1f;
                if (kb.sKey.isPressed) mv.y -= 1f;
                if (kb.dKey.isPressed) mv.x += 1f;
                if (kb.aKey.isPressed) mv.x -= 1f;
                wantSprint = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                jump = kb.spaceKey.wasPressedThisFrame;
            }
            mv = Vector2.ClampMagnitude(mv, 1f);
            bool moving = mv.sqrMagnitude > 0.01f;
            IsSprinting = wantSprint && moving && !exhausted && mv.y > -0.1f;
            if (IsSprinting)
            {
                stamina -= dt;
                if (stamina <= 0f) { stamina = 0f; exhausted = true; }
            }
            else
            {
                stamina = Mathf.Min(staminaMax, stamina + staminaRegen * dt * (moving ? 0.7f : 1f));
                if (exhausted && stamina > staminaMax * 0.35f) exhausted = false;
            }
            float speed = IsSprinting ? sprintSpeed : walkSpeed * (exhausted ? 0.8f : 1f);
            Vector3 wish = (transform.right * mv.x + transform.forward * mv.y) * speed;

            if (cc.isGrounded)
            {
                if (vy < 0f) vy = -2f;
                if (jump) { vy = Mathf.Sqrt(-2f * gravity * jumpHeight); ground = null; }
            }
            vy += gravity * dt;
            impulse = Vector3.MoveTowards(impulse, Vector3.zero, dt * 14f);

            groundValid = false;
            var flags = cc.Move((wish + impulse + Vector3.up * vy) * dt);
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;
            if (!groundValid) ground = null;
            if (ground != null) groundPrev = ground.position;

            // 발소리
            var hv = transform.position - lastPos; hv.y = 0f; lastPos = transform.position;
            if (cc.isGrounded && moving)
            {
                stepDist += hv.magnitude;
                float stride = IsSprinting ? 2.4f : 1.9f;
                if (stepDist > stride)
                {
                    stepDist = 0f;
                    HB_Audio.I?.Play2D("step", IsSprinting ? 0.5f : 0.3f, Random.Range(0.85f, 1.12f));
                }
            }

            // 헤드밥 + 카메라 흔들림
            float bobAmt = (cc.isGrounded && moving) ? (IsSprinting ? 0.065f : 0.035f) : 0f;
            if (moving) bobT += dt * (IsSprinting ? 13f : 9f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.6f);
            var sh = Random.insideUnitSphere * shake * 0.07f;
            cam.transform.localPosition = new Vector3(0f, camBaseY + Mathf.Sin(bobT) * bobAmt, 0f) + sh;
            cam.transform.localRotation = Quaternion.Euler(pitch + sh.y * 25f, 0f, Mathf.Sin(bobT * 0.5f) * bobAmt * 10f + sh.x * 25f);

            // 손전등
            if (!locked && kb != null && kb.fKey.wasPressedThisFrame && flashlight != null)
            {
                flashlight.enabled = !flashlight.enabled;
                HB_Audio.I?.Play2D("click", 0.6f);
            }

            UpdateInteract(kb);

            var ui = HB_UI.I;
            if (ui != null) ui.SetStamina(Stamina01, stamina < staminaMax - 0.01f);
            HB_Audio.I?.SetBreath(exhausted ? 0.8f : (1f - Stamina01) * 0.3f);
        }

        void UpdateInteract(Keyboard kb)
        {
            HB_Interactable hitI = null;
            var ui = HB_UI.I;
            bool blocked = locked || (ui != null && (ui.NoteOpen || Time.frameCount - ui.NoteClosedFrame <= 1));
            if (!blocked && Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, interactDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                hitI = hit.collider.GetComponentInParent<HB_Interactable>();
                if (hitI != null && !hitI.interactable) hitI = null;
            }
            focus = hitI;
            if (ui != null)
            {
                ui.SetPrompt(focus != null ? "[E] " + focus.prompt : "");
                ui.SetCrosshair(focus != null);
            }
            if (focus != null && kb != null && kb.eKey.wasPressedThisFrame) focus.Use();
        }

        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.normal.y > 0.6f)
            {
                if (ground != hit.transform) { ground = hit.transform; groundPrev = ground.position; }
                groundValid = true;
            }
        }
    }
}
