using UnityEngine;

namespace HappyBalloon
{
    /// <summary>계속 회전 (태엽 열쇠, 회전목마, 모빌 등).</summary>
    public class HB_Spin : MonoBehaviour
    {
        public Vector3 axis = Vector3.up;
        public float speed = 30f;
        public bool onlyWhenEnabledFlag;
        void Update() { transform.Rotate(axis, speed * Time.deltaTime, Space.Self); }
    }
}
