using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

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


    private IEnumerator FadeSequence()
    {
        // 1) Fade to black (alpha = 1)
        fadeImage.DOFade(1.0f, fadeDuration);
        // 2) Move player
        player.transform.position = new Vector3(0.5f, 0f, 6f);

        // 3) Hold while black
        yield return new WaitForSeconds(holdDuration);

        // 4) Fade back to transparent (alpha = 0)
    }
}