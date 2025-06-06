using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

public class CNC3DProcessorTests
{
    private CNC3DProcessor processor;
    private GameObject testObject;
    private Mesh testMesh;

    [SetUp]
    public void Setup()
    {
        testObject = new GameObject("TestProcessor");
        processor = testObject.AddComponent<CNC3DProcessor>();

        // Создаем отдельные GameObject для каждого UI компонента
        processor.toolDiameterInput = CreateInputField("1", "ToolDiameter");
        processor.stepOverInput = CreateInputField("0.5", "StepOver");
        processor.spindleSpeedInput = CreateInputField("10000", "SpindleSpeed");
        processor.stepDownInput = CreateInputField("0.2", "StepDown");
        processor.safeHeightInput = CreateInputField("5", "SafeHeight");
        processor.useToolCompensation = CreateToggle(true, "ToolCompToggle");

        // Инициализация Dropdown
        processor.strategyDropdown = CreateDropdown("StrategyDropdown");
        processor.strategyDropdown.AddOptions(
            System.Enum.GetNames(typeof(CNC3DProcessor.ProcessingStrategy)).ToList()
        );

        // Инициализация меша
        testMesh = CreateTestMesh();

        // Мок загрузчика моделей
        processor._modelLoader = testObject.AddComponent<ExternalModelLoader>();
        GameObject modelContainer = new GameObject("ModelContainer");
        processor._modelLoader._loadedModel = modelContainer;
        MeshFilter meshFilter = modelContainer.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = testMesh;

        // Инициализация выпадающего списка постпроцессора
        processor.postProcessorDropdown = CreateDropdown("PostProcessor");
        processor.postProcessorDropdown.AddOptions(
            System.Enum.GetNames(typeof(CNC3DProcessor.PostProcessorType)).ToList()
        );
    }

    private Mesh CreateTestMesh()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(0, 0, 0),
            new Vector3(1, 0, 0),
            new Vector3(0, 1, 0),
            new Vector3(0, 0, 1)
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private InputField CreateInputField(string value, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(testObject.transform);
        InputField field = go.AddComponent<InputField>();
        field.text = value;
        return field;
    }

    private Toggle CreateToggle(bool isOn, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(testObject.transform);
        Toggle toggle = go.AddComponent<Toggle>();
        toggle.isOn = isOn;
        return toggle;
    }

    private Dropdown CreateDropdown(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(testObject.transform);
        return go.AddComponent<Dropdown>();
    }

    [TearDown]
    public void Teardown()
    {
        Object.DestroyImmediate(testObject);
    }

    [Test]
    public void ScaleVertex_ConvertsCorrectly()
    {
        // Arrange
        Vector3 input = new Vector3(0.5f, 0.3f, 0.8f);
        Vector3 expected = new Vector3(500f, 800f, 300f);

        // Act
        Vector3 result = processor.ScaleVertex(input);

        // Assert
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void IntersectZPlane_CalculatesCorrectPoint()
    {
        // Arrange
        Vector3 p1 = new Vector3(0, 0, 0);
        Vector3 p2 = new Vector3(10, 10, 10);
        float zHeight = 5f;
        Vector3 expected = new Vector3(5f, 5f, 5f);

        // Act
        MethodInfo method = typeof(CNC3DProcessor).GetMethod("IntersectZPlane",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Vector3 result = (Vector3)method.Invoke(processor, new object[] { p1, p2, zHeight });

        // Assert
        Assert.AreEqual(expected, result);
    }

    [Test]
    public void ApplyToolCompensation_OffsetsPathCorrectly()
    {
        // Arrange
        List<Vector3> path = new List<Vector3> {
            new Vector3(0, 0, 0),
            new Vector3(10, 0, 0),
            new Vector3(10, 10, 0)
        };
        float radius = 1f;

        // Act
        MethodInfo method = typeof(CNC3DProcessor).GetMethod("ApplyToolCompensation",
            BindingFlags.NonPublic | BindingFlags.Instance);
        List<Vector3> result = (List<Vector3>)method.Invoke(processor, new object[] { path, radius });

        // Assert
        Assert.AreEqual(3, result.Count);

        // Проверяем что точки смещены на указанный радиус
        for (int i = 0; i < result.Count; i++)
        {
            float distance = Vector3.Distance(path[i], result[i]);
            Assert.AreEqual(radius, distance, 0.1f);
        }
    }
}