using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class mudarCena : MonoBehaviour
{
    public static bool endlessMode;
    public string nomeDaCena;

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != "Menu")
        {
            return;
        }

        BindMenuButton("start", LoadNormalMode);
        BindMenuButton("Endless", LoadEndlessMode);
        BindMenuButton("exit", SairDoJogo);
    }

    public void ChangeS()
    {
        if (!string.IsNullOrEmpty(nomeDaCena))
        {
            endlessMode = false;
            SceneManager.LoadScene(nomeDaCena);
        }
    }

    public void LoadNormalMode()
    {
        endlessMode = false;
        SceneManager.LoadScene(string.IsNullOrEmpty(nomeDaCena) ? "play" : nomeDaCena);
    }

    public void LoadEndlessMode()
    {
        endlessMode = true;
        SceneManager.LoadScene(string.IsNullOrEmpty(nomeDaCena) ? "play" : nomeDaCena);
    }

    public void SairDoJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BindMenuButton(string objectName, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        if (buttonObject == null)
        {
            return;
        }

        Button button = buttonObject.GetComponent<Button>();
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }
}
