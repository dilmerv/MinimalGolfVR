using UnityEngine;

namespace MinimalGolf
{
    [DefaultExecutionOrder(-1000)]
    public sealed class CustomGameCursor : MonoBehaviour
    {
        // VR: standalone cursor removed. Kept as stub for builder backwards compat.
        public void Configure(Texture2D texture, Vector2 cursorHotspot) { /* no-op in VR */ }
    }
}
