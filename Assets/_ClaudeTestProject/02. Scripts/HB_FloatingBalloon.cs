using UnityEngine;

namespace HappyBalloon
{
    /// <summary>엔딩 방의 풍선. 플레이어가 일정 시간 바라보면 터진다.</summary>
    public class HB_FloatingBalloon : MonoBehaviour
    {
        public bool armed;
        public float lookTime = 1.4f, maxDistance = 13f, maxAngle = 16f;
        public float bobAmp = 0.15f;
        public GameObject[] hideOnPop;
        public bool popped;
        Vector3 basePos; float seen;

        void Start() { basePos = transform.position; }

        void Update()
        {
            float t = Time.time;
            transform.position = basePos + new Vector3(Mathf.Sin(t * 0.7f) * 0.1f, Mathf.Sin(t * 1.1f) * bobAmp, Mathf.Cos(t * 0.5f) * 0.08f);
            transform.rotation = Quaternion.Euler(Mathf.Sin(t * 0.9f) * 5f, t * 12f, Mathf.Cos(t * 0.8f) * 5f);
            if (!armed || popped) return;
            var p = HB_PlayerController.I; if (p == null) return;
            var cam = p.cam.transform;
            var to = transform.position - cam.position; float d = to.magnitude;
            bool looking = d < maxDistance && Vector3.Angle(cam.forward, to) < maxAngle &&
                           !Physics.Linecast(cam.position, transform.position - to.normalized * 0.8f, ~0, QueryTriggerInteraction.Ignore);
            seen = looking ? seen + Time.deltaTime : Mathf.Max(0f, seen - Time.deltaTime * 0.5f);
            if (seen >= lookTime) { popped = true; HB_GameDirector.I?.OnEndingBalloonPop(this); }
        }

        public void Pop()
        {
            if (hideOnPop != null) foreach (var g in hideOnPop) if (g != null) g.SetActive(false);
            HB_Audio.I?.PlayAt("pop", transform.position, 1f, 0.9f, 60f);
        }
    }
}
