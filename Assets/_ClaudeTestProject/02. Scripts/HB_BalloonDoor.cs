using UnityEngine;

namespace HappyBalloon
{
    /// <summary>바람을 빼면(우클릭) 쪼그라들어 길이 열리는 풍선 문 / 풍선 장애물.</summary>
    public class HB_BalloonDoor : HB_Inflatable
    {
        public string eventId;
        public Transform visual;
        public Collider blocker;
        public bool opened;
        Vector3 baseScale, basePos; float lastPump = -9f;

        void Start()
        {
            amount = 1f;
            if (visual != null) { baseScale = visual.localScale; basePos = visual.localPosition; }
        }

        public override bool Pump(float dir, float dt)
        {
            if (opened) return false;
            lastPump = Time.time;
            if (dir > 0f)
            {
                if (amount >= 1f) return false;
                amount = Mathf.Min(1f, amount + inflateRate * dt);
                return true;
            }
            amount = Mathf.Max(0f, amount - deflateRate * dt);
            if (amount <= 0.02f) Open();
            return true;
        }

        void Open()
        {
            opened = true; amount = 0f;
            if (blocker != null) blocker.enabled = false;
            HB_Audio.I?.PlayAt("deflate_end", transform.position, 1f, 1f, 30f);
            HB_GameDirector.I?.OnEvent(eventId, this);
        }

        void Update()
        {
            if (visual == null) return;
            float a = opened ? 0f : amount;
            bool pumping = Time.time - lastPump < 0.15f;
            float wob = pumping ? Mathf.Sin(Time.time * 30f) * 0.03f : Mathf.Sin(Time.time * 1.3f) * 0.01f;
            float sx = Mathf.Lerp(0.55f, 1f, a) + wob;
            float sy = Mathf.Lerp(0.07f, 1f, a) - wob;
            float sz = Mathf.Lerp(0.9f, 1f, a) + wob;
            var target = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z * sz);
            visual.localScale = Vector3.Lerp(visual.localScale, target, Time.deltaTime * 12f);
            visual.localPosition = new Vector3(basePos.x, basePos.y * (visual.localScale.y / Mathf.Max(0.0001f, baseScale.y)), basePos.z);
        }

        public override string HintText() => opened ? "" : "  [우클릭] 바람 빼기";
    }
}
