using UnityEngine;
using DG.Tweening;

public class CoinBehavior : MonoBehaviour
{
    public GameObject restartMenu;

    [SerializeField]
    private Vector3 rotationVector = new Vector3(0f, 360f, 0f);
    [SerializeField]
    private float rotationSpeed = 1f;

    private Vector3 endSpot = new Vector3(0.45f, 0.2f, -4.69f);
    private float bounceHeight = 1f;
    private float bounceDuration = 1f;

    void Start()
    {
        transform.DORotate(rotationVector, rotationSpeed, RotateMode.WorldAxisAdd).SetLoops(-1).SetEase(Ease.Linear);
        transform.DOJump(endSpot, bounceHeight, 1, bounceDuration, false).SetLoops(-1).SetEase(Ease.Linear);
    }

    // If the coin collides with the player
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("The game was won! Congrats");
            // Disable the coin (this object)
            gameObject.SetActive(false);
            // Show the UI menu
            if (restartMenu != null)
            {
                restartMenu.SetActive(true);
            }
        }
    }
}
