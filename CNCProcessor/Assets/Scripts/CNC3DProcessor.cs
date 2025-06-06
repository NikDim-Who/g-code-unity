using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System;
using System.IO;

public class CNC3DProcessor : MonoBehaviour
{
    [Header("UI Settings")]
    public InputField gcodeOutput;
    public InputField toolDiameterInput;
    public InputField stepOverInput;
    public InputField stepDownInput;
    public InputField spindleSpeedInput;
    public InputField safeHeightInput;
    public Dropdown strategyDropdown;
    public Toggle useToolCompensation;
    public Dropdown postProcessorDropdown;
    public Slider qualitySlider;
    public Material previewMaterial;
    public Button generateButton;
    public Button exportButton;

    [Header("Processing Parameters")]
    [SerializeField] private float _feedRate = 1000f;
    [SerializeField] private float _plungeRate = 300f;

    private Mesh _targetMesh;
    private List<Vector3> _vertices = new List<Vector3>();
    private Bounds _meshBounds;
    private List<Vector3> _generatedPath = new List<Vector3>();
    private List<GameObject> _previewObjects = new List<GameObject>();
    public ExternalModelLoader _modelLoader;
    
    public static CNC3DProcessor instance;
    public enum ProcessingStrategy { Parallel, Spiral, Contour }
    public enum PostProcessorType { GRBL, Mach3, LinuxCNC }

    public struct GCodeSettings
    {
        public float toolDiameter;
        public float stepOver;
        public float stepDown;
        public int spindleSpeed;
        public float safeHeight;
        public ProcessingStrategy strategy;
    }

    private struct Intersection
    {
        public Vector3 start;
        public Vector3 end;

        public Intersection(Vector3 a, Vector3 b)
        {
            start = a;
            end = b;
        }
    }

    void Start()
    {
        strategyDropdown.ClearOptions();
        strategyDropdown.AddOptions(Enum.GetNames(typeof(ProcessingStrategy)).ToList());
        generateButton.onClick.AddListener(Generate3DGCode);
        exportButton.onClick.AddListener(ExportGCode);
        _modelLoader = FindObjectOfType<ExternalModelLoader>();
    }

    private Mesh GetModelMesh()
    {
        MeshFilter meshFilter = _modelLoader._loadedModel.GetComponentInChildren<MeshFilter>();
        return meshFilter != null ? meshFilter.sharedMesh : null;
    }

    public void Initialize()
    {
        if (instance == null)
        {
            instance = new GameObject("CNC3DProcessor").AddComponent<CNC3DProcessor>();
            DontDestroyOnLoad(instance.gameObject);
        }
        _targetMesh = GetModelMesh();
        _targetMesh.RecalculateBounds();
        _meshBounds = _targetMesh.bounds;
        ProcessMeshData();

    }

    private void ProcessMeshData()
    {
        _vertices.Clear();
        foreach (Vector3 vertex in _targetMesh.vertices)
        {
            Vector3 scaledVertex = ScaleVertex(vertex);
            _vertices.Add(scaledVertex);
        }
    }

    public Vector3 ScaleVertex(Vector3 vertex)
    {
        return new Vector3(
            vertex.x * 1000f,
            vertex.z * 1000f,
            vertex.y * 1000f
        );
    }

    public void Generate3DGCode()
    {
        Initialize();

        if (!ValidateParameters() || _targetMesh == null) return;

        GCodeSettings settings = new GCodeSettings
        {
            toolDiameter = float.Parse(toolDiameterInput.text),
            stepOver = float.Parse(stepOverInput.text),
            stepDown = float.Parse(stepDownInput.text),
            spindleSpeed = int.Parse(spindleSpeedInput.text),
            safeHeight = float.Parse(safeHeightInput.text),
            strategy = (ProcessingStrategy)strategyDropdown.value
        };

        StringBuilder gcode = new StringBuilder();
        GenerateHeader(gcode, settings);

        switch (settings.strategy)
        {
            case ProcessingStrategy.Parallel:
                GenerateParallelPaths(gcode, settings);
                break;
            case ProcessingStrategy.Spiral:
                GenerateSpiralPaths(gcode, settings);
                break;
            case ProcessingStrategy.Contour:
                GenerateContourPaths(gcode, settings);
                break;
        }

        GenerateFooter(gcode);
        gcodeOutput.text = gcode.ToString();
        VisualizePath();
    }

    private void GenerateHeader(StringBuilder sb, GCodeSettings settings)
    {
        sb.AppendLine("G21 ; Metric units");
        sb.AppendLine("G90 ; Absolute positioning");
        sb.AppendLine($"S{settings.spindleSpeed} M3 ; Spindle start");
        sb.AppendLine($"G0 Z{settings.safeHeight:F2} ; Safe height");
    }

    private void GenerateParallelPaths(StringBuilder sb, GCodeSettings settings)
    {
        float currentZ = _meshBounds.min.y;
        float toolRadius = settings.toolDiameter / 2f;

        while (currentZ < _meshBounds.max.y)
        {
            sb.AppendLine($"; Layer Z={currentZ:F2}");
            List<Vector3> layerPoints = GetLayerContours(currentZ, toolRadius);
            ProcessLayer(sb, layerPoints, currentZ, settings);
            currentZ += settings.stepDown;
        }
    }

    private List<Vector3> GetLayerContours(float zHeight, float toolRadius)
    {
        List<Intersection> intersections = new List<Intersection>();

        for (int i = 0; i < _targetMesh.triangles.Length; i += 3)
        {
            Vector3 v1 = _vertices[_targetMesh.triangles[i]];
            Vector3 v2 = _vertices[_targetMesh.triangles[i + 1]];
            Vector3 v3 = _vertices[_targetMesh.triangles[i + 2]];

            FindIntersections(v1, v2, v3, zHeight, ref intersections);
        }

        return ProcessIntersections(intersections, toolRadius);
    }

    private void FindIntersections(Vector3 a, Vector3 b, Vector3 c, float z, ref List<Intersection> intersections)
    {
        List<Vector3> points = new List<Vector3>();

        if (Mathf.Sign(a.z - z) != Mathf.Sign(b.z - z))
            points.Add(IntersectZPlane(a, b, z));

        if (Mathf.Sign(b.z - z) != Mathf.Sign(c.z - z))
            points.Add(IntersectZPlane(b, c, z));

        if (Mathf.Sign(c.z - z) != Mathf.Sign(a.z - z))
            points.Add(IntersectZPlane(c, a, z));

        if (points.Count == 2)
            intersections.Add(new Intersection(points[0], points[1]));
    }

    private Vector3 IntersectZPlane(Vector3 p1, Vector3 p2, float z)
    {
        float t = (z - p1.z) / (p2.z - p1.z);
        return new Vector3(
            p1.x + t * (p2.x - p1.x),
            p1.y + t * (p2.y - p1.y),
            z
        );
    }

    private List<Vector3> ProcessIntersections(List<Intersection> intersections, float toolRadius)
    {
        List<Vector3> path = new List<Vector3>();

        while (intersections.Count > 0)
        {
            var current = intersections[0];
            intersections.RemoveAt(0);

            path.Add(current.start);
            path.Add(current.end);

            for (int i = 0; i < intersections.Count; i++)
            {
                if (Vector3.Distance(current.end, intersections[i].start) < 0.01f)
                {
                    current = intersections[i];
                    intersections.RemoveAt(i);
                    path.Add(current.end);
                    i = -1;
                }
            }
        }

        return useToolCompensation.isOn ?
            ApplyToolCompensation(path, toolRadius) :
            path;
    }

    private List<Vector3> ApplyToolCompensation(List<Vector3> path, float radius)
    {
        List<Vector3> offsetPath = new List<Vector3>();

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 prev = path[(i - 1 + path.Count) % path.Count];
            Vector3 current = path[i];
            Vector3 next = path[(i + 1) % path.Count];

            Vector3 dir1 = (current - prev).normalized;
            Vector3 dir2 = (next - current).normalized;

            Vector3 normal = Vector3.Cross(dir1 + dir2, Vector3.up).normalized;
            offsetPath.Add(current + normal * radius);
        }

        return offsetPath;
    }

    private void GenerateSpiralPaths(StringBuilder sb, GCodeSettings settings)
    {
        float currentZ = _meshBounds.min.y;
        Vector3 center = _meshBounds.center;
        float radius = 0f;
        float maxRadius = _meshBounds.extents.magnitude;

        while (currentZ < _meshBounds.max.y)
        {
            sb.AppendLine($"; Spiral layer Z={currentZ:F2}");
            List<Vector3> spiralPoints = GenerateSpiralPoints(center, radius, settings.stepOver);
            ProcessLayer(sb, spiralPoints, currentZ, settings);

            radius += settings.stepOver;
            currentZ += settings.stepDown;

            if (radius > maxRadius) break;
        }
    }

    private List<Vector3> GenerateSpiralPoints(Vector3 center, float radius, float step)
    {
        List<Vector3> points = new List<Vector3>();
        float angle = 0f;

        while (angle < 360 * (radius / step))
        {
            float r = radius + angle / 360 * step;
            points.Add(new Vector3(
                center.x + r * Mathf.Cos(angle * Mathf.Deg2Rad),
                center.y + r * Mathf.Sin(angle * Mathf.Deg2Rad),
                center.z
            ));
            angle += 1f;
        }

        return points;
    }

    private void GenerateContourPaths(StringBuilder sb, GCodeSettings settings)
    {
        // Реализация контурной стратегии
        // (Аналогично параллельной, но с оптимизацией пути)
    }

    private void ProcessLayer(StringBuilder sb, List<Vector3> points, float depth, GCodeSettings settings)
    {
        if (points.Count == 0) return;

        sb.AppendLine(FormatCommand($"G0 Z{settings.safeHeight:F2}"));
        sb.AppendLine(FormatCommand($"G0 X{points[0].x:F2} Y{points[0].y:F2}"));
        sb.AppendLine(FormatCommand($"G1 Z-{depth:F2} F{_plungeRate}"));

        foreach (Vector3 point in points)
        {
            sb.AppendLine(FormatCommand($"G1 X{point.x:F2} Y{point.y:F2} F{_feedRate}"));
        }

        sb.AppendLine(FormatCommand($"G0 Z{settings.safeHeight:F2}"));
    }

    private string FormatCommand(string command)
    {
        PostProcessorType processor = (PostProcessorType)postProcessorDropdown.value;
        return processor switch
        {
            PostProcessorType.Mach3 => $"{command};",
            _ => command
        };
    }

    private void GenerateFooter(StringBuilder sb)
    {
        sb.AppendLine("M5 ; Spindle stop");
        sb.AppendLine("G0 X0 Y0 ; Return home");
        sb.AppendLine("M30 ; Program end");
    }

    public void VisualizePath()
    {
        ClearPreview();
        _generatedPath = gcodeOutput.text.Split('\n')
            .Where(line => line.StartsWith("G1 X"))
            .Select(line => ParseGCodePosition(line))
            .ToList();

        foreach (var point in _generatedPath)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.position = point / 1000f;
            sphere.transform.localScale = Vector3.one * 0.01f;
            sphere.GetComponent<Renderer>().material = previewMaterial;
            _previewObjects.Add(sphere);
        }
    }

    private Vector3 ParseGCodePosition(string line)
    {
        float x = 0f, y = 0f, z = 0f;
        string[] parts = line.Split(' ');
        foreach (string part in parts)
        {
            if (part.StartsWith("X")) x = float.Parse(part.Substring(1));
            if (part.StartsWith("Y")) y = float.Parse(part.Substring(1));
            if (part.StartsWith("Z")) z = float.Parse(part.Substring(1));
        }
        return new Vector3(x, y, z);
    }

    private void ClearPreview()
    {
        foreach (var obj in _previewObjects)
            Destroy(obj);
        _previewObjects.Clear();
    }

    public void ExportGCode()
    {
        string filename = $"gcode_{DateTime.Now:yyyyMMddHHmmss}.nc";
        string path = Path.Combine(Application.persistentDataPath, filename);
        File.WriteAllText(path, gcodeOutput.text);
        Debug.Log($"G-code exported to: {path}");
    }

    private bool ValidateParameters()
    {
        try
        {
            if (float.Parse(toolDiameterInput.text) <= 0)
                throw new Exception("Tool diameter must be positive");

            if (float.Parse(stepOverInput.text) < 0.1f || float.Parse(stepOverInput.text) > 0.9f)
                throw new Exception("Stepover should be between 0.1 and 0.9");

            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Validation Error: {ex.Message}");
            return false;
        }
    }
}