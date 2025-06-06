using UnityEngine;
using UnityEngine.UI;

public class RotationController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private InputField _rotationInput; 

    [Header("Settings")]
    [SerializeField] private float _rotationSpeed = 30f;

    private float _currentAngle = 0f;
    private ExternalModelLoader _modelLoader;

    void Start()
    {
        // Настройка валидации ввода
        _rotationInput.contentType = InputField.ContentType.DecimalNumber;
        _rotationInput.onValueChanged.AddListener(ValidateInput);
        _rotationInput.onEndEdit.AddListener(ValidateFinalInput);
    }

    // Валидация вводимых значений
    private void ValidateInput(string value)
    {
        if (float.TryParse(value, out float number))
        {
            _currentAngle = Mathf.Clamp(number, 0f, 360f);
            _rotationInput.text = _currentAngle.ToString("F1");
        }
        else if (!string.IsNullOrEmpty(value))
        {
            _rotationInput.text = _currentAngle.ToString("F1");
        }
    }

    public float ValidInput(string value)
    {
        if (float.TryParse(value, out float number))
        {
            _currentAngle = Mathf.Clamp(number, 0f, 360f);
            return _currentAngle;
        }
        return 0;
    }

    // Методы вращения 
    public void RotateX(){ if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.Rotate(ValidInput(_rotationInput.text), 0, 0); }
    public void RotateY(){ if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.Rotate(0, ValidInput(_rotationInput.text), 0); }
    public void RotateZ(){ if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.Rotate(0, 0, ValidInput(_rotationInput.text)); }
    public void RotateMX(){ if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.Rotate(-ValidInput(_rotationInput.text), 0, 0); }
    public void RotateMY(){ if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.Rotate(0, -ValidInput(_rotationInput.text), 0);}
    public void RotateMZ(){ if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.Rotate(0, 0, -ValidInput(_rotationInput.text)); }

    public void ResetRotation()
    {
        // Сбрасываем вращение объекта
        
        if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.rotation = Quaternion.identity;
        

        // Сбрасываем текстовое поле
        if (_rotationInput != null)
        {
            _currentAngle = 0f;
            _rotationInput.text = "0";
            _rotationInput.GetComponent<InputField>().text = "0"; // Двойное обновление для разных версий Unity
        }

        // Дополнительный сброс значений для плавных вращений
        StopAllCoroutines(); // Если используется корутина для плавного вращения
    }

    public void UpdateRotation()
    {
        if (float.TryParse(_rotationInput.text, out float angle))
        {
            if (GameObject.Find("ObjModel")) GameObject.Find("ObjModel").transform.rotation = Quaternion.Euler(angle, angle, angle);
            _modelLoader._loadedModel = (GameObject.Find("ObjModel"));
        }
    }

    private void ValidateFinalInput(string value)
    {
        if (string.IsNullOrEmpty(value))
            _rotationInput.text = "0";
    }
}
