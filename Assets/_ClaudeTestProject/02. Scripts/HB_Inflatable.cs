using UnityEngine;

namespace HappyBalloon
{
    /// <summary>펌프 건으로 부풀리거나(+1) 바람을 뺄(-1) 수 있는 물체의 공통 베이스.</summary>
    public abstract class HB_Inflatable : MonoBehaviour
    {
        [Range(0f, 1f)] public float amount;
        public string displayName = "풍선";
        public float inflateRate = 0.6f, deflateRate = 0.7f;

        /// <returns>실제로 무언가 변했으면 true</returns>
        public abstract bool Pump(float dir, float dt);
        public virtual string HintText() => "";
    }
}
