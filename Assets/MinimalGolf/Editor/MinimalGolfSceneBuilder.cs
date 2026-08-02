using System;
using System.Collections.Generic;
using MinimalGolf;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MinimalGolfEditor
{
    public static class MinimalGolfSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/MinimalGolf.unity";
        private const string MaterialFolder = "Assets/MinimalGolf/Materials";
        private const string PhysicsFolder = "Assets/MinimalGolf/Physics";
        private const string MusicFolder = "Assets/Audio/Music";
        private const string SfxFolder = "Assets/Audio/SFX";
        private const string UiFontPath = "Assets/MinimalGolf/Fonts/Inter-Regular.otf";
        private const string KenneyModelFolder = "Assets/Kenney/MinigolfKit/Models/FBX format";
        private static readonly string[] RendererDataPaths =
        {
            "Assets/Settings/PC_Renderer.asset",
            "Assets/Settings/Mobile_Renderer.asset"
        };

        private static Material greenMaterial;
        private static Material railMaterial;
        private static Material creamMaterial;
        private static Material cupMaterial;
        private static Material goldMaterial;
        private static Material blueMaterial;
        private static Material accentMaterial;
        private static Material kenneyMaterial;
        private static Material aimingMaterial;
        private static PhysicsMaterial ballPhysicsMaterial;

        [MenuItem("Minimal Golf/Build Authored Course")]
        public static void Build()
        {
            Shader toon = Shader.Find("Minimal Golf/Toon");
            if (toon == null)
                throw new InvalidOperationException("Minimal Golf/Toon shader has not compiled.");

            CreateMaterials(toon);
            CreatePhysicsMaterial();
            ConfigureOutlineRenderers();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "MinimalGolf";

            Camera camera = CreateEnvironment();
            MiniGolfLevel level1 = CreateWarmUp();
            MiniGolfLevel level2 = CreateGarden();
            MiniGolfLevel level3 = CreateWindmillWay();
            MiniGolfLevel level4 = CreateBumperBend();
            MiniGolfLevel level5 = CreateGrandTour();

            ConfigureLevelReveal(level1);
            ConfigureLevelReveal(level2);
            ConfigureLevelReveal(level3);
            ConfigureLevelReveal(level4);
            ConfigureLevelReveal(level5);

            // Keep every handcrafted course visible and easy to edit in the Scene view.
            // MinimalGolfGame restores all roots to the shared gameplay origin at runtime.
            level1.transform.localPosition = new Vector3(0f, 0f, 0f);
            level2.transform.localPosition = new Vector3(14f, 0f, 0f);
            level3.transform.localPosition = new Vector3(30f, 0f, 0f);
            level4.transform.localPosition = new Vector3(46f, 0f, 0f);
            level5.transform.localPosition = new Vector3(64f, 0f, 0f);

            GameObject systems = new GameObject("GAME SYSTEMS");
            MinimalGolfGame game = systems.AddComponent<MinimalGolfGame>();
            game.levels = new[] { level1, level2, level3, level4, level5 };
            game.gameCamera = camera;
            game.uiFont = AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);

            CreateAudioManager();

            GameObject lineObject = new GameObject("Aiming Line");
            lineObject.transform.SetParent(systems.transform, false);
            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = aimingMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.045f;
            line.endWidth = 0.045f;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 6;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.enabled = false;
            game.aimingLine = line;

            level1.gameObject.SetActive(true);
            level2.gameObject.SetActive(true);
            level3.gameObject.SetActive(true);
            level4.gameObject.SetActive(true);
            level5.gameObject.SetActive(true);

            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("779EBE");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 15f;
            RenderSettings.fogEndDistance = 34f;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("AFC7D4");
            RenderSettings.ambientIntensity = 0.72f;

            PlayerSettings.productName = "Minimal Golf";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.resizableWindow = true;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeGameObject = level1.gameObject;
            Debug.Log("Minimal Golf authored scene built successfully at " + ScenePath);
        }

        private static void CreateAudioManager()
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { MusicFolder });
            string[] paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);

            Array.Sort(paths, StringComparer.OrdinalIgnoreCase);
            AudioClip[] tracks = new AudioClip[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                tracks[i] = AssetDatabase.LoadAssetAtPath<AudioClip>(paths[i]);

            GameObject audioObject = new GameObject("AUDIO MANAGER");
            AudioManager audioManager = audioObject.AddComponent<AudioManager>();
            audioManager.Configure(tracks, 0.32f);
            AudioClip[] shotClips =
            {
                LoadSfx("Golf_Ball_Hit_01"),
                LoadSfx("Golf_Ball_Hit_02"),
                LoadSfx("Golf_Ball_Hit_03")
            };
            AudioClip[] holeClips =
            {
                LoadSfx("Ball_Fall_To_The_Empty_Hole_01"),
                LoadSfx("Ball_Fall_To_The_Empty_Hole_02"),
                LoadSfx("Ball_Fall_To_The_Empty_Hole_03")
            };
            AudioClip collisionClip = LoadSfx("Pitching_Wedge_Shot_Hard_01");
            audioManager.ConfigureSfx(shotClips, holeClips, collisionClip, 0.70f);
            audioManager.ConfigureRotationSfx(LoadSfx("Classic_Woosh"));

            if (tracks.Length == 0)
                Debug.LogWarning("No background music was found under " + MusicFolder + ".");
            if (collisionClip == null)
                Debug.LogWarning("Pitching_Wedge_Shot_Hard_01 was not found under " + SfxFolder + ".");
        }

        private static AudioClip LoadSfx(string clipName)
        {
            string[] guids = AssetDatabase.FindAssets(clipName + " t:AudioClip", new[] { SfxFolder });
            foreach (string guid in guids)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip != null && clip.name.Equals(clipName, StringComparison.OrdinalIgnoreCase))
                    return clip;
            }

            return null;
        }

        private static void ConfigureLevelReveal(MiniGolfLevel level)
        {
            var parts = new List<Transform>();
            for (int groupIndex = 0; groupIndex < level.transform.childCount; groupIndex++)
            {
                Transform group = level.transform.GetChild(groupIndex);
                for (int childIndex = 0; childIndex < group.childCount; childIndex++)
                {
                    Transform child = group.GetChild(childIndex);
                    if (child.GetComponentInChildren<Renderer>(true) != null)
                        parts.Add(child);
                }
            }

            LevelRevealAnimator reveal = level.gameObject.AddComponent<LevelRevealAnimator>();
            reveal.Configure(parts.ToArray());
            level.revealAnimator = reveal;
        }

        private static void CreateMaterials(Shader toon)
        {
            greenMaterial = GetOrCreateMaterial("Course Green", toon, Hex("62B875"), Hex("5A7F6B"), 0.09f);
            railMaterial = GetOrCreateMaterial("Dark Green Rails", toon, Hex("275B45"), Hex("3B5A52"), 0.10f);
            creamMaterial = GetOrCreateMaterial("Warm Cream Base", toon, Hex("F0D9A6"), Hex("BEA97E"), 0.08f);
            cupMaterial = GetOrCreateMaterial("Dark Cup", toon, Hex("172A38"), Hex("17232D"), 0.06f);
            goldMaterial = GetOrCreateMaterial("Cup Gold Rim", toon, Hex("F1C86C"), Hex("A68142"), 0.12f);
            blueMaterial = GetOrCreateMaterial("Ball Blue", toon, Hex("4DA9E8"), Hex("416F91"), 0.16f);
            accentMaterial = GetOrCreateMaterial("Obstacle Accent", toon, Hex("E87B62"), Hex("94564F"), 0.10f);
            kenneyMaterial = GetOrCreateMaterial("Kenney Colormap Toon", toon, Color.white, Hex("71818B"), 0.10f);
            aimingMaterial = GetOrCreateMaterial("Aiming Line Toon", toon, Color.white, Color.white, 0f);

            Texture2D colormap = AssetDatabase.LoadAssetAtPath<Texture2D>(KenneyModelFolder + "/Textures/colormap.png");
            kenneyMaterial.SetTexture("_BaseMap", colormap);
            kenneyMaterial.SetColor("_BaseColor", Color.white);
            aimingMaterial.SetFloat("_UseVertexColor", 1f);
            aimingMaterial.SetFloat("_AmbientStrength", 1f);
            aimingMaterial.SetFloat("_OutlineEnabled", 0f);
            aimingMaterial.DisableKeyword("_OUTLINE_ON");

            EditorUtility.SetDirty(kenneyMaterial);
            EditorUtility.SetDirty(aimingMaterial);
        }

        private static Material GetOrCreateMaterial(string name, Shader shader, Color color, Color shade, float rim)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetColor("_BaseColor", color);
            material.SetColor("_ShadeColor", shade);
            material.SetFloat("_ShadowThreshold", 0.48f);
            material.SetFloat("_ShadowSoftness", 0.035f);
            material.SetFloat("_AmbientStrength", 0.34f);
            material.SetColor("_RimColor", Hex("BDEBFF"));
            material.SetFloat("_RimPower", 3.8f);
            material.SetFloat("_RimStrength", rim);
            material.SetFloat("_OutlineEnabled", 1f);
            material.SetColor("_OutlineColor", Hex("163C36"));
            material.SetFloat("_OutlineWidth", 0.018f);
            material.EnableKeyword("_OUTLINE_ON");
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreatePhysicsMaterial()
        {
            string path = PhysicsFolder + "/Playful Ball Physics.asset";
            ballPhysicsMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (ballPhysicsMaterial == null)
            {
                ballPhysicsMaterial = new PhysicsMaterial("Playful Ball Physics");
                AssetDatabase.CreateAsset(ballPhysicsMaterial, path);
            }

            ballPhysicsMaterial.dynamicFriction = 0.38f;
            ballPhysicsMaterial.staticFriction = 0.44f;
            ballPhysicsMaterial.bounciness = 0.34f;
            ballPhysicsMaterial.frictionCombine = PhysicsMaterialCombine.Average;
            ballPhysicsMaterial.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(ballPhysicsMaterial);
        }

        private static void ConfigureOutlineRenderers()
        {
            foreach (string path in RendererDataPaths)
            {
                ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (rendererData == null)
                    continue;

                rendererData.rendererFeatures.RemoveAll(feature => feature == null);
                RenderObjects outlineFeature = null;
                foreach (ScriptableRendererFeature feature in rendererData.rendererFeatures)
                {
                    if (feature is RenderObjects renderObjects && feature.name == "Minimal Golf Outlines")
                    {
                        outlineFeature = renderObjects;
                        break;
                    }
                }

                if (outlineFeature == null)
                {
                    outlineFeature = ScriptableObject.CreateInstance<RenderObjects>();
                    outlineFeature.name = "Minimal Golf Outlines";
                    AssetDatabase.AddObjectToAsset(outlineFeature, rendererData);
                    rendererData.rendererFeatures.Add(outlineFeature);
                }

                outlineFeature.settings.passTag = "Minimal Golf Outlines";
                outlineFeature.settings.Event = RenderPassEvent.AfterRenderingOpaques;
                outlineFeature.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                outlineFeature.settings.filterSettings.LayerMask = ~0;
                outlineFeature.settings.filterSettings.PassNames = new[] { "MinimalGolfOutline" };
                outlineFeature.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
                outlineFeature.settings.overrideDepthState = false;
                outlineFeature.SetActive(true);
                outlineFeature.Create();

                EditorUtility.SetDirty(outlineFeature);
                EditorUtility.SetDirty(rendererData);
            }
        }

        private static Camera CreateEnvironment()
        {
            GameObject cameraObject = new GameObject("ISOMETRIC CAMERA");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(
                new Vector3(5.1f, 5.65f, -6.9f),
                Quaternion.Euler(29.9f, 321f, 0f));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.4f;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 80f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Hex("779EBE");
            camera.allowHDR = true;
            camera.allowMSAA = true;
            UniversalAdditionalCameraData cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = false;
            cameraData.renderShadows = false;
            cameraObject.AddComponent<CameraImpactShake>();

            GameObject lightObject = new GameObject("WARM SUN");
            lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Hex("FFE1B2");
            light.intensity = 1.25f;
            light.shadows = LightShadows.None;
            light.shadowStrength = 0f;
            light.shadowBias = 0.045f;
            light.shadowNormalBias = 0.35f;

            return camera;
        }

        private static MiniGolfLevel CreateWarmUp()
        {
            MiniGolfLevel level = CreateLevelRoot("Level 1 - THE WARM UP", "THE WARM UP", 2, 4.4f, 10.5f, 6.15f);
            Transform obstacles = FindDirectChild(level.transform, "OBSTACLES");
            GameObject gate = InstantiateKenney("structure-gate", "Kenney Gate - Non-Convex MeshCollider", obstacles,
                new Vector3(0f, 0.105f, 0f), Vector3.zero, Vector3.one * 3.35f, kenneyMaterial);
            AddNonConvexMeshColliders(gate);

            AddTeeMarkers(level.transform, new Vector3(0f, 0.13f, -4.15f));
            CreateBallAndHole(level, new Vector3(0f, 0.29f, -4.05f), new Vector3(0f, 0.11f, 4.15f));
            return level;
        }

        private static MiniGolfLevel CreateGarden()
        {
            MiniGolfLevel level = CreateLevelRoot("Level 2 - THE GARDEN", "THE GARDEN", 3, 6.2f, 14.5f, 7.25f);
            Transform obstacles = FindDirectChild(level.transform, "OBSTACLES");

            GameObject wideGate = InstantiateKenney("structure-gate-wide", "Kenney Wide Gate - Non-Convex MeshCollider", obstacles,
                new Vector3(-0.8f, 0.105f, -1.9f), Vector3.zero, Vector3.one * 3.6f, kenneyMaterial);
            AddNonConvexMeshColliders(wideGate);

            GameObject gates = InstantiateKenney("structure-gates", "Kenney Twin Gates - Non-Convex MeshCollider", obstacles,
                new Vector3(0.85f, 0.105f, 2.4f), Vector3.zero, Vector3.one * 3.15f, kenneyMaterial);
            AddNonConvexMeshColliders(gates);

            CreatePrimitive("Garden Planter Left", PrimitiveType.Cube, obstacles, new Vector3(-2.35f, 0.31f, 4.65f), new Vector3(0.65f, 0.42f, 1.9f), railMaterial, true);
            CreatePrimitive("Garden Planter Right", PrimitiveType.Cube, obstacles, new Vector3(2.28f, 0.31f, -4.55f), new Vector3(0.65f, 0.42f, 1.75f), railMaterial, true);
            CreateDecorativePlant(obstacles, new Vector3(-2.35f, 0.63f, 4.65f));
            CreateDecorativePlant(obstacles, new Vector3(2.28f, 0.63f, -4.55f));

            AddTeeMarkers(level.transform, new Vector3(-1.5f, 0.13f, -6.1f));
            CreateBallAndHole(level, new Vector3(-1.5f, 0.29f, -6f), new Vector3(1.55f, 0.11f, 5.95f));
            return level;
        }

        private static MiniGolfLevel CreateWindmillWay()
        {
            MiniGolfLevel level = CreateLevelRoot("Level 3 - WINDMILL WAY", "WINDMILL WAY", 4, 7f, 20f, 9.25f);
            Transform obstacles = FindDirectChild(level.transform, "OBSTACLES");
            Transform rails = FindDirectChild(level.transform, "RAILS");

            GameObject windmill = InstantiateKenney("windmill", "Kenney Rotating Windmill", obstacles,
                new Vector3(0f, -0.084f, -1.65f), Vector3.zero, Vector3.one * 2.75f, kenneyMaterial);
            ConfigureWindmill(windmill);

            const float blockerY = 0.37f;
            const float blockerZ = -1.65f;
            CreatePrimitive("Windmill Left Side Rail Blocker", PrimitiveType.Cube, rails,
                new Vector3(-2.45f, blockerY, blockerZ), new Vector3(2.10f, 0.54f, 0.42f), railMaterial, true);
            CreatePrimitive("Windmill Right Side Rail Blocker", PrimitiveType.Cube, rails,
                new Vector3(2.45f, blockerY, blockerZ), new Vector3(2.10f, 0.54f, 0.42f), railMaterial, true);

            GameObject bumpLeft = InstantiateKenney("bump", "Raised Island Left", obstacles,
                new Vector3(-1.85f, 0.105f, 3.15f), Vector3.zero, new Vector3(2.25f, 2.1f, 2.25f), greenMaterial);
            AddNonConvexMeshColliders(bumpLeft);
            GameObject bumpRight = InstantiateKenney("bump", "Raised Island Right", obstacles,
                new Vector3(1.75f, 0.105f, 5.5f), Vector3.zero, new Vector3(2.15f, 2.0f, 2.15f), greenMaterial);
            AddNonConvexMeshColliders(bumpRight);

            GameObject diamondA = InstantiateKenney("obstacle-diamond", "Diamond Bumper A", obstacles,
                new Vector3(1.8f, 0.105f, 1.2f), new Vector3(0f, 20f, 0f), Vector3.one * 1.35f, accentMaterial);
            AddNonConvexMeshColliders(diamondA);
            GameObject diamondB = InstantiateKenney("obstacle-triangle", "Triangle Bumper B", obstacles,
                new Vector3(-1.65f, 0.105f, 7.1f), new Vector3(0f, -18f, 0f), Vector3.one * 1.55f, accentMaterial);
            AddNonConvexMeshColliders(diamondB);

            AddTeeMarkers(level.transform, new Vector3(0f, 0.13f, -8.55f));
            CreateBallAndHole(level, new Vector3(0f, 0.29f, -8.45f), new Vector3(1.65f, 0.11f, 8.25f));
            return level;
        }

        private static MiniGolfLevel CreateBumperBend()
        {
            MiniGolfLevel level = CreateLevelRoot("Level 4 - BUMPER BEND", "BUMPER BEND", 4, 7.4f, 18f, 8.35f);
            Transform obstacles = FindDirectChild(level.transform, "OBSTACLES");

            GameObject wideGate = InstantiateKenney("structure-gate-wide", "Kenney Bend Gate - Non-Convex MeshCollider", obstacles,
                new Vector3(0.85f, 0.105f, -3.6f), Vector3.zero, Vector3.one * 3.55f, kenneyMaterial);
            AddNonConvexMeshColliders(wideGate);
            CreateTunnelSideGrass(obstacles, "Bend Gate", -3.7f, -0.55f, 2.25f, 3.7f, -3.6f);

            CreatePrimitive("Lower Left Bend Bumper", PrimitiveType.Cube, obstacles,
                new Vector3(-2.35f, 0.34f, -0.4f), new Vector3(1.05f, 0.48f, 3.4f), railMaterial, true);
            CreatePrimitive("Upper Right Bend Bumper", PrimitiveType.Cube, obstacles,
                new Vector3(2.3f, 0.34f, 3.55f), new Vector3(1.05f, 0.48f, 3.25f), railMaterial, true);

            GameObject diamond = InstantiateKenney("obstacle-diamond", "Center Diamond Bumper", obstacles,
                new Vector3(0.3f, 0.105f, 1.75f), new Vector3(0f, 45f, 0f), Vector3.one * 1.45f, accentMaterial);
            AddNonConvexMeshColliders(diamond);
            GameObject finishBump = InstantiateKenney("bump", "Finish Approach Island", obstacles,
                new Vector3(-1.75f, 0.105f, 6.55f), Vector3.zero, new Vector3(2.05f, 1.8f, 2.05f), greenMaterial);
            AddNonConvexMeshColliders(finishBump);

            AddTeeMarkers(level.transform, new Vector3(-1.55f, 0.13f, -7.65f));
            CreateBallAndHole(level, new Vector3(-1.55f, 0.29f, -7.55f), new Vector3(1.55f, 0.11f, 7.45f));
            return level;
        }

        private static MiniGolfLevel CreateGrandTour()
        {
            MiniGolfLevel level = CreateLevelRoot("Level 5 - GRAND TOUR", "GRAND TOUR", 5, 8.5f, 21.5f, 9.7f);
            Transform obstacles = FindDirectChild(level.transform, "OBSTACLES");

            GameObject wideGate = InstantiateKenney("structure-gate-wide", "Kenney Grand Gate - Non-Convex MeshCollider", obstacles,
                new Vector3(-1.05f, 0.105f, -1.35f), Vector3.zero, Vector3.one * 3.7f, kenneyMaterial);
            AddNonConvexMeshColliders(wideGate);
            CreateTunnelSideGrass(obstacles, "Grand Gate", -4.25f, -2.4f, 0.35f, 4.25f, -1.35f);
            GameObject twinGates = InstantiateKenney("structure-gates", "Kenney Final Twin Gates - Non-Convex MeshCollider", obstacles,
                new Vector3(1.15f, 0.105f, 3.65f), Vector3.zero, Vector3.one * 3.35f, kenneyMaterial);
            AddNonConvexMeshColliders(twinGates);
            CreateTunnelSideGrass(obstacles, "Final Twin Gates", -4.25f, -0.5f, 2.8f, 4.25f, 3.65f);

            GameObject bumpLeft = InstantiateKenney("bump", "Grand Tour Island Left", obstacles,
                new Vector3(-2.35f, 0.105f, 6.75f), Vector3.zero, new Vector3(2.1f, 1.9f, 2.1f), greenMaterial);
            AddNonConvexMeshColliders(bumpLeft);
            GameObject triangle = InstantiateKenney("obstacle-triangle", "Grand Tour Triangle", obstacles,
                new Vector3(2.45f, 0.105f, -4.65f), new Vector3(0f, -25f, 0f), Vector3.one * 1.55f, accentMaterial);
            AddNonConvexMeshColliders(triangle);
            GameObject diamond = InstantiateKenney("obstacle-diamond", "Grand Tour Diamond", obstacles,
                new Vector3(2.25f, 0.105f, 7.75f), new Vector3(0f, 20f, 0f), Vector3.one * 1.35f, accentMaterial);
            AddNonConvexMeshColliders(diamond);

            AddTeeMarkers(level.transform, new Vector3(0f, 0.13f, -9.35f));
            CreateBallAndHole(level, new Vector3(0f, 0.29f, -9.25f), new Vector3(0f, 0.11f, 9.2f));
            return level;
        }

        private static MiniGolfLevel CreateLevelRoot(string hierarchyName, string displayName, int par, float width, float length, float cameraSize)
        {
            GameObject root = new GameObject(hierarchyName);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            MiniGolfLevel level = root.AddComponent<MiniGolfLevel>();
            level.levelName = displayName;
            level.par = par;
            level.cameraSize = cameraSize;
            level.courseWidth = width;
            level.courseLength = length;

            Transform course = NewGroup("COURSE", root.transform);
            Transform rails = NewGroup("RAILS", root.transform);
            NewGroup("OBSTACLES", root.transform);
            NewGroup("HOLE", root.transform);
            NewGroup("PLAYER", root.transform);

            CreatePrimitive("Warm Cream Base", PrimitiveType.Cube, course, new Vector3(0f, -0.35f, 0f), new Vector3(width + 1.15f, 0.5f, length + 1.15f), creamMaterial, true);
            CreatePrimitive("Green Playing Surface", PrimitiveType.Cube, course, Vector3.zero, new Vector3(width, 0.2f, length), greenMaterial, true);

            const float railThickness = 0.28f;
            const float railHeight = 0.54f;
            float railY = 0.1f + railHeight * 0.5f;
            CreatePrimitive("Left Rail", PrimitiveType.Cube, rails, new Vector3(-width * 0.5f - railThickness * 0.5f, railY, 0f), new Vector3(railThickness, railHeight, length + railThickness * 2f), railMaterial, true);
            CreatePrimitive("Right Rail", PrimitiveType.Cube, rails, new Vector3(width * 0.5f + railThickness * 0.5f, railY, 0f), new Vector3(railThickness, railHeight, length + railThickness * 2f), railMaterial, true);
            CreatePrimitive("Start Rail", PrimitiveType.Cube, rails, new Vector3(0f, railY, -length * 0.5f - railThickness * 0.5f), new Vector3(width, railHeight, railThickness), railMaterial, true);
            CreatePrimitive("End Rail", PrimitiveType.Cube, rails, new Vector3(0f, railY, length * 0.5f + railThickness * 0.5f), new Vector3(width, railHeight, railThickness), railMaterial, true);
            return level;
        }

        private static void CreateBallAndHole(MiniGolfLevel level, Vector3 ballPosition, Vector3 holePosition)
        {
            Transform playerGroup = FindDirectChild(level.transform, "PLAYER");
            Transform holeGroup = FindDirectChild(level.transform, "HOLE");

            GameObject spawn = new GameObject("Ball Reset Point");
            spawn.transform.SetParent(playerGroup, false);
            spawn.transform.localPosition = ballPosition;
            level.ballSpawn = spawn.transform;

            GameObject ball = new GameObject("Golf Ball");
            ball.transform.SetParent(playerGroup, false);
            ball.transform.localPosition = ballPosition;
            SphereCollider collider = ball.AddComponent<SphereCollider>();
            collider.radius = 0.16f;
            collider.sharedMaterial = ballPhysicsMaterial;
            Rigidbody rigidbody = ball.AddComponent<Rigidbody>();
            rigidbody.mass = 0.78f;
            rigidbody.linearDamping = 0.72f;
            rigidbody.angularDamping = 0.82f;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.maxAngularVelocity = 18f;
            ball.AddComponent<GolfBallImpact>();
            level.ball = rigidbody;

            CreatePrimitive("Blue Ball Visual (Scale 0.32)", PrimitiveType.Sphere, ball.transform,
                Vector3.zero, Vector3.one * 0.32f, blueMaterial, false);

            GameObject holeCenter = new GameObject("Hole Center - Cup Assist Target");
            holeCenter.transform.SetParent(holeGroup, false);
            holeCenter.transform.localPosition = holePosition;
            level.holeCenter = holeCenter.transform;

            GameObject rim = CreatePrimitive("Cream Gold Cup Rim (Decorative No Collider)", PrimitiveType.Cylinder, holeGroup,
                holePosition + Vector3.up * 0.018f, new Vector3(1.02f, 0.018f, 1.02f), goldMaterial, false);
            rim.transform.localScale = new Vector3(1.02f, 0.018f, 1.02f);
            GameObject cup = CreatePrimitive("Dark Circular Cup (Decorative No Collider)", PrimitiveType.Cylinder, holeGroup,
                holePosition + Vector3.up * 0.043f, new Vector3(0.76f, 0.014f, 0.76f), cupMaterial, false);
            cup.transform.localScale = new Vector3(0.76f, 0.014f, 0.76f);

            InstantiateKenney("flag-blue", "Visible Kenney Flag", holeGroup,
                holePosition + new Vector3(0.24f, 0.025f, 0.04f), Vector3.zero, Vector3.one * 1.62f, kenneyMaterial);
        }

        private static void ConfigureWindmill(GameObject windmill)
        {
            foreach (MeshFilter meshFilter in windmill.GetComponentsInChildren<MeshFilter>(true))
            {
                if (meshFilter.gameObject.name.Equals("blades", StringComparison.OrdinalIgnoreCase))
                {
                    meshFilter.gameObject.AddComponent<WindmillRotor>();
                    CreateBladeCollider("Blade Collider - Rising Diagonal", meshFilter.transform,
                        new Vector3(1.85f, 0.20f, 0.10f), 45f);
                    CreateBladeCollider("Blade Collider - Falling Diagonal", meshFilter.transform,
                        new Vector3(1.85f, 0.20f, 0.10f), -45f);
                }
            }

            // The decorative Kenney base has a border across its opening, so it is intentionally
            // sunk below the green and left non-colliding. Two narrow tower colliders preserve
            // believable side impacts while leaving a generous, clean center passage.
            CreateWindmillTowerCollider("Left Windmill Tower Collider", windmill.transform, -0.39f);
            CreateWindmillTowerCollider("Right Windmill Tower Collider", windmill.transform, 0.39f);
        }

        private static void CreateWindmillTowerCollider(string name, Transform parent, float localX)
        {
            GameObject colliderObject = new GameObject(name);
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localPosition = new Vector3(localX, 0.42f, 0f);
            BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.24f, 0.9f, 0.5f);
            collider.sharedMaterial = ballPhysicsMaterial;
        }

        private static void CreateTunnelSideGrass(Transform parent, string tunnelName,
            float leftOuter, float leftInner, float rightInner, float rightOuter, float localZ)
        {
            const float bankHeight = 0.54f;
            const float bankDepth = 1.2f;
            float bankY = 0.1f + bankHeight * 0.5f;

            CreatePrimitive(tunnelName + " Left Grass Bank", PrimitiveType.Cube, parent,
                new Vector3((leftOuter + leftInner) * 0.5f, bankY, localZ),
                new Vector3(leftInner - leftOuter, bankHeight, bankDepth), greenMaterial, true);
            CreatePrimitive(tunnelName + " Right Grass Bank", PrimitiveType.Cube, parent,
                new Vector3((rightInner + rightOuter) * 0.5f, bankY, localZ),
                new Vector3(rightOuter - rightInner, bankHeight, bankDepth), greenMaterial, true);
        }

        private static void CreateBladeCollider(string name, Transform parent, Vector3 size, float localZRotation)
        {
            GameObject colliderObject = new GameObject(name);
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localRotation = Quaternion.Euler(0f, 0f, localZRotation);
            BoxCollider collider = colliderObject.AddComponent<BoxCollider>();
            collider.size = size;
            collider.sharedMaterial = ballPhysicsMaterial;
        }

        private static void AddNonConvexMeshColliders(GameObject model)
        {
            foreach (MeshFilter meshFilter in model.GetComponentsInChildren<MeshFilter>(true))
            {
                MeshCollider collider = meshFilter.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = meshFilter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
                collider.convex = false;
                collider.sharedMaterial = ballPhysicsMaterial;
            }
        }

        private static void AddTeeMarkers(Transform levelRoot, Vector3 center)
        {
            Transform course = FindDirectChild(levelRoot, "COURSE");
            CreatePrimitive("Tee Marker Left", PrimitiveType.Cylinder, course, center + new Vector3(-0.46f, 0f, 0f), new Vector3(0.15f, 0.045f, 0.15f), goldMaterial, false);
            CreatePrimitive("Tee Marker Right", PrimitiveType.Cylinder, course, center + new Vector3(0.46f, 0f, 0f), new Vector3(0.15f, 0.045f, 0.15f), goldMaterial, false);
        }

        private static void CreateDecorativePlant(Transform parent, Vector3 position)
        {
            GameObject stem = CreatePrimitive("Plant Stem", PrimitiveType.Cylinder, parent, position, new Vector3(0.12f, 0.28f, 0.12f), railMaterial, false);
            stem.transform.localScale = new Vector3(0.12f, 0.28f, 0.12f);
            CreatePrimitive("Plant Crown", PrimitiveType.Sphere, parent, position + Vector3.up * 0.44f, new Vector3(0.5f, 0.45f, 0.5f), greenMaterial, false);
        }

        private static GameObject InstantiateKenney(string assetName, string hierarchyName, Transform parent, Vector3 localPosition, Vector3 localEuler, Vector3 localScale, Material material)
        {
            string path = KenneyModelFolder + "/" + assetName + ".fbx";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                throw new InvalidOperationException("Missing Kenney model: " + path);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = hierarchyName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localEulerAngles = localEuler;
            instance.transform.localScale = localScale;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return instance;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool keepCollider)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localRotation = Quaternion.identity;
            gameObject.transform.localScale = localScale;
            Renderer renderer = gameObject.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            Collider collider = gameObject.GetComponent<Collider>();
            if (!keepCollider && collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            else if (collider != null)
                collider.sharedMaterial = ballPhysicsMaterial;
            return gameObject;
        }

        private static Transform NewGroup(string name, Transform parent)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent, false);
            return group.transform;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child == null)
                throw new InvalidOperationException("Missing authored group " + name + " under " + parent.name);
            return child;
        }

        private static Color Hex(string html)
        {
            if (!ColorUtility.TryParseHtmlString("#" + html, out Color color))
                return Color.magenta;
            return color;
        }
    }
}
