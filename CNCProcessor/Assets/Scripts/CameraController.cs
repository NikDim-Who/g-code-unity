using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Перемещение")]
    public float moveSpeed = 25f;

    [Header("Вращение")]
    public float rotationSpeed = 50f;
    public KeyCode rotateKey = KeyCode.Mouse1; // Правая кнопка мыши
    public float maxVerticalAngle = 360f;

    [Header("Зум")]
    public float zoomSpeed = 30f;
    public float minZoom = 5f;
    public float maxZoom = 100f;

    [Header("Горячие клавиши")]
    public KeyCode resetKey = KeyCode.Home;
    public KeyCode toggleRotationKey = KeyCode.R;

    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private float currentZoom;
    private bool rotationEnabled = true;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        currentZoom = -Camera.main.transform.localPosition.z;
    }

    void Update()
    {
        HandleMovement();
        HandleZoom();

        if (rotationEnabled) HandleRotation();

        if (Input.GetKeyDown(resetKey)) ResetCamera();
        if (Input.GetKeyDown(toggleRotationKey)) ToggleRotation();
    }

    void HandleMovement()
    {
        // Перемещение стрелками
        Vector3 move = new Vector3(
            Input.GetAxis("Horizontal"),
            0,
            Input.GetAxis("Vertical")
        ) * moveSpeed * Time.deltaTime;

        transform.Translate(move, Space.Self);
    }

    void HandleRotation()
    {
        if (Input.GetKey(rotateKey))
        {
            // Горизонтальное вращение
            float mouseX = Input.GetAxis("Mouse X") * rotationSpeed;
            // Вертикальное вращение с ограничением
            float mouseY = -Input.GetAxis("Mouse Y") * rotationSpeed;

            Vector3 currentRotation = transform.eulerAngles;
            currentRotation.x += mouseY;
            currentRotation.y += mouseX;

            transform.eulerAngles = currentRotation;
        }
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        currentZoom = Mathf.Clamp(
            currentZoom - scroll * zoomSpeed,
            minZoom,
            maxZoom
        );
        Camera.main.transform.localPosition = new Vector3(0, 0, -currentZoom);
    }

    void ResetCamera()
    {
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        currentZoom = -Camera.main.transform.localPosition.z;
    }

    void ToggleRotation()
    {
        rotationEnabled = !rotationEnabled;
    }
}