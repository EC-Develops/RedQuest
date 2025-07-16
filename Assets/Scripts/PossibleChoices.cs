using System.Collections.Generic;
using UnityEngine;

public class PossibleChoices : MonoBehaviour
{
    public List<GameObject> threeOptions = new List<GameObject>();

    /// <summary>
    /// Randomly selects one of the three options.
    /// </summary>
    /// <returns>A randomly selected GameObject from threeOptions, or null if none available.</returns>
    public GameObject RandomSelection()
    {
        if (threeOptions == null || threeOptions.Count == 0)
        {
            Debug.LogWarning($"{gameObject.name} has no possible next tiles assigned.");
            return null;
        }

        int randomIndex = Random.Range(0, threeOptions.Count);
        GameObject selectedTile = threeOptions[randomIndex];

        Debug.Log($"{gameObject.name} selected next tile: {selectedTile.name}");
        return selectedTile;
    }
}
