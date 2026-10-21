using System.Collections.Generic;
using UnityEngine;

public enum RenovationAesthetic { Royal, Gothic, Alien }

[System.Serializable]
public class RenovationOption
{
    public string label;
    public GameObject prefab;
    public RenovationAesthetic aesthetic;
}

public class FurnitureSpot : MonoBehaviour
{
    public static readonly List<FurnitureSpot> ActiveSpots = new List<FurnitureSpot>();
    public static Camera viewCamera;

    public RenovationOption[] options = new RenovationOption[3];
    public Color idleColor = Color.white;
    public Color focusColor = Color.yellow;
    public float outlineScale = 1.01f;
    public float edgeAngle = 30f;
    public float labelHeight = 0.3f;
    public float labelCharacterSize = 0.02f;
    public string promptText = "Press E to interact";
    public string emptyText = "Empty";
    public bool hideOwnMeshWhenModelShown = true;
    public bool useSpotScale = false;

    private class ModelData
    {
        public Mesh mesh;
        public Material[] materials;
    }

    private static readonly Dictionary<Mesh, Mesh> outlineCache = new Dictionary<Mesh, Mesh>();
    private static readonly Dictionary<GameObject, ModelData> modelCache = new Dictionary<GameObject, ModelData>();
    private static readonly HashSet<GameObject> failedPrefabs = new HashSet<GameObject>();
    private static Font labelFont;

    private Transform visualRoot;
    private Transform modelRoot;
    private Renderer[] ownRenderers;
    private MeshFilter modelFilter;
    private MeshRenderer modelRenderer;
    private MeshFilter outlineFilter;
    private MeshRenderer outlineRenderer;
    private Material outlineMaterial;
    private TextMesh label;
    private MeshRenderer labelRenderer;
    private RenovationArea area;
    private int index = 0;
    private const float minAxis = 0.1f;
    private const float minBounds = 0.3f;

    public bool IsActive { get; private set; }

    public RenovationOption CurrentOption
    {
        get { return index >= 0 && index < options.Length ? options[index] : null; }
    }

    public bool HasFurniture
    {
        get { return CurrentOption != null && CurrentOption.prefab != null; }
    }

    public RenovationAesthetic CurrentAesthetic
    {
        get { return CurrentOption.aesthetic; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ActiveSpots.Clear();
        outlineCache.Clear();
        modelCache.Clear();
        failedPrefabs.Clear();
        viewCamera = null;
    }

    void Awake()
    {
        area = GetComponentInParent<RenovationArea>();
        ownRenderers = GetComponentsInChildren<Renderer>(true);
        GameObject vr = new GameObject(name + "_Visuals");
        visualRoot = vr.transform;
        SyncVisualRoot();

        GameObject model = new GameObject("Model");
        modelRoot = model.transform;
        modelRoot.SetParent(visualRoot, false);
        modelFilter = model.AddComponent<MeshFilter>();
        modelRenderer = model.AddComponent<MeshRenderer>();
        modelRenderer.enabled = false;

        GameObject outline = new GameObject("Outline");
        outline.transform.SetParent(modelRoot, false);
        outline.transform.localScale = Vector3.one * outlineScale;
        outlineFilter = outline.AddComponent<MeshFilter>();
        outlineRenderer = outline.AddComponent<MeshRenderer>();
        outlineMaterial = new Material(Shader.Find("Sprites/Default"));
        outlineRenderer.sharedMaterial = outlineMaterial;
        outlineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        outlineRenderer.receiveShadows = false;
        outlineRenderer.enabled = false;

        BuildLabel();
        Refresh();
        SetFocused(false);
    }

    void OnEnable()
    {
        if (IsActive && !ActiveSpots.Contains(this)) ActiveSpots.Add(this);
    }

    void OnDisable()
    {
        ActiveSpots.Remove(this);
    }

    void OnDestroy()
    {
        if (visualRoot != null) Destroy(visualRoot.gameObject);
    }

    void SyncVisualRoot()
    {
        visualRoot.position = transform.position;
        visualRoot.rotation = transform.rotation;
        visualRoot.localScale = useSpotScale ? transform.lossyScale : Vector3.one;
    }

    void LateUpdate()
    {
        SyncVisualRoot();
        if (IsActive && viewCamera != null)
            label.transform.rotation = viewCamera.transform.rotation;
    }

    void BuildLabel()
    {
        if (labelFont == null)
        {
#if UNITY_2022_2_OR_NEWER
            labelFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
#else
            labelFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
#endif
        }

        GameObject go = new GameObject("Label");
        go.transform.SetParent(visualRoot, false);
        if (useSpotScale)
        {
            Vector3 s = transform.lossyScale;
            go.transform.localScale = new Vector3(1f / Mathf.Max(s.x, 1e-4f), 1f / Mathf.Max(s.y, 1e-4f), 1f / Mathf.Max(s.z, 1e-4f));
        }
        label = go.AddComponent<TextMesh>();
        label.font = labelFont;
        label.fontSize = 64;
        label.characterSize = labelCharacterSize;
        label.anchor = TextAnchor.LowerCenter;
        label.alignment = TextAlignment.Center;

        labelRenderer = go.GetComponent<MeshRenderer>();
        labelRenderer.sharedMaterial = labelFont.material;
        labelRenderer.enabled = false;
    }

    public void SetActive(bool value)
    {
        if (IsActive == value) return;
        IsActive = value;

        outlineRenderer.enabled = value && outlineFilter.sharedMesh != null;
        labelRenderer.enabled = value;

        if (value) ActiveSpots.Add(this);
        else ActiveSpots.Remove(this);
    }

    public void SetFocused(bool value)
    {
        Color c = value ? focusColor : idleColor;
        outlineMaterial.color = c;
        label.color = c;
    }

    public void Cycle()
    {
        if (!IsActive || options.Length == 0) return;

        index = (index + 1) % options.Length;
        Refresh();

        if (area != null) area.Evaluate();
    }

    public bool TryRaycast(Ray ray, float maxDistance, out float distance)
    {
        if (!GetWorldBounds().IntersectRay(ray, out distance)) return false;
        return distance <= maxDistance;
    }

    void Refresh()
    {
        ModelData current = HasFurniture ? GetModelData(CurrentOption.prefab) : null;
        ModelData reference = GetReference();

        if (current != null && reference != null)
        {
            modelFilter.sharedMesh = current.mesh;
            modelRenderer.sharedMaterials = current.materials;
            Fit(current.mesh.bounds, reference.mesh.bounds);
            modelRenderer.enabled = true;
        }
        else
        {
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localScale = Vector3.one;
            modelRenderer.enabled = false;
        }

        if (hideOwnMeshWhenModelShown && ownRenderers != null)
        {
            bool hide = modelRenderer.enabled;
            foreach (Renderer r in ownRenderers)
            {
                if (r == null || r == modelRenderer || r == outlineRenderer || r == labelRenderer) continue;
                r.enabled = !hide;
            }
        }

        Mesh shown = current != null ? current.mesh : (reference != null ? reference.mesh : null);
        outlineFilter.sharedMesh = shown != null ? GetOutlineMesh(shown) : null;
        outlineRenderer.enabled = IsActive && outlineFilter.sharedMesh != null;

        RefreshLabel();
    }

    void RefreshLabel()
    {
        RenovationOption option = CurrentOption;
        string name;

        if (option != null && !string.IsNullOrEmpty(option.label)) name = option.label;
        else if (option != null && option.prefab != null) name = option.prefab.name;
        else name = emptyText;

        label.text = name + "\n" + promptText;

        Bounds b = GetWorldBounds();
        label.transform.position = new Vector3(b.center.x, b.max.y + labelHeight, b.center.z);
    }

    ModelData GetReference()
    {
        foreach (RenovationOption o in options)
        {
            if (o == null) continue;
            ModelData data = GetModelData(o.prefab);
            if (data != null) return data;
        }
        return null;
    }

    void Fit(Bounds current, Bounds reference)
    {
        float scale = GetFitScale(current.size, reference.size);
        Vector3 referenceBottom = new Vector3(reference.center.x, reference.min.y, reference.center.z);
        Vector3 currentBottom = new Vector3(current.center.x, current.min.y, current.center.z);

        modelRoot.localScale = Vector3.one * scale;
        modelRoot.localPosition = referenceBottom - currentBottom * scale;
    }

    static float GetFitScale(Vector3 size, Vector3 target)
    {
        float best = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            if (size[i] < minAxis || target[i] < minAxis) continue;
            best = Mathf.Min(best, target[i] / size[i]);
        }
        return best == float.MaxValue ? 1f : best;
    }

    static ModelData GetModelData(GameObject prefab)
    {
        if (prefab == null || failedPrefabs.Contains(prefab)) return null;

        ModelData data;
        if (modelCache.TryGetValue(prefab, out data) && data != null && data.mesh != null) return data;

        List<CombineInstance> combine = new List<CombineInstance>();
        List<Material> materials = new List<Material>();
        Transform root = prefab.transform;
        Matrix4x4 rootScale = Matrix4x4.Scale(root.localScale);

        HashSet<Renderer> allowed = null;
        LODGroup lodGroup = prefab.GetComponentInChildren<LODGroup>(true);
        if (lodGroup != null)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length > 0) allowed = new HashSet<Renderer>(lods[0].renderers);
        }

        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer meshRenderer = filter.GetComponent<MeshRenderer>();
            if (filter.sharedMesh == null || meshRenderer == null) continue;
            if (!meshRenderer.enabled || !filter.gameObject.activeSelf) continue;
            if (meshRenderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly) continue;
            if (allowed != null && !allowed.Contains(meshRenderer)) continue;

            Matrix4x4 matrix = rootScale * root.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Material[] shared = meshRenderer.sharedMaterials;

            for (int sub = 0; sub < filter.sharedMesh.subMeshCount; sub++)
            {
                CombineInstance instance = new CombineInstance();
                instance.mesh = filter.sharedMesh;
                instance.subMeshIndex = sub;
                instance.transform = matrix;
                combine.Add(instance);
                materials.Add(shared.Length > 0 ? shared[Mathf.Min(sub, shared.Length - 1)] : null);
            }
        }

        if (combine.Count == 0)
        {
            Debug.LogWarning("FurnitureSpot: no usable meshes found in prefab " + prefab.name);
            failedPrefabs.Add(prefab);
            return null;
        }

        Mesh combined = new Mesh();
        combined.name = prefab.name + "_Combined";
        combined.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        combined.CombineMeshes(combine.ToArray(), false, true);

        data = new ModelData();
        data.mesh = combined;
        data.materials = materials.ToArray();
        modelCache[prefab] = data;
        return data;
    }

    Bounds GetWorldBounds()
    {
        ModelData reference = GetReference();
        if (reference == null) return new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);

        Bounds local = reference.mesh.bounds;
        Matrix4x4 matrix = Matrix4x4.TRS(transform.position, transform.rotation,
            useSpotScale ? transform.lossyScale : Vector3.one);
        Vector3 c = local.center;
        Vector3 e = local.extents;

        Bounds world = new Bounds(matrix.MultiplyPoint3x4(c - e), Vector3.zero);
        for (int i = 1; i < 8; i++)
        {
            Vector3 corner = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            world.Encapsulate(matrix.MultiplyPoint3x4(corner));
        }

        world.size = Vector3.Max(world.size, Vector3.one * minBounds);
        return world;
    }

    Mesh GetOutlineMesh(Mesh source)
    {
        Mesh result;
        if (outlineCache.TryGetValue(source, out result) && result != null) return result;

        result = BuildOutlineMesh(source, edgeAngle);
        outlineCache[source] = result;
        return result;
    }

    static Mesh BuildOutlineMesh(Mesh source, float angle)
    {
        Vector3[] vertices = source.vertices;
        int[] triangles = source.triangles;

        Dictionary<Vector3Int, int> weld = new Dictionary<Vector3Int, int>();
        List<Vector3> points = new List<Vector3>();
        int[] map = new int[vertices.Length];

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3Int key = new Vector3Int(
                Mathf.RoundToInt(vertices[i].x * 10000f),
                Mathf.RoundToInt(vertices[i].y * 10000f),
                Mathf.RoundToInt(vertices[i].z * 10000f));

            int id;
            if (!weld.TryGetValue(key, out id))
            {
                id = points.Count;
                weld[key] = id;
                points.Add(vertices[i]);
            }
            map[i] = id;
        }

        Dictionary<long, List<Vector3>> edges = new Dictionary<long, List<Vector3>>();

        for (int t = 0; t + 2 < triangles.Length; t += 3)
        {
            int a = map[triangles[t]];
            int b = map[triangles[t + 1]];
            int c = map[triangles[t + 2]];

            Vector3 normal = Vector3.Cross(points[b] - points[a], points[c] - points[a]);
            if (normal.sqrMagnitude < 1e-12f) continue;
            normal.Normalize();

            AddEdge(edges, a, b, normal);
            AddEdge(edges, b, c, normal);
            AddEdge(edges, c, a, normal);
        }

        float cosLimit = Mathf.Cos(angle * Mathf.Deg2Rad);
        List<int> indices = new List<int>();

        foreach (KeyValuePair<long, List<Vector3>> pair in edges)
        {
            List<Vector3> normals = pair.Value;
            bool feature = normals.Count == 1;

            for (int i = 1; i < normals.Count && !feature; i++)
            {
                if (Vector3.Dot(normals[0], normals[i]) < cosLimit) feature = true;
            }

            if (feature)
            {
                indices.Add((int)(pair.Key >> 32));
                indices.Add((int)(pair.Key & 0xFFFFFFFFL));
            }
        }

        Color32[] colors = new Color32[points.Count];
        for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255);

        Mesh outline = new Mesh();
        outline.name = source.name + "_Outline";
        if (points.Count > 65535) outline.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        outline.SetVertices(points);
        outline.colors32 = colors;
        outline.SetIndices(indices.ToArray(), MeshTopology.Lines, 0);
        outline.RecalculateBounds();
        return outline;
    }

    static void AddEdge(Dictionary<long, List<Vector3>> edges, int a, int b, Vector3 normal)
    {
        if (a == b) return;

        int lo = Mathf.Min(a, b);
        int hi = Mathf.Max(a, b);
        long key = ((long)lo << 32) | (long)(uint)hi;

        List<Vector3> list;
        if (!edges.TryGetValue(key, out list))
        {
            list = new List<Vector3>(2);
            edges[key] = list;
        }
        list.Add(normal);
    }
}