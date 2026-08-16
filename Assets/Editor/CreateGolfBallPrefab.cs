using UnityEngine;
using UnityEditor;
public static class CreateGolfBallPrefab
{
    [MenuItem("Minimal Golf/Create GolfBall Prefab")]
    public static void Create()
    {
        var go = new GameObject("Golf Ball");
        var rb = go.AddComponent<Rigidbody>();
        rb.mass = 0.45f;
        rb.linearDamping = 0.65f;
        rb.angularDamping = 0.9f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.maxAngularVelocity = 18f;
        var col = go.AddComponent<SphereCollider>();
        col.radius = 0.2f;
        var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/MinimalGolf/Physics/Playful Ball Physics.asset");
        col.sharedMaterial = mat;
        go.AddComponent<MinimalGolf.GolfBallImpact>();
        var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "Visual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.32f;
        var c = visual.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c);
        var blueMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/MinimalGolf/Materials/Ball Blue.mat");
        var rend = visual.GetComponent<Renderer>();
        if (rend != null && blueMat != null) rend.sharedMaterial = blueMat;
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        rend.receiveShadows = false;
        string path = "Assets/MinimalGolf/Prefabs/GolfBall.prefab";
        System.IO.Directory.CreateDirectory("Assets/MinimalGolf/Prefabs");
        PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        Debug.Log("[CreateGolfBallPrefab] Created " + path);
        AssetDatabase.Refresh();
    }
}
