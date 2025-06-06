using UnityEngine;
using System.Collections;
using System.IO;
using Dummiesman;
#if UNITY_EDITOR
using UnityEditor;
#else
using System.IO;
#endif

public class SceneWork : MonoBehaviour
{
    public GameObject targetObject;
    public OBJLoader loader = new OBJLoader();

    public void OpenFileDialog()
    {
    #if UNITY_EDITOR
        string path = EditorUtility.OpenFilePanel("�������� OBJ ����", "", "obj");
        if (!string.IsNullOrEmpty(path))
        {
            StartCoroutine(LoadOBJModel(path));
        }
    #endif
    }

    private IEnumerator LoadOBJModel(string filePath)
    {
        // �������� OBJ ��� GameObject (������ ��� ������ Runtime OBJ Loader)
        GameObject loadedModel = loader.Load(filePath);

        // ���������� ������ ������
        if (targetObject != null)
        {
            Destroy(targetObject);
        }

        // ������� ����� ������
        targetObject = Instantiate(loadedModel, Vector3.zero, Quaternion.identity);
        targetObject.name = "LoadedModel";

        yield return null;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
