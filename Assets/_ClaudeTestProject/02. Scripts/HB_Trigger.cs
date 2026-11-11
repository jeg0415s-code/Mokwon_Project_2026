using UnityEngine;

namespace HappyBalloon
{
    /// <summary>플레이어가 박스 영역에 들어오면 한 번 이벤트를 보낸다 (물리 트리거 대신 영역 검사).</summary>
    [RequireComponent(typeof(BoxCollider))]
    public class HB_Trigger : MonoBehaviour
    {
        public string eventId;
        public string requireFlag;
        public bool fired;
        BoxCollider box;

        void Awake() { box = GetComponent<BoxCollider>(); box.isTrigger = true; }

        void Update()
        {
            if (fired) return;
            var p = HB_PlayerController.I; var d = HB_GameDirector.I;
            if (p == null || d == null) return;
            if (!box.bounds.Contains(p.transform.position + Vector3.up * 0.9f)) return;
            if (!string.IsNullOrEmpty(requireFlag) && !d.HasFlag(requireFlag)) return;
            fired = true;
            d.OnEvent(eventId, this);
        }
    }
}
