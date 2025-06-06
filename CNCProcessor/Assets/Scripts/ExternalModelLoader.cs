using UnityEngine;
using UnityEngine.UI;
using SFB;
using Dummiesman;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Collections;

public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();

    public static MainThreadDispatcher Instance => _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (_instance == null)
        {
            _instance = new GameObject("MainThreadDispatcher").AddComponent<MainThreadDispatcher>();
            DontDestroyOnLoad(_instance.gameObject);
        }
    }

    void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                try
                {
                    _executionQueue.Dequeue()?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"MainThreadDispatcher error: {ex}");
                }
            }
        }
    }

    public static void RunOnMainThread(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
}

public class ExternalModelLoader : MonoBehaviour
{
    [Header("References")]
    public GameObject globalVolume;
    public Text statusText;
    public Text logText;
    public Camera mainCamera;
    public Light[] sceneLights;
    public float distanceMultiplier = 1.5f;
    public Material defaultMaterial;

    [Header("Settings")]
    [SerializeField] private int maxLogEntries = 100;

    private byte[] _objFileData;
    public GameObject _loadedModel;
    private Vector3 _modelCenter;
    private bool _isLoading = false;
    private List<string> _logEntries = new List<string>();

    [Header("Rotation Settings")]
    public float rotationSpeedX = 0f; // Скорость вращения по оси X
    public float rotationSpeedY = 0f; // Скорость вращения по оси Y
    public float rotationSpeedZ = 0f; // Скорость вращения по оси Z
    public bool useWorldSpace = false; // Использовать мировые координаты

    [Header("3D Processing")]
    public CNC3DProcessor cnc3DProcessor;
    public Button generate3DButton;

    void Start()
    {
        //Application.logMessageReceived += HandleUnityLog;
        InitializeLogSystem();

        if (MainThreadDispatcher.Instance == null)
        {
            Debug.LogError("MainThreadDispatcher not initialized!");
        }
    }

    private void OnGenerate3DClick()
    {
        if (_loadedModel == null)
        {
            WriteToLog("No model loaded", "ERROR");
            return;
        }
        cnc3DProcessor.Generate3DGCode();
    }

    private void InitializeLogSystem()
    {
        try
        {
            _logEntries.Clear();
            WriteToLog("Application started", "INFO");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Log initialization failed: {ex}");
        }
    }

    public void WriteToLog(string message, string type = "INFO")
    {
        var logEntry = $"[{DateTime.Now:HH:mm:ss.fff}] [{type}] {message}";
        _logEntries.Add(logEntry);

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            try
            {
                // Обновление UI
                if (logText != null)
                {
                    logText.text = logEntry;
                    Debug.Log(logEntry);
                }

                // Сохранение в файл
                File.AppendAllText(Path.Combine(Application.persistentDataPath, "app_log.txt"), logEntry + "\n");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Log write error: {ex}");
            }
        });
    }

    public void OnLoadButtonClick()
    {
        if (_isLoading) return;
        StartCoroutine(LoadModelCoroutine());
    }

    private IEnumerator LoadModelCoroutine()
    {
        _isLoading = true;
        statusText.text = "Загрузка...";
        WriteToLog("Starting model loading process", "INFO");

        try
        {
            // Выбор файла
            var paths = StandaloneFileBrowser.OpenFilePanel("Выберите модель", "", "obj", false);
            if (paths.Length == 0)
            {
                WriteToLog("File selection canceled", "INFO");
                yield break;
            }

            var objPath = paths[0];
            WriteToLog($"Selected file: {objPath}", "INFO");

            // Загрузка данных
            byte[] fileData = null;
            yield return ReadFileAsync(objPath, data => fileData = data);

            if (fileData == null || fileData.Length == 0)
            {
                throw new Exception("Failed to read file");
            }

            // Создание модели
            GameObject model = null;
            yield return CreateModelAsync(fileData, createdModel => model = createdModel);

            if (model == null)
            {
                throw new Exception("Model creation failed");
            }

            // Настройка модели
            yield return PositionAndConfigureModel(model);

            statusText.text = "Готово!";
            WriteToLog("Model loading completed", "INFO");
            model = null;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private IEnumerator ReadFileAsync(string path, Action<byte[]> callback)
    {
        byte[] result = null;
        Exception error = null;

        Task.Run(() =>
        {
            try
            {
                result = File.ReadAllBytes(path);
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        while (result == null && error == null)
        {
            yield return null;
        }

        if (error != null)
        {
            throw error;
        }

        callback?.Invoke(result);
    }

    private IEnumerator CreateModelAsync(byte[] data, Action<GameObject> callback)
    {
        GameObject model = null;
        Exception error = null;

        MainThreadDispatcher.RunOnMainThread(() =>
        {
            try
            {
                using (var stream = new MemoryStream(data))
                {
                    model = new OBJLoader().Load(stream);
                    
                    if (model != null)
                    {
                        ApplyModelSettings(model);
                    }
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        while (model == null && error == null)
        {
            yield return null;
        }

        if (error != null)
        {
            throw error;
        }

        callback?.Invoke(model);
    }

    private IEnumerator PositionAndConfigureModel(GameObject model)
    {
        // Уничтожение предыдущей модели
        if (_loadedModel != null)
        {
            Destroy(_loadedModel);
            yield return null; // Даем кадр для уничтожения объекта
        }

        _loadedModel = model;

        Vector3 rotation = new Vector3(
            rotationSpeedX,
            rotationSpeedY,
            rotationSpeedZ
            );

        

        // Позиционирование
        PositionModel(model);
        yield return null;

        // Настройка камеры
        CalculateModelDimensions(out Vector3 size, out _modelCenter);
        AdjustCameraAndLight(size);
    }

    void PositionModel(GameObject model)
    {
        try
        {
            WriteToLog("Starting model positioning", "DEBUG");
            if (globalVolume == null)
            {
                WriteToLog("Global Volume not assigned", "ERROR");
                throw new NullReferenceException("Global Volume не назначен");
            }

            var volumeRenderer = globalVolume.GetComponent<Renderer>();
            if (volumeRenderer == null)
            {
                WriteToLog("Global Volume has no Renderer", "ERROR");
                throw new MissingComponentException("Нет компонента Renderer");
            }

            Vector3 rotation = new Vector3(
            rotationSpeedX,
            rotationSpeedY,
            rotationSpeedZ
            );

             // Применяем вращение к объекту
            

            Vector3 targetPosition = new Vector3(
                volumeRenderer.bounds.max.x,
                volumeRenderer.bounds.min.y,
                volumeRenderer.bounds.min.z
            );

            var modelRenderer = model.GetComponent<Renderer>();
            if (modelRenderer != null)
            {
                targetPosition -= new Vector3(
                    modelRenderer.bounds.extents.x,
                    -modelRenderer.bounds.extents.y,
                    modelRenderer.bounds.extents.z
                );
            }
            model.transform.position = targetPosition;
            model.transform.Rotate(
            rotation,
            useWorldSpace ? Space.World : Space.Self
            );
            model.name = "ObjModel";
            WriteToLog($"Model positioned at: {targetPosition}", "INFO");
        }
        catch (Exception ex)
        {
            WriteToLog($"Positioning failed: {ex.Message}", "ERROR");
        }
    }

    void CalculateModelDimensions(out Vector3 size, out Vector3 center)
    {
        try
        {
            WriteToLog("Calculating model dimensions", "DEBUG");
            Renderer modelRenderer = _loadedModel.GetComponentInChildren<Renderer>();
            if (modelRenderer == null)
            {
                WriteToLog("No renderer found for model", "WARNING");
                size = Vector3.one;
                center = Vector3.zero;
                return;
            }

            Bounds bounds = modelRenderer.bounds;
            size = bounds.size;
            center = bounds.center;
            WriteToLog($"Model dimensions: {size}, Center: {center}", "DEBUG");
        }
        catch (Exception ex)
        {
            WriteToLog($"Dimension calculation error: {ex.Message}", "ERROR");
            size = Vector3.one;
            center = Vector3.zero;
        }
    }

    void ApplyModelSettings(GameObject model)
    {
        try
        {
            WriteToLog("Applying model settings", "DEBUG");
            var renderers = model.GetComponentsInChildren<Renderer>();
            WriteToLog($"Found {renderers.Length} renderers", "DEBUG");

            // 1. Проверка и создание fallback шейдера
            Shader GetFallbackShader()
            {
                var shader = Shader.Find("Standard");
                if (shader == null)
                {
                    WriteToLog("Critical: Standard shader missing! Using ErrorShader", "FATAL");
                    shader = Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("UI/Default");
                }
                return shader;
            }

            // 2. Создание безопасного материала
            Material CreateSafeMaterial(Material original)
            {
                try
                {
                    if (original != null && original.shader != null)
                    {
                        return new Material(original);
                    }
                }
                catch { }

                WriteToLog($"Creating fallback material for shader: {original?.shader?.name}", "WARNING");
                return new Material(GetFallbackShader())
                {
                    color = Color.magenta,
                    name = "FallbackMaterial"
                };
            }

            // 3. Основной цикл применения материалов
            foreach (var renderer in renderers)
            {
                try
                {
                    var newMaterials = new Material[renderer.sharedMaterials.Length];

                    // Если назначен defaultMaterial
                    if (defaultMaterial != null)
                    {
                        for (int i = 0; i < newMaterials.Length; i++)
                        {
                            newMaterials[i] = CreateSafeMaterial(defaultMaterial);
                        }
                    }
                    // Если defaultMaterial не назначен
                    else
                    {
                        for (int i = 0; i < newMaterials.Length; i++)
                        {
                            var originalMat = renderer.sharedMaterials[i];
                            newMaterials[i] = CreateSafeMaterial(originalMat);
                        }
                    }

                    renderer.sharedMaterials = newMaterials;
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
                catch (Exception ex)
                {
                    WriteToLog($"Renderer error: {ex.Message}", "ERROR");
                }
            }

            WriteToLog("Materials applied successfully", "INFO");
        }
        catch (Exception ex)
        {
            WriteToLog($"Model settings error: {ex.Message}", "ERROR");
        }
    }

    void AdjustCameraAndLight(Vector3 modelSize)
    {
        try
        {
            WriteToLog("Adjusting camera and lights", "DEBUG");
            float maxDimension = Mathf.Max(modelSize.x, modelSize.y, modelSize.z);
            float targetDistance = maxDimension * distanceMultiplier;

            Vector3 direction = mainCamera.transform.position - _modelCenter;
            direction = direction.normalized;

            Vector3 newCameraPosition = _modelCenter + direction * targetDistance;
            mainCamera.transform.position = newCameraPosition;
            WriteToLog($"Camera position updated: {newCameraPosition}", "INFO");

            mainCamera.fieldOfView = Mathf.Clamp(60 - maxDimension * 0.5f, 20, 100);
            WriteToLog($"Camera FOV set to: {mainCamera.fieldOfView}", "DEBUG");

            foreach (Light light in sceneLights)
            {
                if (light.type == LightType.Directional)
                {
                    light.intensity = Mathf.Clamp(maxDimension * 0.8f, 0.5f, 5f);
                    WriteToLog($"Directional light intensity: {light.intensity}", "DEBUG");
                }
                else
                {
                    light.transform.position = _modelCenter + Vector3.up * targetDistance;
                    light.range = targetDistance * 2f;
                    WriteToLog($"Point light position: {light.transform.position}", "DEBUG");
                }
            }

            mainCamera.transform.LookAt(_modelCenter);
            WriteToLog("Camera focus adjusted to model center", "INFO");
        }
        catch (Exception ex)
        {
            WriteToLog($"Camera/light adjustment failed: {ex.Message}", "ERROR");
        }
    }

    private void HandleUnityLog(string condition, string stackTrace, LogType type)
    {
        var logType = type switch
        {
            LogType.Error => "ERROR",
            LogType.Assert => "DEBUG",
            LogType.Warning => "WARNING",
            LogType.Log => "INFO",
            LogType.Exception => "EXCEPTION",
            _ => "UNKNOWN"
        };

        WriteToLog($"{condition}\n{stackTrace}", logType);
    }

    void OnDestroy()
    {
        //Application.logMessageReceived -= HandleUnityLog;
    }
}