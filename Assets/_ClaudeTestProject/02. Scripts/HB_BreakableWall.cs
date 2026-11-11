using System.Collections;
using UnityEngine;

namespace HappyBalloon
{
    /// <summary>버블이 뚫고 나오는 벽. 조각(Rigidbody)들을 폭발력으로 날린다.</summary>
    public class HB_BreakableWall : MonoBehaviour
    {
        public Rigidbody[] chunks;
        public bool broken;

        public void Break(Vector3 from, float force)
        {
            if (broken) return;
            broken = true;
            foreach (var rb in chunks)
            {
                if (rb == null) continue;
                rb.isKinematic = false;
                rb.AddExplosionForce(force, from, 9f, 0.5f, ForceMode.Impulse);
                rb.AddTorque(Random.insideUnitSphere * force * 0.5f, ForceMode.Impulse);
            }
            HB_Audio.I?.PlayAt("crash", transform.position, 1f, 1f, 70f);
            StartCoroutine(Settle());
        }

        IEnumerator Settle()
        {
            yield return new WaitForSeconds(4f);
            foreach (var rb in chunks)
            {
                if (rb == null) continue;
                rb.isKinematic = true;
                var c = rb.GetComponent<Collider>();
                if (c != null) c.enabled = false;
            }
        }
    }
}
