using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ResetPlayer : MonoBehaviour
{
    public GameObject player;

    // Fade sequence variables
    public Image fadeImage;           
    public float fadeDuration = 1f;   // Time to fade in/out
    public float holdDuration = 0.5f;   // Time to stay black

    // Allows the whole function to be called outside this script
    public void TryAgain()
    {
        Debug.Log("Fade sequence commencing");
        StartCoroutine(FadeSequence());
    }


    public IEnumerator FadeSequence()
    {

        // 1) Fade to Black
        yield return StartCoroutine(Fade(0f, 1f, fadeDuration));
        // 2) Move player to start while screen is black
        player.transform.position = new Vector3(0.5f, 0f, 6f);
        // 3) Wait while screen is black
        yield return new WaitForSeconds(holdDuration);
        // 4) Fade back in
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