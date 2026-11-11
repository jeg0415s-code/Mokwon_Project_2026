using UnityEngine;

namespace HappyBalloon
{
    /// <summary>위치/회전 오프셋으로 열리고 닫히는 문 (미닫이, 셔터, 철문 모두 이걸로).</summary>
    public class HB_Door : MonoBehaviour
    {
        public Vector3 openPosOffset;
        public Vector3 openEulerOffset;
        public float duration = 1.6f;
        public bool isOpen;
        public string moveSound = "door";
        public Renderer[] indicators;
        public Color lockedColor = new Color(1f, 0.1f, 0.08f), openColor = new Color(0.2f, 1f, 0.35f);

        Vector3 closedPos; Quaternion closedRot; float t;
        MaterialPropertyBlock mpb;

        void Awake()
        {
            closedPos = transform.localPosition; closedRot = transform.localRotation;
            t = isOpen ? 1f : 0f; Apply(); SetIndicator();
        }

        public void Open() { if (isOpen) return; isOpen = true; Sound(); SetIndicator(); }
        public void Close() { if (!isOpen) return; isOpen = false; Sound(); SetIndicator(); }
        public void SetInstant(bool open) { isOpen = open; t = open ? 1f : 0f; Apply(); SetIndicator(); }

        void Sound()
        {
            if (!string.IsNullOrEmpty(moveSound)) HB_Audio.I?.PlayAt(moveSound, transform.position, 1f, 1f, 40f);
        }

        void SetIndicator()
        {
            if (indicators == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            var c = isOpen ? openColor : lockedColor;
            foreach (var r in indicators)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", c); mpb.SetColor("_EmissionColor", c * 3f);
                r.SetPropertyBlock(mpb);
            }
        }

        void Update()
        {
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(t, target)) return;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            Apply();
        }

        void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, t);
            transform.localPosition = closedPos + openPosOffset * e;
            transform.localRotation = closedRot * Quaternion.Euler(openEulerOffset * e);
        }
    }
}
