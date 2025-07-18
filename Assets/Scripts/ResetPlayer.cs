using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ResetPlayer : MonoBehaviour
{
    public GameObject player;

    // Fade sequence variables
    public Image fadeImage;
    public float fadeDuration = 1.0f; // Time to fade in/out

    public IEnumerator TryAgain()
    {
        Debug.Log("Fade sequence commencing");
        // 1) Fade to black (alpha = 1)
        yield return fadeImage.DOFade(1.0f, fadeDuration).WaitForCompletion();
        // 2) Move player
        player.transform.position = new Vector3(0.5f, 0f, 6f);

        // 3) Hold while black
        yield return new WaitForSeconds(1.0f);

        // 4) Fade back to transparent (alpha = 0)
        yield return fadeImage.DOFade(0f, fadeDuration);
    }
}