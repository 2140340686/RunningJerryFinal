using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameController : MonoBehaviour
{
    public enum GameState
    {
        WaitingToStart,
        Playing,
        Paused,
        GameOver
    }

    [Header("HUD")]
    public float score;
    public int scoreCoins;
    public Text scoreCoinText;
    public Text scoreText;
    public GameObject gameOver;
    public GameObject winPanel;

    [Header("Enemy Setup")]
    public RuntimeAnimatorController enemyController;
    public Avatar enemyAvatar;
    public AnimationClip enemyIdleClip;
    public AnimationClip enemyRunClip;
    public AnimationClip enemyAttackClip;
    public string enemyObjectName = "Monster01_01";
    public Texture2D enemySkinTexture;
    public float milestoneSpeedBoost = 1.2f;
    public float enemyAttackImpactDelay = 0.8f;
    public AudioClip coinCollectSound;
    public AudioClip loseSound;
    public AudioClip winSound;
    public AudioClip powerUpSound;
    public GameObject powerUpCollectEffect;
    public AudioClip backgroundMusic;

    [Header("Mini Map")]
    public Vector2 miniMapPanelSize = new Vector2(220f, 220f);
    public Vector2 miniMapPanelOffset = new Vector2(-24f, 24f);
    public Vector3 miniMapCameraLocalPosition = new Vector3(0f, 28f, 4f);
    public Vector3 miniMapCameraLocalEuler = new Vector3(90f, 0f, 0f);
    public float miniMapOrthoSize = 18f;
    public float gameOverDelay = 3f;

    [Header("Pause")]
    public float resumeCountdownSeconds = 3f;

    private static readonly Vector3 ReusedHealthPackLocalPosition = new Vector3(0.13f, 1.98f, 32.21f);
    private static readonly Quaternion ReusedHealthPackLocalRotation = new Quaternion(-0.014926077f, 0.24905281f, 0.09738967f, 0.9634652f);
    private static readonly Vector3 ReusedHealthPackLocalScale = new Vector3(2f, 2f, 2f);

    private GameState currentState = GameState.WaitingToStart;
    private PlayerScript player;
    private EnemyChaseAI enemy;
    private Coroutine gameOverRoutine;
    private Coroutine resumeRoutine;

    private Font uiFont;
    private Text resultText;
    private Text winText;
    private GameObject continuePanel;
    private Button continueButton;
    private Button pauseButton;
    private Button gameOverMenuButton;
    private Button winMenuButton;
    private Text continueLabelText;
    private TMP_Text continueLabelTmp;
    private string continueLabelDefault = "Continue";
    private Text countdownOverlayText;
    private int nextSpeedMilestone = 50;
    private AudioSource musicSource;
    [SerializeField] private GameObject healthPackTemplate;
    private bool endlessMode;

    public bool IsPlaying => currentState == GameState.Playing;
    public bool IsPaused => currentState == GameState.Paused;
    public bool IsGameOver => currentState == GameState.GameOver;

    private void Start()
    {
        Time.timeScale = 1f;
        player = FindFirstObjectByType<PlayerScript>();
        endlessMode = mudarCena.endlessMode;
        AssignAudioAssets();
        SetupBackgroundMusic();

        if (gameOver == null)
        {
            GameObject gameOverObject = FindSceneObject("Game Over");
            if (gameOverObject != null)
            {
                gameOver = gameOverObject;
            }
        }

        if (winPanel == null)
        {
            GameObject winObject = FindSceneObject("Win");
            if (winObject != null)
            {
                winPanel = winObject;
            }
        }

        if (gameOver != null)
        {
            gameOver.SetActive(false);
        }

        if (winPanel != null)
        {
            winPanel.SetActive(false);
        }

        if (scoreCoinText != null)
        {
            uiFont = scoreCoinText.font;
        }

        if (uiFont == null)
        {
            uiFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        PreparePowerUps();
        try
        {
            PrepareExistingUi();
            PreparePauseUi();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex, this);
        }

        try
        {
            SetupEnemy();
        }
        catch (System.Exception ex)
        {
            Debug.LogException(ex, this);
        }

        RefreshHud();
        StartRun();
    }

    private void Update()
    {
        if (currentState != GameState.Playing || player == null)
        {
            return;
        }

        score = Mathf.Max(0f, player.DistanceTravelled);
        RefreshHud();
    }

    public void StartRun()
    {
        if (currentState != GameState.WaitingToStart)
        {
            return;
        }

        currentState = GameState.Playing;
        Time.timeScale = 1f;
        SetPauseInteractable(true);

        player?.BeginRun();
        enemy?.BeginChase();
        RefreshHud();
    }

    public void PauseGame()
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

        currentState = GameState.Paused;
        Time.timeScale = 0f;
        ShowContinuePanel(false);
        SetPauseInteractable(false);
    }

    public void ContinueGame()
    {
        if (currentState != GameState.Paused)
        {
            return;
        }

        if (resumeRoutine != null)
        {
            StopCoroutine(resumeRoutine);
        }

        resumeRoutine = StartCoroutine(ResumeCountdownRoutine());
    }

    public void LoadMenuScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("Menu");
    }

    public void ShowGameOver(string message = "GAME OVER")
    {
        if (currentState == GameState.GameOver)
        {
            return;
        }

        currentState = GameState.GameOver;
        Time.timeScale = 1f;
        HideContinuePanel();
        SetPauseInteractable(false);

        player?.FreezeForEnemyAttack();
        enemy?.CatchPlayer();
        SetResultText(endlessMode ? BuildEndlessRunEndedMessage() : message);

        if (gameOverRoutine != null)
        {
            StopCoroutine(gameOverRoutine);
        }

        gameOverRoutine = StartCoroutine(ShowGameOverDelayed(message));
    }

    public void AddCoin()
    {
        scoreCoins++;
        PlayClip(coinCollectSound);
        RefreshHud();

        if (!endlessMode && scoreCoins >= 200)
        {
            ShowWin();
            return;
        }

        if (scoreCoins >= nextSpeedMilestone && nextSpeedMilestone <= 150)
        {
            player?.IncreaseBaseSpeed(milestoneSpeedBoost);
            enemy?.IncreaseBaseChaseSpeed(milestoneSpeedBoost);
            nextSpeedMilestone += 50;
        }
    }

    public void OnPlayerHitObstacle()
    {
        if (currentState != GameState.Playing)
        {
            return;
        }

        enemy?.TriggerPressureBoost();
        RefreshHud();
    }

    private void SetupEnemy()
    {
        GameObject enemyObject = GameObject.Find(enemyObjectName);
        if (enemyObject == null || player == null)
        {
            return;
        }

        GameObject animationTarget = FindEnemyAnimationTarget(enemyObject);
        Animator rootAnimator = enemyObject.GetComponent<Animator>();
        Animator enemyAnimator = animationTarget.GetComponent<Animator>();
        if (enemyAnimator == null)
        {
            enemyAnimator = animationTarget.AddComponent<Animator>();
        }

        AssignEnemyAnimationAssets(enemyObject);

        if (enemyAvatar != null)
        {
            enemyAnimator.avatar = enemyAvatar;
        }

        if (enemyController != null)
        {
            enemyAnimator.runtimeAnimatorController = enemyController;
        }

        if (rootAnimator != null && rootAnimator != enemyAnimator)
        {
            rootAnimator.enabled = false;
        }

        enemyAnimator.enabled = true;
        enemyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        enemyAnimator.Rebind();
        enemyAnimator.Update(0f);

        ApplyEnemySkin(enemyObject);

        enemy = enemyObject.GetComponent<EnemyChaseAI>();
        if (enemy == null)
        {
            enemy = enemyObject.AddComponent<EnemyChaseAI>();
        }

        enemy.Initialize(player.transform, this, enemyAnimator, enemyIdleClip, enemyRunClip, enemyAttackClip);
        enemy.SyncBaseSpeed(player.BaseRunSpeed);
        enemy.ResetToStartPosition();

        if (currentState == GameState.Playing)
        {
            enemy.BeginChase();
        }
    }

    private GameObject FindEnemyAnimationTarget(GameObject enemyObject)
    {
        Transform explicitRoot = enemyObject.transform.Find("Monster01_AllAnim");
        if (explicitRoot != null)
        {
            return explicitRoot.gameObject;
        }

        foreach (SkinnedMeshRenderer renderer in enemyObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer != null)
            {
                return renderer.transform.root == enemyObject.transform
                    ? renderer.gameObject
                    : renderer.transform.root.gameObject;
            }
        }

        foreach (MeshRenderer renderer in enemyObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (renderer != null)
            {
                return renderer.transform.root == enemyObject.transform
                    ? renderer.gameObject
                    : renderer.transform.root.gameObject;
            }
        }

        return enemyObject;
    }

    private void AssignEnemyAnimationAssets(GameObject enemyObject)
    {
#if UNITY_EDITOR
        RuntimeAnimatorController loadedController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Stylized3DMonster/Monster01/Anim/InPlace_Anim/Monster01_AC_InPlace.controller"
        );

        if (loadedController != null)
        {
            enemyController = loadedController;
        }

        enemyIdleClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Stylized3DMonster/Monster01/Anim/InPlace_Anim/Monster01_Idle.anim"
        );
        enemyRunClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Stylized3DMonster/Monster01/Anim/InPlace_Anim/Monster01_Run_InPlace.anim"
        );
        enemyAttackClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/Stylized3DMonster/Monster01/Anim/InPlace_Anim/Monster01_Attack02_InPlace.anim"
        );

        Avatar loadedAvatar = null;
        Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
            "Assets/Stylized3DMonster/Monster01/Monster01_AllAnim.fbx"
        );

        foreach (Object asset in assets)
        {
            if (asset is Avatar avatar)
            {
                loadedAvatar = avatar;
                break;
            }
        }

        if (loadedAvatar == null)
        {
            Animator[] animators = enemyObject.GetComponentsInChildren<Animator>(true);
            foreach (Animator candidate in animators)
            {
                if (candidate != null && candidate.avatar != null)
                {
                    loadedAvatar = candidate.avatar;
                    break;
                }
            }
        }

        enemyAvatar = loadedAvatar;
#endif
    }

    private void ApplyEnemySkin(GameObject enemyObject)
    {
        if (enemySkinTexture == null)
        {
            return;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null)
        {
            return;
        }

        Material runtimeSkin = new Material(shader);
        runtimeSkin.mainTexture = enemySkinTexture;

        foreach (Renderer renderer in enemyObject.GetComponentsInChildren<Renderer>(true))
        {
            renderer.material = runtimeSkin;
        }
    }

    private void PrepareExistingUi()
    {
        if (scoreCoinText == null)
        {
            GameObject coinObject = FindSceneObject("Coins");
            if (coinObject != null)
            {
                scoreCoinText = coinObject.GetComponent<Text>();
            }
        }

        if (scoreText == null)
        {
            GameObject distanceObject = FindSceneObject("Distance");
            if (distanceObject != null)
            {
                scoreText = distanceObject.GetComponent<Text>();
            }
        }

        if (gameOver != null)
        {
            Transform textTransform = gameOver.transform.Find("Text");
            if (textTransform != null)
            {
                resultText = textTransform.GetComponent<Text>();
                ApplyResultTextStyle(false);
            }
        }

        if (winPanel != null)
        {
            Transform textTransform = winPanel.transform.Find("Text");
            if (textTransform != null)
            {
                winText = textTransform.GetComponent<Text>();
                ApplyWinTextStyle();
            }
        }
    }

    private void PreparePauseUi()
    {
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            return;
        }

        continuePanel = FindContinuePanel();
        if (continuePanel != null)
        {
            continuePanel.SetActive(false);
            continueButton = continuePanel.GetComponentInChildren<Button>(true);
            if (continueButton != null)
            {
                continueButton.onClick.RemoveAllListeners();
                continueButton.onClick.AddListener(ContinueGame);
                continueLabelText = continueButton.GetComponentInChildren<Text>(true);
                continueLabelTmp = continueButton.GetComponentInChildren<TMP_Text>(true);

                if (continueLabelTmp != null && !string.IsNullOrEmpty(continueLabelTmp.text))
                {
                    continueLabelDefault = continueLabelTmp.text;
                }
                else if (continueLabelText != null && !string.IsNullOrEmpty(continueLabelText.text))
                {
                    continueLabelDefault = continueLabelText.text;
                }
            }
        }

        GameObject pauseObject = FindSceneObject("Pause");
        if (pauseObject != null)
        {
            pauseButton = pauseObject.GetComponent<Button>();
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveAllListeners();
                pauseButton.onClick.AddListener(PauseGame);
            }
        }

        if (gameOver != null)
        {
            gameOverMenuButton = gameOver.GetComponentInChildren<Button>(true);
            if (gameOverMenuButton != null)
            {
                gameOverMenuButton.onClick.RemoveAllListeners();
                gameOverMenuButton.onClick.AddListener(LoadMenuScene);
            }
        }

        if (winPanel != null)
        {
            winMenuButton = winPanel.GetComponentInChildren<Button>(true);
            if (winMenuButton != null)
            {
                winMenuButton.onClick.RemoveAllListeners();
                winMenuButton.onClick.AddListener(LoadMenuScene);
            }
        }

        GameObject overlayObject = new GameObject("CountdownOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        overlayObject.transform.SetParent(canvas.transform, false);

        RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.sizeDelta = new Vector2(220f, 120f);
        overlayRect.anchoredPosition = Vector2.zero;

        countdownOverlayText = overlayObject.GetComponent<Text>();
        countdownOverlayText.font = uiFont;
        countdownOverlayText.fontSize = 72;
        countdownOverlayText.fontStyle = FontStyle.Bold;
        countdownOverlayText.alignment = TextAnchor.MiddleCenter;
        countdownOverlayText.color = Color.white;
        countdownOverlayText.raycastTarget = false;
        countdownOverlayText.text = string.Empty;
        countdownOverlayText.gameObject.SetActive(false);
    }

    private GameObject FindContinuePanel()
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            GameObject result = FindContinuePanelRecursive(root.transform);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private GameObject FindContinuePanelRecursive(Transform parent)
    {
        if (parent.name == "Continue" &&
            parent.GetComponent<Button>() == null &&
            parent.GetComponentInChildren<Button>(true) != null)
        {
            return parent.gameObject;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            GameObject result = FindContinuePanelRecursive(parent.GetChild(i));
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private GameObject FindSceneObject(string objectName)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            Transform match = FindChildRecursive(root.transform, objectName);
            if (match != null)
            {
                return match.gameObject;
            }
        }

        return null;
    }

    private Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent.name == objectName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindChildRecursive(parent.GetChild(i), objectName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void RefreshHud()
    {
        if (scoreCoinText != null)
        {
            scoreCoinText.text = "Coins: " + scoreCoins;
        }

        if (scoreText != null)
        {
            scoreText.text = "Distance: " + Mathf.RoundToInt(score);
        }
    }

    private void ShowWin()
    {
        if (currentState == GameState.GameOver)
        {
            return;
        }

        currentState = GameState.GameOver;
        Time.timeScale = 1f;
        HideContinuePanel();
        SetPauseInteractable(false);
        PlayClip(winSound);

        if (gameOverRoutine != null)
        {
            StopCoroutine(gameOverRoutine);
            gameOverRoutine = null;
        }

        player?.FreezeRun();
        enemy?.CelebrateWinStop();

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }

        if (winText != null)
        {
            winText.gameObject.SetActive(true);
            ApplyWinTextStyle();
        }

        RefreshHud();
    }

    private void SetResultText(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
            ApplyResultTextStyle(endlessMode);
        }
    }

    private IEnumerator ShowGameOverDelayed(string message)
    {
        float delay = gameOverDelay > 0f ? gameOverDelay : 3f;
        float attackDelay = enemyAttackImpactDelay > 0f ? enemyAttackImpactDelay : 0.8f;

        if (gameOver != null)
        {
            gameOver.SetActive(false);
        }

        yield return new WaitForSeconds(attackDelay);

        player?.TriggerDeath();

        yield return new WaitForSeconds(delay);

        if (gameOver != null)
        {
            gameOver.SetActive(true);
        }

        PlayClip(loseSound);
        SetResultText(endlessMode ? BuildEndlessRunEndedMessage() : message);
        gameOverRoutine = null;
    }

    private string BuildEndlessRunEndedMessage()
    {
        return "RUN ENDED\nDistance: " + Mathf.RoundToInt(score) + "m\nCoins Collected: " + scoreCoins;
    }

    private void ApplyResultTextStyle(bool endless)
    {
        if (resultText == null)
        {
            return;
        }

        RectTransform rect = resultText.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = endless ? new Vector2(0f, 30f) : new Vector2(0f, -10f);
            rect.sizeDelta = endless ? new Vector2(760f, 240f) : new Vector2(760f, 150f);
            rect.localScale = Vector3.one;
        }

        resultText.alignment = TextAnchor.MiddleCenter;
        resultText.fontStyle = endless ? FontStyle.Bold : FontStyle.Normal;
        resultText.fontSize = endless ? 34 : 56;
        resultText.horizontalOverflow = HorizontalWrapMode.Wrap;
        resultText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void ApplyWinTextStyle()
    {
        if (winText == null)
        {
            return;
        }

        RectTransform rect = winText.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 10f);
            rect.sizeDelta = new Vector2(760f, 180f);
            rect.localScale = Vector3.one;
        }

        winText.alignment = TextAnchor.MiddleCenter;
        winText.fontSize = 52;
        winText.fontStyle = FontStyle.Bold;
        winText.horizontalOverflow = HorizontalWrapMode.Wrap;
        winText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private IEnumerator ResumeCountdownRoutine()
    {
        ShowContinuePanel(true);

        int countdown = Mathf.Max(1, Mathf.RoundToInt(resumeCountdownSeconds));
        for (int i = countdown; i >= 1; i--)
        {
            SetCountdownOverlay(i.ToString());

            yield return new WaitForSecondsRealtime(1f);
        }

        SetCountdownOverlay(string.Empty);
        HideContinuePanel();
        Time.timeScale = 1f;
        currentState = GameState.Playing;
        SetPauseInteractable(true);
        resumeRoutine = null;
    }

    private void ShowContinuePanel(bool showCountdown)
    {
        if (continuePanel != null)
        {
            continuePanel.SetActive(!showCountdown);
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(!showCountdown);
            continueButton.interactable = !showCountdown;
        }

        if (showCountdown)
        {
            SetCountdownOverlay(string.Empty);
        }
        else
        {
            SetContinueLabel(continueLabelDefault);
        }
    }

    private void HideContinuePanel()
    {
        if (continuePanel != null)
        {
            continuePanel.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            continueButton.interactable = true;
        }

        SetContinueLabel(continueLabelDefault);
        SetCountdownOverlay(string.Empty);
    }

    private void SetPauseInteractable(bool interactable)
    {
        if (pauseButton != null)
        {
            pauseButton.interactable = interactable;
        }
    }

    private void SetContinueLabel(string value)
    {
        if (continueLabelTmp != null)
        {
            continueLabelTmp.text = value;
        }

        if (continueLabelText != null)
        {
            continueLabelText.text = value;
        }
    }

    private void SetCountdownOverlay(string value)
    {
        if (countdownOverlayText == null)
        {
            return;
        }

        bool visible = !string.IsNullOrEmpty(value);
        countdownOverlayText.gameObject.SetActive(visible);
        countdownOverlayText.text = value;
    }

    private void PreparePowerUps()
    {
        foreach (Transform root in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            ConfigurePowerUpsOn(root);
        }
    }

    public void ConfigurePowerUpsOn(Transform root)
    {
        if (root == null)
        {
            return;
        }

        bool foundHealthPack = false;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            string objectName = child.name.ToLowerInvariant();
            if (objectName.Contains("magnet"))
            {
                SetupPowerUp(child.gameObject, PowerUpPickup.PowerUpType.Magnet, true);
            }
            else if (objectName.Contains("healthpackgreen"))
            {
                foundHealthPack = true;
                if (healthPackTemplate == null)
                {
                    healthPackTemplate = child.gameObject;
                }
                SetupPowerUp(child.gameObject, PowerUpPickup.PowerUpType.SpeedBoost, false);
            }
        }

        if (!foundHealthPack && healthPackTemplate != null && root.GetComponent<Platform>() != null)
        {
            GameObject clone = Instantiate(healthPackTemplate, root);
            clone.name = "HealthPackGreen";
            clone.transform.localPosition = ReusedHealthPackLocalPosition;
            clone.transform.localRotation = ReusedHealthPackLocalRotation;
            clone.transform.localScale = ReusedHealthPackLocalScale;
            clone.SetActive(true);
            SetupPowerUp(clone, PowerUpPickup.PowerUpType.SpeedBoost, false);
        }
    }

    private void SetupPowerUp(GameObject target, PowerUpPickup.PowerUpType type, bool alwaysRotate)
    {
        PowerUpPickup pickup = target.GetComponent<PowerUpPickup>();
        if (pickup == null)
        {
            pickup = target.AddComponent<PowerUpPickup>();
        }

        pickup.type = type;
        pickup.duration = 5f;
        pickup.rotateSpeed = alwaysRotate ? 160f : 80f;
        pickup.collectSound = powerUpSound;
        pickup.collectEffect = powerUpCollectEffect;

        Collider collider = target.GetComponent<Collider>();
        if (collider == null)
        {
            SphereCollider sphere = target.AddComponent<SphereCollider>();
            sphere.radius = type == PowerUpPickup.PowerUpType.Magnet ? 0.18f : 0.35f;
            sphere.isTrigger = true;
        }
        else
        {
            collider.isTrigger = true;
            if (collider is SphereCollider sphere)
            {
                sphere.radius = type == PowerUpPickup.PowerUpType.Magnet ? 0.18f : Mathf.Max(0.25f, sphere.radius);
            }
        }

        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SimpleCollectibleScript collectible = target.GetComponent<SimpleCollectibleScript>();
        if (collectible != null)
        {
            collectible.enabled = false;
        }
    }

    private void AssignAudioAssets()
    {
#if UNITY_EDITOR
        if (coinCollectSound == null)
        {
            coinCollectSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-32.wav"
            );
        }

        if (loseSound == null)
        {
            loseSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-12.wav"
            );
        }

        if (winSound == null)
        {
            winSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-18.wav"
            );
        }

        if (powerUpSound == null)
        {
            powerUpSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/Casual Game Sounds U6/CasualGameSounds/DM-CGS-21.wav"
            );
        }

        if (powerUpCollectEffect == null)
        {
            powerUpCollectEffect = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SimpleCollectiblesPack/Prefabs/CollectEffectSample.prefab"
            );
        }

        if (healthPackTemplate == null)
        {
            healthPackTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SimpleCollectiblesPack/Prefabs/HealthPackGreen.prefab"
            );
        }

        if (backgroundMusic == null)
        {
            backgroundMusic = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/FREE Gaming Music/Chiptune Retro Old School Video Game Music.wav"
            );
        }
#endif
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null)
        {
            Vector3 origin = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            AudioSource.PlayClipAtPoint(clip, origin);
        }
    }

    private void SetupBackgroundMusic()
    {
        if (backgroundMusic == null)
        {
            return;
        }

        musicSource = GetComponent<AudioSource>();
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        musicSource.clip = backgroundMusic;
        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.volume = 0.55f;

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }
}
