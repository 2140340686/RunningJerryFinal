using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

[ExecuteAlways]
public class MiniMapSceneView : MonoBehaviour
{
    public Camera miniMapCamera;
    public Canvas targetCanvas;
    public RawImage miniMapImage;
    public Vector2 panelSize = new Vector2(180f, 180f);
    public Vector2 anchoredPosition = new Vector2(-24f, 24f);
    public float orthographicSize = 18f;
    public int textureSize = 512;

    private RenderTexture renderTexture;
    private bool createdDisplayThisPass;

    private void OnEnable()
    {
        EnsureSetup();
    }

    private void OnValidate()
    {
        EnsureSetup();
    }

    private void Update()
    {
        EnsureSetup();
    }

    private void EnsureSetup()
    {
        if (orthographicSize <= 0f)
        {
            orthographicSize = 18f;
        }

        if (miniMapCamera == null)
        {
            miniMapCamera = FindNamedCamera("MiniMapCamera");
        }

        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>();
        }

        if (miniMapCamera == null || targetCanvas == null)
        {
            return;
        }

        createdDisplayThisPass = false;
        CleanupWorldSpaceDisplay();
        EnsureRawImage();
        EnsureRenderTexture();
        ConfigureMiniMapCamera();
    }

    private void CleanupWorldSpaceDisplay()
    {
        Camera displayCamera = FindDisplayCamera();
        if (displayCamera == null)
        {
            return;
        }

        Transform worldDisplay = displayCamera.transform.Find("MiniMapDisplay");
        if (worldDisplay == null)
        {
            return;
        }

#if UNITY_EDITOR
        Undo.DestroyObjectImmediate(worldDisplay.gameObject);
        EditorSceneManager.MarkSceneDirty(worldDisplay.gameObject.scene);
#else
        DestroyImmediate(worldDisplay.gameObject);
#endif
    }

    private void EnsureRawImage()
    {
        if (miniMapImage == null)
        {
            Transform existing = targetCanvas.transform.Find("MiniMapDisplay");
            if (existing != null)
            {
                miniMapImage = existing.GetComponent<RawImage>();
            }
        }

        if (miniMapImage == null)
        {
            GameObject display = new GameObject("MiniMapDisplay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            display.transform.SetParent(targetCanvas.transform, false);
            miniMapImage = display.GetComponent<RawImage>();
            miniMapImage.color = Color.white;
            createdDisplayThisPass = true;
            ApplyDefaultLayout();

#if UNITY_EDITOR
            Undo.RegisterCreatedObjectUndo(display, "Create MiniMapDisplay");
            EditorSceneManager.MarkSceneDirty(display.scene);
#endif
        }

        miniMapImage.raycastTarget = false;
    }

    private void EnsureRenderTexture()
    {
        if (renderTexture == null || renderTexture.width != textureSize || renderTexture.height != textureSize)
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
            }

            renderTexture = new RenderTexture(textureSize, textureSize, 16)
            {
                name = "MiniMapRT"
            };
        }

        if (miniMapCamera.targetTexture != renderTexture)
        {
            miniMapCamera.targetTexture = renderTexture;
        }

        if (miniMapImage.texture != renderTexture)
        {
            miniMapImage.texture = renderTexture;
        }
    }

    private void ConfigureMiniMapCamera()
    {
        miniMapCamera.rect = new Rect(0f, 0f, 1f, 1f);
        miniMapCamera.forceIntoRenderTexture = true;
        miniMapCamera.orthographic = true;
        miniMapCamera.orthographicSize = orthographicSize;
        miniMapCamera.depth = 1f;
    }

    private void ApplyDefaultLayout()
    {
        if (miniMapImage == null)
        {
            return;
        }

        RectTransform rect = miniMapImage.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = new Vector2(-Mathf.Abs(anchoredPosition.x), Mathf.Abs(anchoredPosition.y));
    }

    private Camera FindNamedCamera(string cameraName)
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera candidate in cameras)
        {
            if (candidate.name == cameraName)
            {
                return candidate;
            }
        }

        return null;
    }

    private Camera FindDisplayCamera()
    {
        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Camera candidate in cameras)
        {
            if (candidate != null && candidate != miniMapCamera && candidate.name == "Main Camera")
            {
                return candidate;
            }
        }

        return null;
    }
}
