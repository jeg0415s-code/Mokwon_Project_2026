using UnityEngine;

namespace HappyBalloon
{
    /// <summary>장식 풍선이 둥실둥실 떠다니며 흔들림.</summary>
    public class HB_Bob : MonoBehaviour
    {
        public float amplitude = 0.12f, speed = 1f, sway = 4f;
        Vector3 basePos; Quaternion baseRot; float phase;

        void Start()
        {
            basePos = transform.localPosition; baseRot = transform.localRotation;
            phase = (transform.position.x * 1.37f + transform.position.z * 0.71f) % 6.28f;
        }

        void Update()
        {
            float t = Time.time * speed + phase;
            transform.localPosition = basePos + new Vector3(Mathf.Sin(t * 0.7f) * amplitude * 0.4f, Mathf.Sin(t) * amplitude, Mathf.Cos(t * 0.6f) * amplitude * 0.4f);
            transform.localRotation = baseRot * Quaternion.Euler(Mathf.Sin(t * 0.9f) * sway, 0f, Mathf.Cos(t * 0.8f) * sway);
        }
    }
}
