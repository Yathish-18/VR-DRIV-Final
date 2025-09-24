using UnityEngine;
using System.Collections;

public class LightningEffect : MonoBehaviour
{
    public Light lightningLight;
    public float minDelay = 5f;
    public float maxDelay = 15f;
    public float minFlashDuration = 0.05f;
    public float maxFlashDuration = 0.15f;
    public float minIntensity = 2f;
    public float maxIntensity = 5f;
    public AudioSource lightningSound;

    private void Start()
    {
        lightningLight.intensity = 0f;
        StartCoroutine(LightningRoutine());
    }

    IEnumerator LightningRoutine()
    {
        while (true)
        {
            // Wait before the next lightning strike
            float delay = Random.Range(minDelay, maxDelay);
            yield return new WaitForSeconds(delay);

            // Randomize number of flashes (1 to 3)
            int flashCount = Random.Range(1, 4);

            // Optional: Play thunder once per strike
            if (lightningSound) lightningSound.Play();

            for (int i = 0; i < flashCount; i++)
            {
                float intensity = Random.Range(minIntensity, maxIntensity);
                float duration = Random.Range(minFlashDuration, maxFlashDuration);

                lightningLight.intensity = intensity;
                yield return new WaitForSeconds(duration);
                lightningLight.intensity = 0f;

                // Small pause between blinks
                yield return new WaitForSeconds(Random.Range(0.05f, 0.2f));
            }
        }
    }
}
