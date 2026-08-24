using UnityEngine;

namespace CoreKeepers
{
    [RequireComponent(typeof(Collider))]
    public sealed class CoreSwampVolume : MonoBehaviour
    {
        private void Reset() => GetComponent<Collider>().isTrigger = true;
        private void OnTriggerEnter(Collider other) => other.GetComponentInParent<EnemyBrain>()?.SetInSwamp(true);
        private void OnTriggerExit(Collider other) => other.GetComponentInParent<EnemyBrain>()?.SetInSwamp(false);
    }
}
