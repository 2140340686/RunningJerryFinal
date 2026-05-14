using System.Collections.Generic;
using UnityEngine;

public class SpawnPlatform : MonoBehaviour
{
    public List<GameObject> platforms = new List<GameObject>();
    public List<Transform> currentPlatforms = new List<Transform>();
    public int offset;
    public float recycleDistance = 45f;

    private Transform player;
    private Transform currentPlatformPoint;
    private int platformIndex;
    private GameController gc;

    private void Start()
    {
        player = GameObject.FindGameObjectWithTag("Player").transform;
        gc = FindFirstObjectByType<GameController>();

        for (int i = 0; i < platforms.Count; i++)
        {
            Transform platformTransform = Instantiate(platforms[i], new Vector3(0, 0, i * 86), transform.rotation).transform;
            currentPlatforms.Add(platformTransform);
            offset += 86;
            gc?.ConfigurePowerUpsOn(platformTransform);
            ResetPlatformChildren(platformTransform);
        }

        currentPlatformPoint = currentPlatforms[platformIndex].GetComponent<Platform>().point;
    }

    private void Update()
    {
        float distance = player.position.z - currentPlatformPoint.position.z;

        if (distance >= recycleDistance)
        {
            Recycle(currentPlatforms[platformIndex].gameObject);
            platformIndex++;

            if (platformIndex > currentPlatforms.Count - 1)
            {
                platformIndex = 0;
            }

            currentPlatformPoint = currentPlatforms[platformIndex].GetComponent<Platform>().point;
        }
    }

    public void Recycle(GameObject platform)
    {
        platform.transform.position = new Vector3(0, 0, offset);
        offset += 86;
        gc?.ConfigurePowerUpsOn(platform.transform);
        ResetPlatformChildren(platform.transform);
    }

    private void ResetPlatformChildren(Transform root)
    {
        bool hasActiveSpeedBoost = false;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.CompareTag("Coin"))
            {
                CoinResetState resetState = child.GetComponent<CoinResetState>();
                if (resetState == null)
                {
                    resetState = child.gameObject.AddComponent<CoinResetState>();
                }

                resetState.CaptureIfNeeded();
                resetState.ResetState();
            }

            PowerUpPickup pickup = child.GetComponent<PowerUpPickup>();

            if (child.CompareTag("Coin") || pickup != null)
            {
                child.gameObject.SetActive(true);
            }

            if (pickup != null && pickup.type == PowerUpPickup.PowerUpType.SpeedBoost)
            {
                hasActiveSpeedBoost = true;
            }

            SimpleCollectibleScript collectible = child.GetComponent<SimpleCollectibleScript>();
            if (collectible != null)
            {
                collectible.enabled = false;
            }
        }

        if (!hasActiveSpeedBoost)
        {
            gc?.ConfigurePowerUpsOn(root);

            foreach (PowerUpPickup pickup in root.GetComponentsInChildren<PowerUpPickup>(true))
            {
                if (pickup.type == PowerUpPickup.PowerUpType.SpeedBoost)
                {
                    pickup.gameObject.SetActive(true);
                }
            }
        }
    }
}
