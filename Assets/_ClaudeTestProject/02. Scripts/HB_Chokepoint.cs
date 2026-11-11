using System.Collections.Generic;
using UnityEngine;

namespace HappyBalloon
{
    /// <summary>좁은 문틀(초크포인트). 이 영역 안에서 부풀려진 추격자는 끼어서 움직이지 못한다.</summary>
    public class HB_Chokepoint : MonoBehaviour
    {
        public static readonly List<HB_Chokepoint> All = new List<HB_Chokepoint>();
        public Vector3 size = new Vector3(1.6f, 3f, 2.6f);
        public Renderer[] frame;
        public Light glow;
        public bool stuck;

        MaterialPropertyBlock mpb;
        Color baseColor = new Color(1f, 0.5f, 0.1f);

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public bool Contains(Vector3 w)
        {
            var l = transform.InverseTransformPoint(w);
            return Mathf.Abs(l.x) <= size.x * 0.5f && l.y >= -0.6f && l.y <= size.y && Mathf.Abs(l.z) <= size.z * 0.5f;
        }

        public void SetStuck(bool s) { stuck = s; }

        void Update()
        {
            float k = stuck ? 2.5f + Mathf.Sin(Time.time * 18f) * 1.5f : 0.9f + Mathf.Sin(Time.time * 3f) * 0.5f;
            if (glow != null) glow.intensity = stuck ? 3.5f : 1.4f + Mathf.Sin(Time.time * 3f) * 0.4f;
            if (frame == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            var c = stuck ? new Color(1f, 0.25f, 0.1f) : baseColor;
            foreach (var r in frame)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb); mpb.SetColor("_EmissionColor", c * k); r.SetPropertyBlock(mpb);
            }
        }

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(new Vector3(0f, size.y * 0.5f, 0f), size);
        }
    }
}
