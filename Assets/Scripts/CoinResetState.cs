using UnityEngine;

public class CoinResetState : MonoBehaviour
{
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Vector3 originalLocalScale;
    private bool initialized;

    public void CaptureIfNeeded()
    {
        if (initialized)
        {
            return;
        }

        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;
        originalLocalScale = transform.localScale;
        initialized = true;
    }

    public void ResetState()
    {
        CaptureIfNeeded();
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
        transform.localScale = originalLocalScale;
    }
}
