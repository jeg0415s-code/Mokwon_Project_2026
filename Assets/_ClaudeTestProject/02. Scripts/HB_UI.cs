using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace HappyBalloon
{
    /// <summary>
    /// 런타임에 생성되는 HUD (프롬프트, 목표, 자막, 쪽지, 페이드, 스태미나, 펌프 게이지, 엔딩 화면).
    /// </summary>
    public class HB_UI : MonoBehaviour
    {
        public static HB_UI I;

        Font font;
        Text prompt, objective, subtitle, toast, counter, noteTitle, noteBody, noteHint, endTitle, endSub, pauseText, pumpLabel;
        Image crosshair, fade, flash, staminaFill, pumpFill;
        CanvasGroup staminaGroup, pumpGroup, objectiveGroup, toastGroup, subtitleGroup;
        GameObject notePanel, endGroup, pauseGroup;
        RawImage noteImage;
        System.Action onNoteClosed;
        float subtitleT, toastT, flashA, objPulse;
        Color flashC = Color.red;

        public bool NoteOpen => notePanel != null && notePanel.activeSelf;
        public int NoteOpenedFrame { get; private set; } = -10;
        public int NoteClosedFrame { get; private set; } = -10;

        void Awake()
        {
            I = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Build();
        }

        // ------------------------------------------------------------------ build
        static Vector2 V(float x, float y) => new Vector2(x, y);

        RectTransform RT(Transform p, string n, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var g = new GameObject(n, typeof(RectTransform));
            g.transform.SetParent(p, false);
            var r = (RectTransform)g.transform;
            r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = V(0.5f, 0.5f);
            r.anchoredPosition = pos; r.sizeDelta = size;
            return r;
        }

        Image Img(Transform p, string n, Color c, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var i = RT(p, n, aMin, aMax, pos, size).gameObject.AddComponent<Image>();
            i.color = c; i.raycastTarget = false;
            return i;
        }

        Text Txt(Transform p, string n, int size, TextAnchor a, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 sz, bool shadow = true)
        {
            var r = RT(p, n, aMin, aMax, pos, sz);
            var t = r.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = a; t.color = Color.white; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true; t.lineSpacing = 1.15f;
            if (shadow) { var s = r.gameObject.AddComponent<Outline>(); s.effectDistance = V(2f, -2f); s.effectColor = new Color(0f, 0f, 0f, 0.85f); }
            return t;
        }

        void Build()
        {
            var cg = new GameObject("HB_Canvas");
            cg.transform.SetParent(transform, false);
            var canvas = cg.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
            var sc = cg.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = V(1920, 1080); sc.matchWidthOrHeight = 0.5f;
            var root = cg.transform;
            Vector2 C = V(0.5f, 0.5f);

            crosshair = Img(root, "Crosshair", new Color(1f, 1f, 1f, 0.75f), C, C, Vector2.zero, V(6, 6));
            prompt = Txt(root, "Prompt", 30, TextAnchor.UpperCenter, C, C, V(0, -46), V(1000, 60));
            prompt.color = new Color(1f, 0.92f, 0.7f);

            // pump gauge
            var pg = RT(root, "PumpGauge", C, C, V(0, -110), V(360, 60));
            pumpGroup = pg.gameObject.AddComponent<CanvasGroup>(); pumpGroup.alpha = 0f;
            Img(pg, "Bg", new Color(0f, 0f, 0f, 0.55f), C, C, V(0, -14), V(300, 12));
            pumpFill = Img(pg, "Fill", new Color(1f, 0.45f, 0.65f, 0.95f), V(0.5f, 0.5f), V(0.5f, 0.5f), V(0, -14), V(296, 8));
            pumpFill.rectTransform.pivot = V(0f, 0.5f); pumpFill.rectTransform.anchoredPosition = V(-148, -14);
            pumpLabel = Txt(pg, "Label", 22, TextAnchor.MiddleCenter, C, C, V(0, 12), V(700, 34));

            // objective (top-left)
            var og = RT(root, "Objective", V(0f, 1f), V(0f, 1f), V(340, -85), V(620, 130));
            objectiveGroup = og.gameObject.AddComponent<CanvasGroup>();
            objective = Txt(og, "Text", 26, TextAnchor.UpperLeft, V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
            objective.color = new Color(1f, 0.88f, 0.55f);

            counter = Txt(root, "Counter", 28, TextAnchor.UpperRight, V(1f, 1f), V(1f, 1f), V(-260, -60), V(460, 60));
            counter.color = new Color(1f, 0.6f, 0.75f);

            toast = Txt(root, "Toast", 30, TextAnchor.UpperCenter, V(0.5f, 1f), V(0.5f, 1f), V(0, -230), V(1400, 140));
            toast.color = new Color(1f, 0.75f, 0.85f, 1f);
            toastGroup = toast.gameObject.AddComponent<CanvasGroup>(); toastGroup.alpha = 0f;

            subtitle = Txt(root, "Subtitle", 36, TextAnchor.LowerCenter, V(0.5f, 0f), V(0.5f, 0f), V(0, 170), V(1500, 160));
            subtitleGroup = subtitle.gameObject.AddComponent<CanvasGroup>(); subtitleGroup.alpha = 0f;

            // stamina
            var sg = RT(root, "Stamina", V(0.5f, 0f), V(0.5f, 0f), V(0, 70), V(320, 14));
            staminaGroup = sg.gameObject.AddComponent<CanvasGroup>(); staminaGroup.alpha = 0f;
            Img(sg, "Bg", new Color(0f, 0f, 0f, 0.5f), V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
            staminaFill = Img(sg, "Fill", new Color(0.75f, 0.9f, 1f, 0.85f), V(0, 0), V(1, 1), Vector2.zero, V(-4, -4));

            flash = Img(root, "Flash", new Color(1f, 0f, 0f, 0f), V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);

            // note panel
            notePanel = Img(root, "NotePanel", new Color(0f, 0f, 0f, 0.72f), V(0, 0), V(1, 1), Vector2.zero, Vector2.zero).gameObject;
            var paper = Img(notePanel.transform, "Paper", new Color(0.93f, 0.89f, 0.8f, 1f), C, C, Vector2.zero, V(1040, 820));
            noteTitle = Txt(paper.transform, "Title", 40, TextAnchor.UpperCenter, V(0.5f, 1f), V(0.5f, 1f), V(0, -60), V(940, 60), false);
            noteTitle.color = new Color(0.35f, 0.08f, 0.12f);
            noteImage = RT(paper.transform, "Image", V(0.5f, 1f), V(0.5f, 1f), V(0, -120), V(520, 390)).gameObject.AddComponent<RawImage>();
            noteImage.rectTransform.pivot = V(0.5f, 1f); noteImage.rectTransform.anchoredPosition = V(0, -115);
            noteImage.raycastTarget = false;
            noteBody = Txt(paper.transform, "Body", 28, TextAnchor.UpperLeft, V(0.5f, 1f), V(0.5f, 1f), V(0, -130), V(920, 600), false);
            noteBody.rectTransform.pivot = V(0.5f, 1f);
            noteBody.color = new Color(0.15f, 0.12f, 0.12f);
            noteHint = Txt(paper.transform, "Hint", 22, TextAnchor.LowerCenter, V(0.5f, 0f), V(0.5f, 0f), V(0, 40), V(900, 40), false);
            noteHint.color = new Color(0.4f, 0.35f, 0.35f);
            noteHint.text = "[E] 또는 [클릭] 닫기";
            notePanel.SetActive(false);

            // pause
            pauseGroup = Img(root, "Pause", new Color(0f, 0f, 0f, 0.7f), V(0, 0), V(1, 1), Vector2.zero, Vector2.zero).gameObject;
            pauseText = Txt(pauseGroup.transform, "Text", 40, TextAnchor.MiddleCenter, V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);
            pauseText.text = "<size=70><color=#FF7A9A>일시정지</color></size>\n\n[ESC] 계속하기\n[R] 처음부터 다시\n[Q] 게임 종료\n\n<size=26><color=#AAAAAA>이동 WASD · 달리기 Shift · 점프 Space · 상호작용 E · 손전등 F\n펌프 건: 좌클릭(길게) 부풀리기 · 우클릭(길게) 바람 빼기</color></size>";
            pauseGroup.SetActive(false);

            fade = Img(root, "Fade", new Color(0f, 0f, 0f, 1f), V(0, 0), V(1, 1), Vector2.zero, Vector2.zero);

            // ending
            endGroup = RT(root, "End", V(0, 0), V(1, 1), Vector2.zero, Vector2.zero).gameObject;
            endTitle = Txt(endGroup.transform, "Title", 96, TextAnchor.MiddleCenter, C, C, V(0, 120), V(1800, 160));
            endTitle.color = new Color(1f, 0.4f, 0.55f);
            endSub = Txt(endGroup.transform, "Sub", 34, TextAnchor.UpperCenter, C, C, V(0, -60), V(1600, 400));
            endSub.rectTransform.pivot = V(0.5f, 1f); endSub.rectTransform.anchoredPosition = V(0, 20);
            endGroup.SetActive(false);
        }

        // ------------------------------------------------------------------ API
        public void SetPrompt(string s) { prompt.text = s ?? ""; }
        public void SetCrosshair(bool hot)
        {
            crosshair.color = hot ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(1f, 1f, 1f, 0.7f);
            crosshair.rectTransform.sizeDelta = hot ? V(10, 10) : V(6, 6);
        }

        public void SetObjective(string s)
        {
            objective.text = string.IsNullOrEmpty(s) ? "" : "<size=20><color=#FFFFFF99>◆ 목표</color></size>\n" + s;
            objPulse = 1f;
        }

        public void SetCounter(string s) { counter.text = s ?? ""; }

        public void Subtitle(string s, float dur = 3.8f)
        {
            subtitle.text = s; subtitleT = dur;
        }

        public void Toast(string s, float dur = 5f)
        {
            toast.text = s; toastT = dur;
        }

        public void ShowNote(string title, string body, Texture img, System.Action onClose = null)
        {
            noteTitle.text = title ?? "";
            noteBody.text = body ?? "";
            noteImage.texture = img;
            noteImage.gameObject.SetActive(img != null);
            noteBody.rectTransform.anchoredPosition = img != null ? V(0, -525) : V(0, -130);
            noteBody.fontSize = img != null ? 26 : 29;
            onNoteClosed = onClose;
            notePanel.SetActive(true);
            NoteOpenedFrame = Time.frameCount;
        }

        public void CloseNote()
        {
            if (!NoteOpen) return;
            notePanel.SetActive(false);
            NoteClosedFrame = Time.frameCount;
            var cb = onNoteClosed; onNoteClosed = null; cb?.Invoke();
        }

        public void SetFade(float a) { fade.color = new Color(0f, 0f, 0f, a); }
        public float FadeAlpha => fade.color.a;

        public IEnumerator FadeTo(float a, float dur)
        {
            float s = fade.color.a, t = 0f;
            while (t < dur) { t += Time.unscaledDeltaTime; SetFade(Mathf.Lerp(s, a, t / dur)); yield return null; }
            SetFade(a);
        }

        public void Flash(Color c, float a) { flashC = c; flashA = Mathf.Max(flashA, a); }

        public void SetStamina(float v, bool show)
        {
            staminaFill.rectTransform.anchorMax = V(Mathf.Clamp01(v), 1f);
            staminaFill.color = v < 0.25f ? new Color(1f, 0.45f, 0.4f, 0.9f) : new Color(0.75f, 0.9f, 1f, 0.85f);
            staminaGroup.alpha = Mathf.MoveTowards(staminaGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 3f);
        }

        public void SetPump(bool show, float v, string label)
        {
            pumpGroup.alpha = Mathf.MoveTowards(pumpGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 6f);
            if (show)
            {
                pumpFill.rectTransform.sizeDelta = V(296f * Mathf.Clamp01(v), 8f);
                pumpLabel.text = label;
            }
        }

        public void SetPause(bool on) { pauseGroup.SetActive(on); }

        public void ShowEnd(string title, string sub)
        {
            endGroup.SetActive(true);
            endTitle.text = title; endSub.text = sub;
            prompt.text = ""; objective.text = ""; counter.text = ""; toast.text = "";
        }

        public void HideHud(bool hide)
        {
            crosshair.enabled = !hide;
            objectiveGroup.alpha = hide ? 0f : 1f;
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            subtitleT -= dt; toastT -= dt;
            subtitleGroup.alpha = Mathf.Clamp01(subtitleT * 2f);
            toastGroup.alpha = Mathf.Clamp01(toastT * 1.5f);
            flashA = Mathf.MoveTowards(flashA, 0f, dt * 1.4f);
            flash.color = new Color(flashC.r, flashC.g, flashC.b, flashA);
            if (objPulse > 0f)
            {
                objPulse -= dt * 0.8f;
                objective.color = Color.Lerp(new Color(1f, 0.88f, 0.55f), Color.white, Mathf.Clamp01(objPulse));
            }
        }
    }
}
