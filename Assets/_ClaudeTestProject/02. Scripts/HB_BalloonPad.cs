using UnityEngine;

namespace HappyBalloon
{
    /// <summary>
    /// 풍선 발판: 부풀리면 풍선이 커지며 발판이 떠오른다 (플레이어를 태우고 2층까지).
    /// 펌프를 멈추면 잠시 뒤 바람이 새며 천천히 내려온다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class HB_BalloonPad : HB_Inflatable
    {
        public float lowY = 0.3f, highY = 7f;
        public float riseSpeed = 1.5f, fallSpeed = 2.4f, leakSpeed = 0.45f;
        public float leakDelay = 1.4f, holdAtTop = 6f;
        public Transform[] balloons;
        public Transform[] strings;
        public bool reachedTop;

        Vector3[] balloonScale, balloonPos, stringScale;
        float lastPump = -99f, topTime;
        AudioSource creak;

        void Start()
        {
            balloonScale = new Vector3[balloons.Length]; balloonPos = new Vector3[balloons.Length];
            for (int i = 0; i < balloons.Length; i++) { balloonScale[i] = balloons[i].localScale; balloonPos[i] = balloons[i].localPosition; }
            stringScale = new Vector3[strings.Length];
            for (int i = 0; i < strings.Length; i++) stringScale[i] = strings[i].localScale;
            var p = transform.position; p.y = lowY; transform.position = p;
        }

        public override bool Pump(float dir, float dt)
        {
            lastPump = Time.time;
            var p = transform.position;
            if (dir > 0f)
            {
                if (p.y >= highY - 0.001f) return false;
                p.y = Mathf.Min(highY, p.y + riseSpeed * dt);
            }
            else
            {
                if (p.y <= lowY + 0.001f) return false;
                p.y = Mathf.Max(lowY, p.y - fallSpeed * dt);
            }
            transform.position = p;
            return true;
        }

        void Update()
        {
            var p = transform.position;
            amount = Mathf.InverseLerp(lowY, highY, p.y);
            bool atTop = p.y >= highY - 0.01f;
            if (atTop)
            {
                topTime += Time.deltaTime;
                if (!reachedTop) { reachedTop = true; HB_GameDirector.I?.OnEvent("pad_top", this); }
            }
            else topTime = 0f;

            float since = Time.time - lastPump;
            bool leak = atTop ? (topTime > holdAtTop && since > leakDelay) : since > leakDelay;
            if (leak && p.y > lowY)
            {
                p.y = Mathf.Max(lowY, p.y - leakSpeed * Time.deltaTime);
                transform.position = p;
            }

            // 풍선 크기 = 부푼 정도, 줄 길이 보정
            float s = Mathf.Lerp(0.25f, 1f, Mathf.Sqrt(Mathf.Clamp01(amount * 1.4f + 0.02f)));
            float t = Time.time;
            for (int i = 0; i < balloons.Length; i++)
            {
                var sway = new Vector3(Mathf.Sin(t * 1.1f + i) * 0.08f, Mathf.Sin(t * 1.7f + i * 2f) * 0.05f, Mathf.Cos(t * 0.9f + i) * 0.08f);
                balloons[i].localScale = balloonScale[i] * s;
                balloons[i].localPosition = Vector3.Lerp(new Vector3(balloonPos[i].x, 0.5f, balloonPos[i].z), balloonPos[i], s) + sway * s;
            }
            for (int i = 0; i < strings.Length; i++)
                strings[i].localScale = new Vector3(stringScale[i].x, stringScale[i].y * s, stringScale[i].z);
        }

        public override string HintText()
        {
            if (amount >= 0.999f) return "  (최대로 떠올랐다)";
            return "  [좌클릭] 띄우기";
        }
    }
}
