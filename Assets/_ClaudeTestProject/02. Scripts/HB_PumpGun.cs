using UnityEngine;
using UnityEngine.InputSystem;

namespace HappyBalloon
{
    /// <summary>펌프 건: 좌클릭(길게) 부풀리기, 우클릭(길게) 바람 빼기. 카메라에 붙인다.</summary>
    public class HB_PumpGun : MonoBehaviour
    {
        public bool equipped;
        public GameObject model;
        public ParticleSystem air;
        public float range = 14f;

        AudioSource hiss;
        Vector3 modelBase;
        float kick, puffCd;

        void Start()
        {
            if (model != null) { modelBase = model.transform.localPosition; model.SetActive(equipped); }
            hiss = HB_Audio.I.Attach("hiss_in", transform, 0f, 10f);
            hiss.spatialBlend = 0f;
            if (air != null) { var em = air.emission; em.enabled = false; }
        }

        public void Equip()
        {
            equipped = true;
            if (model != null) model.SetActive(true);
        }

        void Update()
        {
            var p = HB_PlayerController.I;
            var d = HB_GameDirector.I;
            if (!equipped || p == null || (d != null && d.Paused))
            {
                if (hiss != null) hiss.volume = 0f;
                return;
            }
            var mouse = Mouse.current;
            bool canUse = !p.locked && mouse != null && (HB_UI.I == null || !HB_UI.I.NoteOpen);
            bool L = canUse && mouse.leftButton.isPressed;
            bool R = canUse && mouse.rightButton.isPressed && !L;
            float dir = L ? 1f : R ? -1f : 0f;
            float dt = Time.deltaTime;

            HB_Inflatable target = null;
            if (Physics.Raycast(transform.position, transform.forward, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
                target = hit.collider.GetComponentInParent<HB_Inflatable>();

            bool active = false;
            if (dir != 0f && target != null) active = target.Pump(dir, dt);

            // 소리
            if (dir != 0f)
            {
                var c = HB_Audio.I.Get(dir > 0f ? "hiss_in" : "hiss_out");
                if (hiss.clip != c) { hiss.clip = c; hiss.Play(); }
                if (!hiss.isPlaying) hiss.Play();
                hiss.volume = Mathf.MoveTowards(hiss.volume, active ? 0.6f : 0.2f, dt * 6f);
                hiss.pitch = 1f + (target != null ? target.amount * 0.45f : 0f) * (dir > 0f ? 1f : -0.5f);
                if (!active) { puffCd -= dt; if (puffCd <= 0f) { puffCd = 0.6f; HB_Audio.I.Play2D("puff", 0.4f); } }
            }
            else hiss.volume = Mathf.MoveTowards(hiss.volume, 0f, dt * 8f);

            if (air != null)
            {
                var em = air.emission; em.enabled = dir != 0f;
                var main = air.main; main.startSpeed = dir > 0f ? 7f : -2.5f;
            }

            kick = dir != 0f ? Mathf.PingPong(Time.time * 9f, 1f) : Mathf.MoveTowards(kick, 0f, dt * 5f);
            if (model != null) model.transform.localPosition = modelBase + new Vector3(0f, -0.012f * kick, -0.035f * kick);

            if (HB_UI.I != null)
            {
                if (target != null) HB_UI.I.SetPump(true, target.amount, target.displayName + target.HintText());
                else HB_UI.I.SetPump(false, 0f, "");
            }
        }
    }
}
