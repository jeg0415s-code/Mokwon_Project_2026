using UnityEngine;

namespace HappyBalloon
{
    /// <summary>
    /// 월드 TextMesh 가 벽을 뚫고 보이지 않도록 깊이 테스트 셰이더(HappyBalloon/WorldText)를 쓰고,
    /// 동적 폰트 텍스처를 머티리얼에 연결한다. boost 로 네온처럼 밝게 만들 수 있다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(TextMesh))]
    public class HB_TextFix : MonoBehaviour
    {
        public float boost = 1f;
        TextMesh tm; MeshRenderer mr; MaterialPropertyBlock mpb;

        void OnEnable()
        {
            tm = GetComponent<TextMesh>(); mr = GetComponent<MeshRenderer>();
            Font.textureRebuilt += OnRebuilt;
            Apply();
        }

        void OnDisable() { Font.textureRebuilt -= OnRebuilt; }
        void OnRebuilt(Font f)
        {
            if (tm == null || f != tm.font) return;
            var s = tm.text; tm.text = ""; tm.text = s;   // 폰트 텍스처가 다시 만들어지면 글자 UV 를 새로 생성
            Apply();
        }

        public void SetBoost(float b) { boost = b; Apply(); }

        void Apply()
        {
            if (tm == null || mr == null || tm.font == null) return;
            if (mpb == null) mpb = new MaterialPropertyBlock();
            mr.GetPropertyBlock(mpb);
            var tex = tm.font.material != null ? tm.font.material.mainTexture : null;
            if (tex != null) mpb.SetTexture("_MainTex", tex);
            mpb.SetFloat("_Boost", boost);
            mr.SetPropertyBlock(mpb);
        }
    }
}
