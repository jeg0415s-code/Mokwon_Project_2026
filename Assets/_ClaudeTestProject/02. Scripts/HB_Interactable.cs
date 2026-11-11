using UnityEngine;

namespace HappyBalloon
{
    /// <summary>[E]로 사용할 수 있는 물체. eventId 로 GameDirector 에 알리고, 쪽지 내용이 있으면 쪽지를 띄운다.</summary>
    public class HB_Interactable : MonoBehaviour
    {
        public string prompt = "조사하기";
        public string eventId;
        public bool oneShot = true;
        public bool interactable = true;
        public bool hideOnUse;
        public string useSound = "page";
        [Header("쪽지 (선택)")]
        public string noteTitle;
        [TextArea(3, 12)] public string noteBody;
        public Texture noteImage;

        public void Use()
        {
            if (!interactable) return;
            if (oneShot) interactable = false;
            if (!string.IsNullOrEmpty(useSound)) HB_Audio.I?.Play2D(useSound, 0.8f);
            HB_GameDirector.I?.OnInteract(this);
            if (hideOnUse) gameObject.SetActive(false);
        }
    }
}
