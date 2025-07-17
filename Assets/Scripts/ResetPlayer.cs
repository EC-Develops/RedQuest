using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ResetPlayer : MonoBehaviour
{
    public GameObject player;

    // Fade sequence variables
    public Image fadeImage;           
    public float fadeDuration = 1f;   // Time to fade in/out
    public float holdDuration = 1f;   // Time to stay black

    // Allows the whole function to be called outside this script
    public void TryAgain()
    {
        StartCoroutine(FadeSequence());
        // move player to Start
        player.transform.position = new Vector3(0.5f, 0f, 6f);
    }


    public IEnumerator FadeSequence()
    {

        // Fade to Black
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));

        yield return new WaitForSeconds(holdDuration);

        // Fade back out
        yield return StartCoroutine(Fade(1f, 0f, fadeDuration));
    }

    IEnumerator Fade(float startAlpha, float endAlpha, float duration)
    {
        float elapsed = 0f;
        Color color = fadeImage.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(startAlpha, endAlpha, elapsed / duration);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }

        // Ensure final alpha
        color.a = endAlpha;
        fadeImage.color = color;
    }
}