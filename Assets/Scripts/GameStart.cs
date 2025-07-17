using System;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    public Material defaultColor;
    public Material customColor;
    
    public GridManager gridManager;
    private void Start()
    {
    }
    private void OnTriggerEnter(Collider other)
    {
        {
            gridManager.ResetGrid(); // Important to reset everything when triggerTile is stepped on during the game
            
            // Change the triggerTile to correctColor when stepped on to initiate game
            GetComponent<Renderer>().material = customColor;
        }
    }
}

