using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HappyBalloon
{
    /// <summary>
    /// 해피벌룬 랜드 전체 진행(스토리 이벤트, 목표, 체크포인트, 사망/리스폰, 연출, 엔딩)을 관리한다.
    /// </summary>
    public class HB_GameDirector : MonoBehaviour
    {
        public static HB_GameDirector I;

        [Header("Player")]
        public HB_PlayerController player;
        public HB_PumpGun pump;

        [Header("01 Entrance")]
        public HB_Door gateLeft, gateRight;
        public Rigidbody[] gateChain;
        public Collider gateBlocker;

        [Header("02 Main Hall")]
        public GameObject statue;
        public GameObject statueGoneProps;
        public GameObject ventSkin;
        public Transform ventPoint;
        public GameObject lightsEmergency;
        public GameObject lightsPower;
        public Transform lever;
        public HB_Door mazeDoor;
        public HB_TextFix[] neonSigns;
        public HB_TextFix neonFlicker;
        public GameObject pumpGlow;

        [Header("03 Maze")]
        public HB_Door mazeExitDoor;
        public GameObject[] drawingSlots;
        public Transform giggleRoomPoint;
        public Light[] giggleRoomLights;
        public GameObject giggleBalloon;

        [Header("04 Practice / Chase")]
        public HB_Chaser doll;
        public HB_Door practiceExitDoor;
        public HB_Chaser bubble;
        public HB_BreakableWall burstWall;
        public Transform bubbleSpawn;
        public Transform chaseCheckpoint;
        public GameObject lightsPractice;
        public GameObject lightsChase;
        public HB_Door shutter;

        [Header("05 Finale")]
        public HB_FloatingBalloon endBalloon;
        public GameObject lightsFinale;
        public Light endSpot;

        [Header("FX")]
        public Volume fxVolume;

        public bool Paused { get; private set; }

        readonly HashSet<string> flags = new HashSet<string>();
        int drawings, deaths, bubbleStuckCount;
        bool busy, ended, chaseActive;
        Vector3 cpPos; float cpYaw;
        Vignette vig; ChromaticAberration chroma; ColorAdjustments color;
        HB_UI ui; HB_Audio au;

        public bool HasFlag(string f) => flags.Contains(f);
        void Flag(string f) { flags.Add(f); }

        // ------------------------------------------------------------------ setup
        void Awake()
        {
            I = this;
            au = gameObject.AddComponent<HB_Audio>();
            ui = gameObject.AddComponent<HB_UI>();
            Time.timeScale = 1f; AudioListener.pause = false;
        }

        void Start()
        {
            if (fxVolume != null && fxVolume.profile != null)
            {
                fxVolume.profile.TryGet(out vig);
                fxVolume.profile.TryGet(out chroma);
                fxVolume.profile.TryGet(out color);
            }
            cpPos = player.transform.position; cpYaw = player.transform.eulerAngles.y;

            SetActive(lightsPower, false); SetActive(lightsChase, false); SetActive(lightsFinale, false);
            SetActive(lightsEmergency, true); SetActive(lightsPractice, true);
            SetActive(statue, true); SetActive(statueGoneProps, false); SetActive(ventSkin, false);
            foreach (var s in drawingSlots) SetActive(s, false);
            foreach (var n in neonSigns) if (n != null) n.SetBoost(0.18f);
            if (neonFlicker != null) neonFlicker.SetBoost(0.18f);
            if (bubble != null) bubble.gameObject.SetActive(false);
            if (endBalloon != null) endBalloon.armed = false;
            if (endSpot != null) endSpot.enabled = false;
            RenderSettings.fogDensity = 0.03f;

            au.SetAmbient("wind", 0.45f);
            ui.SetFade(1f);
            StartCoroutine(Intro());
        }

        static void SetActive(GameObject g, bool on) { if (g != null) g.SetActive(on); }

        IEnumerator Intro()
        {
            player.locked = true;
            yield return new WaitForSeconds(0.4f);
            yield return ui.FadeTo(0f, 2.5f);
            ui.Subtitle("...여기가 해피벌룬 랜드. 10년 만이네.", 3.5f);
            yield return new WaitForSeconds(3f);
            au.Play2D("page", 0.8f);
            ShowNote("구겨진 편지",
                "민호에게.\n\n폐장한 지 10년이 지났는데도, 밤마다 해피벌룬 랜드에서 풍선 터지는 소리가 들린단다.\n" +
                "그날 사라진 아이들... 그리고 <b>버블</b>.\n\n" +
                "부탁이다. <b>2층 관리실의 전원</b>을 다시 켜 주렴.\n불이 켜져 있는 동안엔 그 아이도 얌전해질 거야.\n\n" +
                "정문 열쇠는 필요 없을 거다. 이미 누군가 열어 두었으니까.\n\n" +
                "                                                — 수석 정비사 한경수",
                null, () =>
                {
                    ui.SetObjective("폐쇄된 정문으로 들어가기");
                    ui.Toast("이동 <b>WASD</b>  ·  달리기 <b>Shift</b>  ·  점프 <b>Space</b>  ·  상호작용 <b>E</b>  ·  손전등 <b>F</b>", 7f);
                });
        }

        void ShowNote(string title, string body, Texture img, System.Action onClose = null)
        {
            player.locked = true;
            ui.ShowNote(title, body, img, () => { if (!busy) player.locked = false; onClose?.Invoke(); });
        }

        // ------------------------------------------------------------------ update
        void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;

            if (ended)
            {
                if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
                if (kb != null && kb.escapeKey.wasPressedThisFrame) Quit();
                return;
            }

            if (kb != null && kb.escapeKey.wasPressedThisFrame) SetPaused(!Paused);
            if (Paused)
            {
                if (kb != null && kb.rKey.wasPressedThisFrame) Restart();
                if (kb != null && kb.qKey.wasPressedThisFrame) Quit();
                return;
            }
            if (Cursor.lockState != CursorLockMode.Locked && mouse != null && mouse.leftButton.wasPressedThisFrame)
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            if (ui.NoteOpen && Time.frameCount > ui.NoteOpenedFrame)
            {
                if ((kb != null && kb.eKey.wasPressedThisFrame) || (mouse != null && mouse.leftButton.wasPressedThisFrame))
                { au.Play2D("page", 0.6f); ui.CloseNote(); }
            }

            UpdateDanger();
        }

        void UpdateDanger()
        {
            float danger = 0f;
            if (chaseActive && bubble != null && bubble.gameObject.activeInHierarchy && bubble.state != HB_Chaser.State.Frozen)
                danger = 1f - Mathf.Clamp01((bubble.DistanceToPlayer - 2f) / 20f);
            au.SetHeartbeat(chaseActive ? Mathf.Max(0.25f, danger) : 0f);
            if (vig != null) vig.intensity.value = Mathf.Lerp(vig.intensity.value, 0.33f + danger * 0.27f, Time.deltaTime * 3f);
            if (chroma != null) chroma.intensity.value = Mathf.Lerp(chroma.intensity.value, 0.12f + danger * 0.6f, Time.deltaTime * 3f);
            if (player.flashlight != null && danger > 0.65f && Random.value < 0.06f) StartCoroutine(FlashlightBlink(0.06f));
        }

        IEnumerator FlashlightBlink(float t)
        {
            var f = player.flashlight; if (f == null || !f.enabled) yield break;
            f.enabled = false; yield return new WaitForSeconds(t); f.enabled = true;
        }

        void SetPaused(bool p)
        {
            Paused = p;
            Time.timeScale = p ? 0f : 1f;
            AudioListener.pause = p;
            ui.SetPause(p);
            Cursor.lockState = p ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = p;
        }

        void Restart()
        {
            Time.timeScale = 1f; AudioListener.pause = false;
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(gameObject.scene.path,
                new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
#else
            UnityEngine.SceneManagement.SceneManager.LoadScene(gameObject.scene.buildIndex);
#endif
        }

        void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ------------------------------------------------------------------ events
        public void OnInteract(HB_Interactable it)
        {
            if (!string.IsNullOrEmpty(it.noteBody) || !string.IsNullOrEmpty(it.noteTitle))
                ShowNote(it.noteTitle, it.noteBody, it.noteImage);
            if (!string.IsNullOrEmpty(it.eventId)) OnEvent(it.eventId, it);
        }

        public void OnEvent(string id, Object src = null)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id.StartsWith("drawing")) { OnDrawing(); return; }

            switch (id)
            {
                case "gate": StartCoroutine(GateSeq()); break;

                case "enter_hall":
                    Flag("hall");
                    RenderSettings.fogDensity = 0.012f;
                    au.SetAmbient("drone", 0.5f);
                    StartCoroutine(Lines(0.5f,
                        ("...메인 홀. 매표소도, 버블 동상도 10년 전 그대로야.", 3.8f),
                        ("관리실은 2층이었지. 전원부터 켜야 해.", 3.5f)));
                    ui.SetObjective("2층 관리실로 올라가 전원 켜기");
                    break;

                case "stairs_broken":
                    Flag("stairs");
                    au.PlayAt("creak", player.transform.position + player.transform.forward * 3f, 0.8f, 0.7f);
                    ui.Subtitle("계단이 무너져 있어... 이대로는 못 올라가.", 3.5f);
                    if (!HasFlag("pump"))
                    {
                        ui.SetObjective("올라갈 방법 찾기  <size=20>(매표소에 불이 켜져 있다)</size>");
                        SetActive(pumpGlow, true);
                    }
                    break;

                case "pump":
                    Flag("pump");
                    pump.Equip();
                    au.Play2D("pickup", 0.8f);
                    SetActive(pumpGlow, false);
                    ui.Toast("<b>펌프 건</b> 획득!\n<b>[좌클릭 길게]</b> 부풀리기    <b>[우클릭 길게]</b> 바람 빼기", 8f);
                    ui.Subtitle("이걸로 계단 옆 풍선 발판을 띄울 수 있겠어.", 3.5f);
                    ui.SetObjective("계단 옆 풍선 발판에 올라 발판을 부풀려 2층으로");
                    break;

                case "pad_top":
                    if (!HasFlag("pad_top")) { Flag("pad_top"); ui.Toast("발판에서 내려 관리실로", 3f); }
                    break;

                case "enter_control":
                    if (HasFlag("power")) break;
                    ui.Subtitle("관리실... 저기 메인 전원 레버가 있어.", 3f);
                    ui.SetObjective("레버를 당겨 해피벌룬 랜드의 전원 켜기");
                    break;

                case "lever": StartCoroutine(PowerOnSeq()); break;

                case "maze_locked":
                    if (!HasFlag("power")) ui.Subtitle("잠겨 있어. 전원이 들어와야 열릴 것 같아.", 2.5f);
                    break;

                case "hall_after_power":
                    StartCoroutine(Lines(0.2f,
                        ("<color=#FF8FA8>...동상이 없어.</color>", 3f),
                        ("받침대만 남았어. 누가 옮긴 거지...?", 3.5f)));
                    au.Play2D("sting", 0.7f);
                    ui.SetObjective("미로 놀이방으로 가기  <size=20>(메인 홀 안쪽 왼편)</size>");
                    break;

                case "vent":
                    au.PlayAt("scrape", ventPoint != null ? ventPoint.position : player.transform.position, 1f, 0.9f, 30f);
                    ui.Subtitle("풍선 껍질이... 환풍구 안으로 빨려 들어가 있어.", 4f);
                    break;

                case "enter_maze":
                    Flag("maze");
                    RenderSettings.fogDensity = 0.016f;
                    ui.SetObjective("흩어진 그림 3장 모으기");
                    ui.SetCounter("그림 0 / 3");
                    ui.Subtitle("미로 놀이방... 아이들이 제일 좋아하던 곳이었지.", 3.5f);
                    ui.Toast("부풀어 있는 <b>풍선 문</b>은 <b>[우클릭 길게]</b>로 바람을 빼서 열 수 있다", 6f);
                    StartCoroutine(MazeAmbience());
                    break;

                case "bdoor_1": ui.Subtitle("푸시이익... 풍선이 쪼그라들었다.", 2.5f); break;
                case "bdoor_2": ui.Subtitle("길이 열렸어.", 2f); break;
                case "blocker": ui.Subtitle("통로를 막고 있던 풍선 덩어리가 꺼졌다.", 2.5f); break;

                case "giggle_room": StartCoroutine(GiggleRoom()); break;

                case "enter_practice": StartCoroutine(PracticeSeq()); break;

                case "doll_push":
                    if (!HasFlag("doll_push"))
                    {
                        Flag("doll_push");
                        ui.Toast("인형에게 닿으면 밀려난다. <color=#FF6070>버블이라면... 끝이다.</color>", 4f);
                    }
                    break;

                case "chase_start": StartCoroutine(ChaseStartSeq()); break;
                case "chaser_free":
                    if (src == bubble) ui.Subtitle("<color=#FF6070>빠져나왔어!</color>", 1.5f);
                    break;
                case "shutter": StartCoroutine(ShutterSeq()); break;
                case "enter_finale": StartCoroutine(FinaleSeq()); break;
            }
        }

        IEnumerator Lines(float delay, params (string, float)[] lines)
        {
            yield return new WaitForSeconds(delay);
            foreach (var l in lines) { ui.Subtitle(l.Item1, l.Item2); yield return new WaitForSeconds(l.Item2 + 0.3f); }
        }

        // ------------------------------------------------------------------ 01 gate
        IEnumerator GateSeq()
        {
            Flag("gate");
            if (gateChain != null)
                foreach (var rb in gateChain) if (rb != null) { rb.isKinematic = false; rb.AddForce(Random.insideUnitSphere * 0.5f, ForceMode.Impulse); }
            au.PlayAt("chain", gateLeft.transform.position, 1f);
            ui.Subtitle("쇠사슬이... 이미 끊어져 있어.", 3f);
            yield return new WaitForSeconds(1.2f);
            if (gateBlocker != null) gateBlocker.enabled = false;
            au.PlayAt("creak", gateLeft.transform.position, 1f, 0.85f, 50f);
            gateLeft.Open(); gateRight.Open();
            ui.SetObjective("해피벌룬 랜드 안으로 들어가기");
        }

        // ------------------------------------------------------------------ 02 power
        IEnumerator PowerOnSeq()
        {
            busy = true;
            player.locked = true;
            if (lever != null)
            {
                var start = lever.localRotation; var end = start * Quaternion.Euler(-80f, 0f, 0f);
                for (float t = 0f; t < 1f; t += Time.deltaTime / 0.6f) { lever.localRotation = Quaternion.Slerp(start, end, Mathf.SmoothStep(0f, 1f, t)); yield return null; }
                lever.localRotation = end;
            }
            au.Play2D("clunk", 1f);
            player.Shake(0.4f);
            yield return new WaitForSeconds(0.5f);

            // 완전 암전
            bool flashOn = player.flashlight != null && player.flashlight.enabled;
            if (player.flashlight != null) player.flashlight.enabled = false;
            SetActive(lightsEmergency, false);
            au.Play2D("hum_on", 1f);
            au.SetAmbient("drone", 0.15f);
            yield return new WaitForSeconds(1.6f);

            // 동상이 사라지고, 불이 들어온다
            SetActive(statue, false);
            SetActive(statueGoneProps, true);
            SetActive(ventSkin, true);
            Flag("power");
            SetActive(lightsPower, true);
            if (lightsPower != null) yield return StartCoroutine(StaggerLights(lightsPower));
            foreach (var n in neonSigns) if (n != null) n.SetBoost(3f);
            if (neonFlicker != null) StartCoroutine(NeonFlicker(neonFlicker));
            if (player.flashlight != null) player.flashlight.enabled = flashOn;
            RenderSettings.ambientLight = new Color(0.09f, 0.08f, 0.11f);
            if (mazeDoor != null) mazeDoor.Open();
            au.SetMusic("musicbox", 0.22f);
            au.SetAmbient("drone", 0.4f);
            player.locked = false;
            busy = false;

            ui.Subtitle("전원이 들어왔다. 음악이... 아직도 나오네.", 3.5f);
            yield return new WaitForSeconds(3.8f);
            if (ventPoint != null) au.PlayAt("scrape", ventPoint.position, 1f, 0.8f, 80f);
            ui.Subtitle("<color=#FF8FA8>...방금, 아래층에서 뭔가 기어가는 소리가.</color>", 3.5f);
            ui.SetObjective("메인 홀로 돌아가기");
        }

        IEnumerator StaggerLights(GameObject root)
        {
            var lights = root.GetComponentsInChildren<Light>(true);
            var targets = new float[lights.Length];
            for (int i = 0; i < lights.Length; i++) { targets[i] = lights[i].intensity; lights[i].intensity = 0f; }
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].intensity = targets[i];
                if (i % 3 == 0) { au.Play2D("click", 0.35f, Random.Range(0.7f, 1.2f)); yield return new WaitForSeconds(0.06f); }
            }
        }

        IEnumerator NeonFlicker(HB_TextFix n)
        {
            while (n != null)
            {
                n.SetBoost(Random.value < 0.25f ? 0.15f : 3f);
                yield return new WaitForSeconds(Random.Range(0.04f, 0.5f));
            }
        }

        // ------------------------------------------------------------------ 03 maze
        void OnDrawing()
        {
            drawings = Mathf.Min(3, drawings + 1);
            au.Play2D("pickup", 0.7f);
            if (drawingSlots != null && drawings - 1 < drawingSlots.Length) SetActive(drawingSlots[drawings - 1], true);
            ui.SetCounter("그림 " + drawings + " / 3");
            if (drawings >= 3 && !HasFlag("drawings"))
            {
                Flag("drawings");
                StartCoroutine(AfterDrawings());
            }
        }

        IEnumerator AfterDrawings()
        {
            while (ui.NoteOpen) yield return null;
            mazeExitDoor.Open();
            au.PlayAt("windup", mazeExitDoor.transform.position + Vector3.forward * 6f, 1f, 0.8f, 60f);
            ui.Subtitle("출구가 열렸어... 그 너머에서 태엽 감는 소리가 들려.", 4f);
            ui.SetObjective("그림판 옆 출구로 나가기");
            ui.SetCounter("");
        }

        IEnumerator MazeAmbience()
        {
            while (!HasFlag("practice"))
            {
                yield return new WaitForSeconds(Random.Range(14f, 24f));
                if (HasFlag("practice")) break;
                var pos = player.transform.position + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 9f;
                au.PlayAt(Random.value < 0.5f ? "giggle" : "squeak", pos, 0.55f, Random.Range(0.75f, 0.95f), 25f);
            }
        }

        IEnumerator GiggleRoom()
        {
            var pos = giggleRoomPoint != null ? giggleRoomPoint.position : player.transform.position;
            au.PlayAt("giggle", pos, 1f, 0.8f, 30f);
            if (giggleRoomLights != null) foreach (var l in giggleRoomLights) if (l != null) l.enabled = false;
            yield return new WaitForSeconds(0.25f);
            if (giggleBalloon != null)
            {
                // 풍선 하나가 천천히 위로 날아가 버린다
                var t = giggleBalloon.transform; var bob = giggleBalloon.GetComponent<HB_Bob>(); if (bob != null) bob.enabled = false;
                for (float k = 0f; k < 4f; k += Time.deltaTime) { t.position += Vector3.up * Time.deltaTime * 1.6f; yield return null; }
                giggleBalloon.SetActive(false);
            }
            if (giggleRoomLights != null) foreach (var l in giggleRoomLights) if (l != null) l.enabled = true;
            ui.Subtitle("...누구 있어?", 2.5f);
        }

        // ------------------------------------------------------------------ 04 practice & chase
        IEnumerator PracticeSeq()
        {
            Flag("practice");
            mazeExitDoor.Close();
            RenderSettings.fogDensity = 0.02f;
            au.SetMusic(null, 0f);
            ui.Subtitle("태엽 인형 작업실...? 문이 닫혔어!", 3f);
            yield return new WaitForSeconds(1.8f);
            au.PlayAt("windup", doll.transform.position, 1f, 1f, 40f);
            yield return new WaitForSeconds(1.3f);
            doll.Activate();
            ui.Subtitle("<color=#FFB060>인형이... 움직인다.</color>", 2.5f);
            ui.SetObjective("다가오는 태엽 인형을 <color=#FFA040>좁은 문틀</color>로 유인해서\n문틀 안에 있을 때 <b>[좌클릭]</b>으로 부풀려 끼우기");
            ui.Toast("작업 일지를 떠올려: <b>좁은 문틀 안</b>에서 부풀리면 몸이 끼어 빠져나오지 못한다", 7f);
        }

        public void OnChaserStuck(HB_Chaser c, HB_Chokepoint where)
        {
            if (c == doll && !HasFlag("practice_done"))
            {
                Flag("practice_done");
                StartCoroutine(PracticeDone());
            }
            else if (c == bubble)
            {
                bubbleStuckCount++;
                player.Shake(0.5f);
                ui.Toast(bubbleStuckCount == 1 ? "<color=#FFA040>버블이 문틀에 끼었다!</color>  지금 도망쳐!" : "<color=#FFA040>끼었다!</color>", 2.5f);
            }
        }

        IEnumerator PracticeDone()
        {
            yield return new WaitForSeconds(0.6f);
            ui.Subtitle("끼었어...! 이 방법이라면 버블한테서도 도망칠 수 있어.", 3.8f);
            yield return new WaitForSeconds(1.2f);
            practiceExitDoor.Open();
            ui.SetObjective("작업실 출구로 나가기");
        }

        IEnumerator ChaseStartSeq()
        {
            Flag("chase");
            busy = true;
            cpPos = chaseCheckpoint.position; cpYaw = chaseCheckpoint.eulerAngles.y;
            au.SetMusic(null, 0f);
            au.SetAmbient("drone", 0f);
            yield return new WaitForSeconds(0.3f);

            // 정적 → 뒤쪽 벽을 쾅쾅 두드림
            au.PlayAt("bang", burstWall.transform.position, 1f, 0.7f, 60f); player.Shake(0.3f);
            yield return new WaitForSeconds(0.8f);
            au.PlayAt("bang", burstWall.transform.position, 1f, 0.6f, 60f); player.Shake(0.45f);
            ui.Subtitle("...뒤에서?", 1.2f);
            yield return new WaitForSeconds(0.7f);

            SetActive(lightsPractice, false);
            SetActive(lightsChase, true);
            burstWall.Break(burstWall.transform.position - burstWall.transform.forward * 2f + Vector3.up, 18f);
            au.Play2D("sting", 1f);
            player.Shake(1f);
            ui.Flash(new Color(1f, 0.1f, 0.1f), 0.45f);
            bubble.ResetTo(bubbleSpawn.position, bubbleSpawn.eulerAngles.y, false);
            yield return new WaitForSeconds(0.5f);
            au.PlayAt("scream", bubble.transform.position, 0.9f, 0.7f, 80f);
            yield return new WaitForSeconds(0.6f);
            bubble.Activate();
            chaseActive = true;
            busy = false;
            RenderSettings.fogDensity = 0.03f;
            au.SetMusic("chase", 0.55f);
            ui.Subtitle("<size=52><color=#FF3040>도망쳐!</color></size>", 2.5f);
            ui.SetObjective("<color=#FF6070>버블에게서 도망치기</color>\n좁은 문틀에서 돌아서서 <b>[좌클릭]</b>으로 끼워 시간을 벌어라");
        }

        public void PlayerCaught(HB_Chaser c)
        {
            if (busy || ended || !chaseActive) return;
            StartCoroutine(CaughtSeq(c));
        }

        IEnumerator CaughtSeq(HB_Chaser c)
        {
            busy = true; chaseActive = false; deaths++;
            player.locked = true;
            c.Freeze();
            c.FaceInstant(player.cam.transform.position);
            player.ForceLook(c.head != null ? c.head : c.transform);
            float torch = player.flashlight != null ? player.flashlight.intensity : 0f;
            if (player.flashlight != null) player.flashlight.intensity = torch * 0.12f;   // 얼굴이 하얗게 날아가지 않게
            au.SetMusic(null, 0f); au.StopMusicNow();
            au.Play2D("scream", 1f, 0.9f);
            au.Play2D("pop", 0.8f, 0.5f);
            ui.Flash(new Color(0.8f, 0f, 0f), 0.85f);
            player.Shake(1.2f);
            ui.HideHud(true);
            yield return new WaitForSeconds(0.9f);
            ui.SetFade(1f);
            au.SetHeartbeat(0f);
            ui.Subtitle("<color=#FF3040>버블에게 잡혔다...</color>", 2.5f);
            yield return new WaitForSeconds(2.2f);

            // 리스폰
            player.ForceLook(null);
            if (player.flashlight != null) player.flashlight.intensity = torch;
            player.Teleport(cpPos, cpYaw);
            player.RefillStamina();
            foreach (var ch in HB_Chokepoint.All) ch.SetStuck(false);
            bubble.ResetTo(bubbleSpawn.position, bubbleSpawn.eulerAngles.y, false);
            if (shutter != null) shutter.SetInstant(true);
            ui.HideHud(false);
            yield return ui.FadeTo(0f, 1f);
            player.locked = false;
            if (deaths >= 2) ui.Toast("팁: <b>Shift</b>로 달려서 거리를 벌리고, 문틀을 지난 뒤 <b>돌아서서</b> 버블이 문틀 안에 들어오면 <b>[좌클릭]</b>", 7f);
            ui.Subtitle("...온다.", 1.5f);
            yield return new WaitForSeconds(1.6f);
            au.PlayAt("scream", bubble.transform.position, 0.6f, 0.6f, 60f);
            bubble.Activate();
            chaseActive = true;
            au.SetMusic("chase", 0.55f);
            busy = false;
        }

        IEnumerator ShutterSeq()
        {
            Flag("shutter");
            shutter.Close();
            chaseActive = false;
            yield return new WaitForSeconds(0.45f);
            au.PlayAt("slam", shutter.transform.position, 1f, 1f, 60f);
            player.Shake(0.6f);
            if (bubble != null && bubble.gameObject.activeSelf)
            {
                bubble.Freeze();
                bubble.gameObject.SetActive(false);
            }
            au.SetMusic(null, 0f);
            yield return new WaitForSeconds(1.2f);
            for (int i = 0; i < 3; i++)
            {
                au.PlayAt("bang", shutter.transform.position, 1f, 0.8f - i * 0.08f, 50f);
                player.Shake(0.35f);
                yield return new WaitForSeconds(0.9f + i * 0.4f);
            }
            au.PlayAt("squeak", shutter.transform.position, 0.6f, 0.5f, 40f);
            ui.Subtitle("...따돌렸어?", 2.5f);
            yield return new WaitForSeconds(2.5f);
            au.SetHeartbeat(0f);
            au.SetAmbient("drone", 0.35f);
            if (!HasFlag("finale")) ui.SetObjective("앞으로 나아가기");
        }

        // ------------------------------------------------------------------ 05 finale
        IEnumerator FinaleSeq()
        {
            Flag("finale");
            RenderSettings.fogDensity = 0.018f;
            SetActive(lightsFinale, true);
            if (endSpot != null) endSpot.enabled = true;
            au.PlayAt("click", endSpot != null ? endSpot.transform.position : player.transform.position, 1f, 0.6f, 60f);
            ui.SetObjective("");
            yield return new WaitForSeconds(1f);
            ui.Subtitle("버블 극장... 무대 위에 풍선 하나가 떠 있어.", 3.5f);
            if (endBalloon != null) endBalloon.armed = true;
        }

        public void OnEndingBalloonPop(HB_FloatingBalloon b)
        {
            if (ended) return;
            StartCoroutine(EndingSeq(b));
        }

        IEnumerator EndingSeq(HB_FloatingBalloon b)
        {
            ended = true; busy = true;
            player.locked = true;
            player.ForceLook(b.transform);
            yield return new WaitForSeconds(0.6f);
            b.Pop();
            ui.Flash(Color.white, 0.6f);
            player.Shake(0.5f);
            au.SetMusic(null, 0f); au.SetAmbient("drone", 0f); au.SetHeartbeat(0f);
            SetActive(lightsFinale, false);
            if (endSpot != null) endSpot.enabled = false;
            if (player.flashlight != null) player.flashlight.enabled = false;
            yield return new WaitForSeconds(1.6f);
            au.Play2D("giggle", 1f, 0.75f);
            ui.Subtitle("<size=54><color=#FF5A6E>찾았다.</color></size>", 3f);
            yield return new WaitForSeconds(2.6f);
            yield return ui.FadeTo(1f, 1.5f);
            ui.HideHud(true);
            ui.ShowEnd("HAPPY BALLOON LAND",
                "<color=#CCCCCC>버블은 아직 터지지 않았다...</color>\n\n\n<size=26><color=#888888>플레이해 주셔서 감사합니다\n\n[R] 처음부터     [ESC] 종료</color></size>");
            au.SetMusic("musicbox", 0.3f);
        }
    }
}
