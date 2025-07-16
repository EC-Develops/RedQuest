using System;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    public Material defaultColor;
    public Material customColor;
    
    public GridManager gridManager;
    private GameObject[] gridTiles;
    private void Start()
    {
        gridTiles = GameObject.FindGameObjectsWithTag("Tile");

    }
    private void OnTriggerEnter(Collider other)
    {

        {
            resetGridTiles(); // Important for when triggerTile is stepped on during the game
            
            // Change the triggerTile to correctColor when stepped on to initiate game
            GetComponent<Renderer>().material = customColor;
        }
    }
    
    private void resetGridTiles()
    {
                    // Reset grid tiles color
                    foreach (GameObject tile in gridTiles)
                    {
                        tile.GetComponent<Renderer>().material = defaultColor;
                    }
                    
                    // Reset step count
                    gridManager.currentStepIndex = 0;

    }
    
}

