using UnityEngine;

namespace CoreKeepers
{
    public sealed class CoreCameraFollow : MonoBehaviour
    {
        [SerializeField] private Vector3 offset = new(0f, 13f, -9f);
        [SerializeField, Min(0.01f)] private float smoothTime = 0.16f;
        private Vector3 velocity;

        private void LateUpdate()
        {
            if (NetworkWarrior.Local == null)
                return;
            var desired = NetworkWarrior.Local.transform.position + offset;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
            transform.rotation = Quaternion.LookRotation(NetworkWarrior.Local.transform.position - transform.position, Vector3.up);
        }
    }
}
