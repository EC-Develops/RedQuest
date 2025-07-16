using UnityEngine;

public class TileBehavior : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // From the GridManager class in 
            FindFirstObjectByType<GridManager>().OnTileStepped(this.gameObject);
            Debug.Log("Player has stepped on tile: " + this.name);
        }
    }
}
