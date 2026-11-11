using UnityEngine;
using UnityEngine.AI;

namespace HappyBalloon
{
    /// <summary>
    /// 추격자 (버블 / 태엽 인형). NavMesh 로 플레이어를 쫓고,
    /// 좁은 문틀(HB_Chokepoint) 안에서 펌프로 부풀려지면 끼어서 멈춘다.
    /// lethal 이면 닿는 순간 플레이어 사망, 아니면 밀쳐내기만 한다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class HB_Chaser : HB_Inflatable
    {
        public enum State { Idle, Chase, Stuck, Frozen }

        [Header("Behaviour")]
        public bool lethal = true;
        public float baseSpeed = 4.4f, farSpeed = 6.6f, nearDistance = 7f, farDistance = 16f;
        public float stuckDuration = 4.5f;
        public bool stayStuck;
        public float catchDistance = 1.35f;
        public float pushForce = 9f;

        [Header("Visual / Audio")]
        public Transform visual;
        public Transform head;
        public Transform windupKey;
        public float visualHeight = 2.9f;
        public string stepSound = "step_heavy";
        public float stepInterval = 0.55f;
        public float stepVolume = 1f;

        public State state = State.Idle;
        public HB_Chokepoint choke;
        public float DistanceToPlayer { get; private set; } = 999f;

        NavMeshAgent agent;
        Vector3 visBase;
        float stuckT, repath, stepT, contactCd, hintCd, lastPump = -9f;
        HB_Chokepoint stuckIn;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            if (visual != null) visBase = visual.localScale;
            agent.speed = baseSpeed; agent.acceleration = 14f; agent.angularSpeed = 600f; agent.autoBraking = false;
            agent.stoppingDistance = 0.2f;
        }

        public void Activate()
        {
            gameObject.SetActive(true);
            state = State.Chase;
            if (agent.isOnNavMesh) agent.isStopped = false;
        }

        public void ResetTo(Vector3 pos, float rotY, bool chase)
        {
            gameObject.SetActive(true);
            amount = 0f;
            if (stuckIn != null) stuckIn.SetStuck(false);
            stuckIn = null;
            agent.Warp(pos);
            transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            state = chase ? State.Chase : State.Idle;
            if (agent.isOnNavMesh) { agent.ResetPath(); agent.isStopped = !chase; }
            if (visual != null) visual.localScale = visBase;
        }

        /// <summary>점프스케어용: 몸과 얼굴을 즉시 대상 쪽으로 돌린다.</summary>
        public void FaceInstant(Vector3 worldPos)
        {
            var d = worldPos - transform.position; d.y = 0f;
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(d);
            if (head != null) head.rotation = Quaternion.LookRotation(worldPos - head.position);
        }

        public void Freeze()
        {
            state = State.Frozen;
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; agent.ResetPath(); }
        }

        void Update()
        {
            var p = HB_PlayerController.I;
            if (p == null) return;
            float dt = Time.deltaTime;
            Vector3 toP = p.transform.position - transform.position;
            float flat = new Vector2(toP.x, toP.z).magnitude;
            DistanceToPlayer = flat;

            choke = null;
            foreach (var c in HB_Chokepoint.All) if (c.Contains(transform.position)) { choke = c; break; }

            switch (state)
            {
                case State.Chase: UpdateChase(p, toP, flat, dt); break;
                case State.Stuck:
                    if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; }
                    if (!stayStuck)
                    {
                        stuckT -= dt;
                        if (stuckT <= 0f) Unstick();
                    }
                    break;
            }

            if (windupKey != null && state == State.Chase) windupKey.Rotate(Vector3.forward, 240f * dt, Space.Self);
            UpdateVisual(p, dt);
        }

        void UpdateChase(HB_PlayerController p, Vector3 toP, float flat, float dt)
        {
            if (!agent.isOnNavMesh) return;
            agent.isStopped = false;
            repath -= dt;
            if (repath <= 0f) { repath = 0.15f; agent.SetDestination(p.transform.position); }

            float k = Mathf.InverseLerp(nearDistance, farDistance, flat);
            float speed = Mathf.Lerp(baseSpeed, farSpeed, k);
            bool pumped = Time.time - lastPump < 0.2f;
            if (choke != null) speed *= pumped ? 0.12f : 0.75f;      // 문틀에서는 몸을 비집느라 느려짐
            if (amount > 0f && !pumped) amount = Mathf.MoveTowards(amount, 0f, dt * 0.5f);
            agent.speed = speed;

            contactCd -= dt;
            if (flat < catchDistance && Mathf.Abs(toP.y) < 2.2f && contactCd <= 0f)
            {
                contactCd = 1.6f;
                if (lethal) HB_GameDirector.I?.PlayerCaught(this);
                else
                {
                    var push = toP; push.y = 0f;
                    if (push.sqrMagnitude < 0.01f) push = transform.forward;
                    p.AddImpulse(push.normalized * pushForce);
                    p.Shake(0.6f);
                    HB_Audio.I?.PlayAt("squeak", transform.position, 0.9f, 0.7f);
                    HB_GameDirector.I?.OnEvent("doll_push", this);
                }
            }

            float v = agent.velocity.magnitude;
            stepT -= dt * Mathf.Clamp(v / Mathf.Max(0.1f, baseSpeed), 0.2f, 1.6f);
            if (stepT <= 0f && v > 0.2f)
            {
                stepT = stepInterval;
                HB_Audio.I?.PlayAt(stepSound, transform.position, stepVolume, Random.Range(0.9f, 1.06f), lethal ? 45f : 18f);
                if (lethal) p.Shake(Mathf.Clamp01(1f - flat / 16f) * 0.3f);
            }
        }

        void UpdateVisual(HB_PlayerController p, float dt)
        {
            if (visual == null) return;
            float infl = state == State.Stuck ? 1f : amount;
            bool stuckNow = state == State.Stuck;
            float wob = Mathf.Sin(Time.time * (stuckNow ? 24f : 7f)) * (stuckNow ? 0.045f : 0.03f);
            var sc = visBase * (1f + infl * 0.5f);
            sc.y = visBase.y * (1f + infl * 0.18f + wob);
            if (choke != null && !stuckNow) { sc.x *= 0.78f; sc.z *= 1.12f; }   // 문틀을 비집고 들어가는 모습

            // 낮은 천장 / 문틀 상인방 아래에서는 고무처럼 찌그러짐
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.up, out var h, 6f, ~0, QueryTriggerInteraction.Ignore))
            {
                float room = h.distance + 0.3f;
                float need = visualHeight * (sc.y / Mathf.Max(0.001f, visBase.y));
                if (need > room - 0.05f)
                {
                    float f = Mathf.Max(0.3f, (room - 0.05f) / need);
                    sc.y *= f; sc.x *= 1f + (1f - f) * 0.5f; sc.z *= 1f + (1f - f) * 0.5f;
                }
            }
            visual.localScale = Vector3.Lerp(visual.localScale, sc, dt * 10f);

            if (head != null && state != State.Idle)
            {
                var look = p.cam.transform.position - head.position;
                if (look.sqrMagnitude > 0.01f) head.rotation = Quaternion.Slerp(head.rotation, Quaternion.LookRotation(look), dt * 5f);
            }
        }

        public override bool Pump(float dir, float dt)
        {
            if (dir <= 0f || state == State.Stuck || state == State.Idle || state == State.Frozen) return false;
            lastPump = Time.time;
            if (choke != null)
            {
                amount += inflateRate * dt;
                if (amount >= 1f) { amount = 1f; BecomeStuck(); }
                return true;
            }
            amount = Mathf.Min(0.3f, amount + inflateRate * 0.5f * dt);
            if (Time.time > hintCd)
            {
                hintCd = Time.time + 4f;
                HB_UI.I?.Toast("좁은 문틀 <color=#FFA040>안</color>에 들어왔을 때 부풀려야 끼워진다!", 2.5f);
            }
            return true;
        }

        void BecomeStuck()
        {
            state = State.Stuck;
            stuckT = stuckDuration;
            stuckIn = choke;
            if (stuckIn != null) stuckIn.SetStuck(true);
            if (agent.isOnNavMesh) { agent.isStopped = true; agent.velocity = Vector3.zero; agent.ResetPath(); }
            HB_Audio.I?.PlayAt("stretch", transform.position, 1f, lethal ? 0.8f : 1.2f, 40f);
            HB_GameDirector.I?.OnChaserStuck(this, stuckIn);
        }

        void Unstick()
        {
            state = State.Chase;
            amount = 0f;
            if (stuckIn != null) stuckIn.SetStuck(false);
            stuckIn = null;
            HB_Audio.I?.PlayAt("deflate_end", transform.position, 1f, 0.6f, 45f);
            HB_Audio.I?.PlayAt("squeak", transform.position, 1f, 0.55f, 45f);
            HB_GameDirector.I?.OnEvent("chaser_free", this);
        }

        public override string HintText()
        {
            if (state == State.Stuck) return "  (끼었다!)";
            if (choke != null) return "  <color=#FFA040>[좌클릭] 지금 부풀려!</color>";
            return "";
        }
    }
}
