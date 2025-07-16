using System;
using UnityEngine;

public class GameStart : MonoBehaviour
{
    public Material customColor;

    private void OnTriggerEnter(Collider other)
    {
        // Change the triggerTile to correctColor when stepped on to initiate game
        {
            GetComponent<Renderer>().material = customColor;
        }
    }
}
