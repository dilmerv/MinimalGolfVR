using UnityEngine;

namespace MinimalGolf
{
    [DefaultExecutionOrder(-1000)]
    public sealed class CustomGameCursor : MonoBehaviour
    {
        [Header("Cursor")]
        [SerializeField] private Texture2D cursorTexture;
        [SerializeField] private Vector2 hotspot = new Vector2(10f, 5f);
        [SerializeField] private CursorMode cursorMode = CursorMode.ForceSoftware;

        public void Configure(Texture2D texture, Vector2 cursorHotspot)
        {
            cursorTexture = texture;
            hotspot = cursorHotspot;
            ApplyCursor();
        }

        private void OnEnable()
        {
            ApplyCursor();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyCursor();
        }

        private void ApplyCursor()
        {
            if (!Application.isPlaying || cursorTexture == null)
                return;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Cursor.SetCursor(cursorTexture, hotspot, cursorMode);
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
