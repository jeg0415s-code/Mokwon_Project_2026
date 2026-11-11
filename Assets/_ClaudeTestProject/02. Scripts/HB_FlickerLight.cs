using UnityEngine;

namespace HappyBalloon
{
    /// <summary>불안정하게 깜빡이는 조명. 같이 깜빡일 발광 렌더러를 지정할 수 있다.</summary>
    [RequireComponent(typeof(Light))]
    public class HB_FlickerLight : MonoBehaviour
    {
        public float offChance = 0.18f;
        public float minInterval = 0.04f, maxInterval = 0.3f;
        public Renderer bulb;
        Light l; float baseIntensity, t;
        MaterialPropertyBlock mpb; Color bulbColor;

        void Awake()
        {
            l = GetComponent<Light>(); baseIntensity = l.intensity;
            if (bulb != null)
            {
                mpb = new MaterialPropertyBlock();
                var m = bulb.sharedMaterial;
                bulbColor = m != null && m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.white;
            }
        }

        void Update()
        {
            t -= Time.deltaTime;
            if (t > 0f) return;
            t = Random.Range(minInterval, maxInterval);
            bool off = Random.value < offChance;
            float k = off ? Random.Range(0f, 0.15f) : Random.Range(0.8f, 1.08f);
            l.intensity = baseIntensity * k;
            if (bulb != null) { mpb.SetColor("_EmissionColor", bulbColor * k); bulb.SetPropertyBlock(mpb); }
        }
    }
}
