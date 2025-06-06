using UnityEngine;

public class ShowPanel : MonoBehaviour
{
    [SerializeField] private GameObject _panel; // Ссылка на панель в инспекторе

    // Показывает панель
    public void Show()
    {
        SetPanelState(true);
    }

    // Скрывает панель
    public void Hide()
    {
        SetPanelState(false);
    }

    // Переключает видимость панели
    public void TogglePanel()
    {
        if (_panel != null)
        {
            SetPanelState(!_panel.activeSelf);
        }
    }

    // Устанавливает состояние панели с проверкой на null
    private void SetPanelState(bool state)
    {
        if (_panel != null)
        {
            _panel.SetActive(state);
        }
        else
        {
            Debug.LogWarning("Panel reference is missing!");
        }
    }

    // Свойство для проверки текущего состояния панели
    public bool IsPanelVisible
    {
        get
        {
            if (_panel != null)
            {
                return _panel.activeSelf;
            }
            return false;
        }
    }
}

