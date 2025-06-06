using UnityEngine;

public class ShowPanel : MonoBehaviour
{
    [SerializeField] private GameObject _panel; // ������ �� ������ � ����������

    // ���������� ������
    public void Show()
    {
        SetPanelState(true);
    }

    // �������� ������
    public void Hide()
    {
        SetPanelState(false);
    }

    // ����������� ��������� ������
    public void TogglePanel()
    {
        if (_panel != null)
        {
            SetPanelState(!_panel.activeSelf);
        }
    }

    // ������������� ��������� ������ � ��������� �� null
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

    // �������� ��� �������� �������� ��������� ������
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

