using UnityEngine;

namespace VocaNerd
{
    public class ObjectRotator : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Tooltip("1秒あたりの回転量（度／秒）。Z軸まわりに回転します。")]
        [SerializeField] private float rotationSpeed = 90f;

        private void Awake()
        {
            if (target == null) target = transform;
        }

#if UNITY_EDITOR
        private void Reset()
        {
            target = transform;
        }
#endif

        private void Update()
        {
            if (target == null) return;

            target.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
    }
}
