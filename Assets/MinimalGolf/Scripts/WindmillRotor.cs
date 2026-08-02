using UnityEngine;

namespace MinimalGolf
{
    public sealed class WindmillRotor : MonoBehaviour
    {
        [SerializeField] private float degreesPerSecond = 72f;

        private void FixedUpdate()
        {
            transform.Rotate(0f, 0f, degreesPerSecond * Time.fixedDeltaTime, Space.Self);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            foreach (BoxCollider bladeCollider in GetComponentsInChildren<BoxCollider>(true))
            {
                Gizmos.matrix = bladeCollider.transform.localToWorldMatrix;
                Gizmos.color = new Color(1f, 0.78f, 0.12f, 1f);
                Gizmos.DrawWireCube(bladeCollider.center, bladeCollider.size);
            }
        }
#endif
    }
}
