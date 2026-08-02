using UnityEngine;

namespace MinimalGolf
{
    public sealed class GolfBallImpact : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            CameraImpactShake.Instance?.RegisterImpact(collision.relativeVelocity.magnitude);

            if (collision.collider.gameObject.name == "Green Playing Surface")
                return;

            AudioManager.Instance?.PlayCollisionSfx();
        }
    }
}
