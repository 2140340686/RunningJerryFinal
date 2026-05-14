using UnityEngine;

public class PowerUpPickup : MonoBehaviour
{
    public enum PowerUpType
    {
        Magnet,
        SpeedBoost
    }

    public PowerUpType type;
    public float duration = 5f;
    public float rotateSpeed = 120f;
    public AudioClip collectSound;
    public Object collectEffect;

    private bool collected;

    private void OnEnable()
    {
        collected = false;
    }

    private void Update()
    {
        transform.Rotate(Vector3.up * rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected || !other.CompareTag("Player"))
        {
            return;
        }

        PlayerScript player = other.GetComponent<PlayerScript>();
        if (player == null)
        {
            player = other.GetComponentInParent<PlayerScript>();
        }

        if (player == null)
        {
            return;
        }

        collected = true;

        if (collectSound != null)
        {
            AudioSource.PlayClipAtPoint(collectSound, transform.position);
        }

        if (type == PowerUpType.Magnet)
        {
            player.ActivateMagnet(duration);
        }
        else
        {
            player.ActivateSpeedBoost(duration);
        }

        gameObject.SetActive(false);
        SpawnCollectEffect();
    }

    private void SpawnCollectEffect()
    {
        if (collectEffect == null)
        {
            return;
        }

        try
        {
            Object.Instantiate(collectEffect, transform.position, Quaternion.identity);
        }
        catch
        {
            if (collectEffect is Component effectComponent)
            {
                Object.Instantiate(effectComponent.gameObject, transform.position, Quaternion.identity);
            }
        }
    }
}
