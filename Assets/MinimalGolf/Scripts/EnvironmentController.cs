using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MinimalGolf
{
    /// <summary>
    /// Fully configurable controller for the procedural starry sky.
    /// Attach to a GameObject named "Environment". Drives the skybox material
    /// assigned to RenderSettings.skybox. Works in Edit mode and Play mode,
    /// animates stars/comets over time. All values forwarded to StarrySky.shader.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class EnvironmentController : MonoBehaviour
    {
        [Header("Material")]
        [Tooltip("Skybox material using MinimalGolf/StarrySky. If null, tries RenderSettings.skybox.")]
        public Material skyboxMaterial;

        [Tooltip("Automatically assign skyboxMaterial to RenderSettings.skybox on enable.")]
        public bool autoAssignSkybox = true;

        [Tooltip("If true, also sets main camera clearFlags to Skybox so skybox is visible when it was SolidColor.")]
        public bool updateCameraClearFlags = false;

        [Header("Sky Gradient")]
        [ColorUsage(false)] public Color horizonColor = new Color(0.22f, 0.32f, 0.52f, 1f);
        [ColorUsage(false)] public Color zenithColor = new Color(0.02f, 0.04f, 0.14f, 1f);
        [Range(-0.3f, 0.5f)] public float horizonHeight = 0.02f;
        [Range(0.01f, 0.8f)] public float horizonFalloff = 0.25f;

        [Header("Stars")]
        [Range(0.2f, 4f)] public float starDensity = 1.2f;
        [Range(5f, 200f)] public float starSharpness = 42f;
        [Range(0f, 2f)] public float starIntensity = 1f;
        [ColorUsage(false)] public Color starColor = Color.white;
        [Range(0f, 1f)] public float starColorVariation = 0.35f;
        [Range(0f, 1f)] public float starMinBrightness = 0.35f;

        [Header("Animation")]
        [Range(0f, 5f)] public float twinkleSpeed = 1.2f;
        [Range(0f, 1f)] public float twinkleAmount = 0.35f;
        [Range(-2f, 2f)] public float starRotationSpeed = 0.06f;
        [Range(0f, 3f)] public float timeScale = 1f;
        public bool pauseAnimation = false;

        [Header("Comets — Very Subtle")]
        public bool enableComets = true;
        [ColorUsage(false)] public Color cometColor = new Color(0.85f, 0.95f, 1f, 1f);
        [Range(0f, 2f)] public float cometIntensity = 0.28f;
        [Range(0f, 3f)] public float cometSpeed = 0.35f;
        [Range(0.05f, 1f)] public float cometLength = 0.35f;
        [Range(5f, 100f)] public float cometSharpness = 38f;
        [Range(0f, 0.15f)] public float cometFrequency = 0.015f;
        [Range(0.5f, 8f)] public float cometTailFalloff = 3.5f;

        // Cached property IDs for performance / SRP batcher friendliness
        static readonly int ID_HorizonColor = Shader.PropertyToID("_HorizonColor");
        static readonly int ID_ZenithColor = Shader.PropertyToID("_ZenithColor");
        static readonly int ID_HorizonHeight = Shader.PropertyToID("_HorizonHeight");
        static readonly int ID_HorizonFalloff = Shader.PropertyToID("_HorizonFalloff");
        static readonly int ID_StarDensity = Shader.PropertyToID("_StarDensity");
        static readonly int ID_StarSharpness = Shader.PropertyToID("_StarSharpness");
        static readonly int ID_StarIntensity = Shader.PropertyToID("_StarIntensity");
        static readonly int ID_StarColor = Shader.PropertyToID("_StarColor");
        static readonly int ID_StarColorVariation = Shader.PropertyToID("_StarColorVariation");
        static readonly int ID_StarMinBrightness = Shader.PropertyToID("_StarMinBrightness");
        static readonly int ID_TwinkleSpeed = Shader.PropertyToID("_TwinkleSpeed");
        static readonly int ID_TwinkleAmount = Shader.PropertyToID("_TwinkleAmount");
        static readonly int ID_StarRotationSpeed = Shader.PropertyToID("_StarRotationSpeed");
        static readonly int ID_TimeScale = Shader.PropertyToID("_TimeScale");
        static readonly int ID_EnableComets = Shader.PropertyToID("_EnableComets");
        static readonly int ID_CometColor = Shader.PropertyToID("_CometColor");
        static readonly int ID_CometIntensity = Shader.PropertyToID("_CometIntensity");
        static readonly int ID_CometSpeed = Shader.PropertyToID("_CometSpeed");
        static readonly int ID_CometLength = Shader.PropertyToID("_CometLength");
        static readonly int ID_CometSharpness = Shader.PropertyToID("_CometSharpness");
        static readonly int ID_CometFrequency = Shader.PropertyToID("_CometFrequency");
        static readonly int ID_CometTailFalloff = Shader.PropertyToID("_CometTailFalloff");

        Material runtimeMaterialInstance;
        bool warnedHighFrequency;
        bool warnedName;

        void OnEnable()
        {
            EnsureMaterial();
            ApplyToMaterial();
            ValidateNaming();
        }

        void OnValidate()
        {
            // Clamp to avoid extreme Quest cost
            starDensity = Mathf.Clamp(starDensity, 0.2f, 4f);
            cometFrequency = Mathf.Clamp(cometFrequency, 0f, 0.15f);
            ApplyToMaterial();
            ValidateNaming();
        }

        void Update()
        {
            // Drive time-based animation in editor and play mode
            // Shader uses _Time.y internally, but we also expose _TimeScale
            // Keep material in sync every frame so slider changes animate immediately
            ApplyToMaterial();
        }

        void OnDisable()
        {
            // In play mode we may have instantiated material instance
            if (Application.isPlaying && runtimeMaterialInstance != null)
            {
                Destroy(runtimeMaterialInstance);
                runtimeMaterialInstance = null;
            }
        }

        void OnDestroy()
        {
            if (Application.isPlaying && runtimeMaterialInstance != null)
            {
                Destroy(runtimeMaterialInstance);
            }
        }

        void EnsureMaterial()
        {
            if (skyboxMaterial == null)
            {
                skyboxMaterial = RenderSettings.skybox;
            }

            if (skyboxMaterial == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                // In play, use an instance so we don't dirty the asset
                if (runtimeMaterialInstance == null)
                {
                    runtimeMaterialInstance = Instantiate(skyboxMaterial);
                }
                if (autoAssignSkybox)
                {
                    RenderSettings.skybox = runtimeMaterialInstance;
                    DynamicGI.UpdateEnvironment();
                }
                if (updateCameraClearFlags)
                {
                    var cam = Camera.main;
                    if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor)
                        cam.clearFlags = CameraClearFlags.Skybox;
                }
            }
            else
            {
                // Edit mode: assign shared skybox so Scene view updates
                if (autoAssignSkybox && RenderSettings.skybox != skyboxMaterial)
                {
                    RenderSettings.skybox = skyboxMaterial;
#if UNITY_EDITOR
                    EditorUtility.SetDirty(RenderSettings.skybox);
#endif
                    DynamicGI.UpdateEnvironment();
                }
            }
        }

        void ApplyToMaterial()
        {
            Material target = null;
            if (Application.isPlaying && runtimeMaterialInstance != null)
                target = runtimeMaterialInstance;
            else
                target = skyboxMaterial;

            if (target == null)
                return;

            // Verify shader has properties (avoid errors if wrong shader assigned)
            if (!target.HasProperty(ID_HorizonColor))
                return;

            target.SetColor(ID_HorizonColor, horizonColor);
            target.SetColor(ID_ZenithColor, zenithColor);
            target.SetFloat(ID_HorizonHeight, horizonHeight);
            target.SetFloat(ID_HorizonFalloff, horizonFalloff);
            target.SetFloat(ID_StarDensity, starDensity);
            target.SetFloat(ID_StarSharpness, starSharpness);
            target.SetFloat(ID_StarIntensity, starIntensity);
            target.SetColor(ID_StarColor, starColor);
            target.SetFloat(ID_StarColorVariation, starColorVariation);
            target.SetFloat(ID_StarMinBrightness, starMinBrightness);
            target.SetFloat(ID_TwinkleSpeed, twinkleSpeed);
            target.SetFloat(ID_TwinkleAmount, twinkleAmount);
            float rotSpeed = pauseAnimation ? 0f : starRotationSpeed;
            float twSpeed = pauseAnimation ? 0f : twinkleSpeed;
            float cSpeed = pauseAnimation ? 0f : cometSpeed;
            target.SetFloat(ID_StarRotationSpeed, rotSpeed);
            target.SetFloat(ID_TwinkleSpeed, twSpeed);
            target.SetFloat(ID_CometSpeed, cSpeed);
            target.SetFloat(ID_TimeScale, pauseAnimation ? 0f : timeScale);
            target.SetFloat(ID_EnableComets, enableComets ? 1f : 0f);
            target.SetColor(ID_CometColor, cometColor);
            target.SetFloat(ID_CometIntensity, cometIntensity);
            target.SetFloat(ID_CometLength, cometLength);
            target.SetFloat(ID_CometSharpness, cometSharpness);
            target.SetFloat(ID_CometFrequency, cometFrequency);
            target.SetFloat(ID_CometTailFalloff, cometTailFalloff);

            if (cometFrequency > 0.08f && !warnedHighFrequency)
            {
                warnedHighFrequency = true;
                Debug.LogWarning($"[Environment] Comet Frequency {cometFrequency:0.000} is high — comets will no longer be subtle. Recommended <= 0.02 for quest.", this);
            }
            if (cometFrequency <= 0.08f) warnedHighFrequency = false;
        }

        void ValidateNaming()
        {
            if (gameObject.name != "Environment" && !warnedName)
            {
                warnedName = true;
                Debug.LogWarning($"[Environment] This controller is on '{gameObject.name}' but is expected to be on a GameObject named 'Environment' per spec. Rename the GameObject to 'Environment'.", this);
            }
            if (gameObject.name == "Environment") warnedName = false;
        }

        // Called when user clicks Reset in Inspector
        void Reset()
        {
            horizonColor = new Color(0.22f, 0.32f, 0.52f, 1f);
            zenithColor = new Color(0.02f, 0.04f, 0.14f, 1f);
            horizonHeight = 0.02f;
            horizonFalloff = 0.25f;
            starDensity = 1.2f;
            starSharpness = 42f;
            starIntensity = 1f;
            starColor = Color.white;
            starColorVariation = 0.35f;
            starMinBrightness = 0.35f;
            twinkleSpeed = 1.2f;
            twinkleAmount = 0.35f;
            starRotationSpeed = 0.06f;
            timeScale = 1f;
            pauseAnimation = false;
            enableComets = true;
            cometColor = new Color(0.85f, 0.95f, 1f, 1f);
            cometIntensity = 0.28f;
            cometSpeed = 0.35f;
            cometLength = 0.35f;
            cometSharpness = 38f;
            cometFrequency = 0.015f;
            cometTailFalloff = 3.5f;
        }

#if UNITY_EDITOR
        [MenuItem("GameObject/MinimalGolf/Create Environment", false, 10)]
        static void CreateEnvironmentMenu()
        {
            var existing = GameObject.Find("Environment");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                Debug.Log("[Environment] 'Environment' already exists — selected.", existing);
                return;
            }
            var go = new GameObject("Environment");
            Undo.RegisterCreatedObjectUndo(go, "Create Environment");
            var ctrl = go.AddComponent<EnvironmentController>();
            // Try to auto-assign StarrySky material if found
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/MinimalGolf/Materials/StarrySky.mat");
            if (mat != null) ctrl.skyboxMaterial = mat;
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
#endif
    }
}
