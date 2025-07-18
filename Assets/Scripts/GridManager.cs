using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

using UnityEngine.XR.Interaction.Toolkit.Inputs;

public class GridManager : MonoBehaviour
{
    // Calling other scripts
    private ResetPlayer resetPlayer;

    public GameObject startupTile;
    private bool hasStarted = false;

    // All tiles added to a master list
    public List<GameObject> allTiles = new List<GameObject>();
    // Path of tiles added in a list within Unity inspector
    public List<GameObject> pathTiles = new List<GameObject>();

    public GameObject firstRow;

    // Colors
    public Material correctColor;
    public Material wrongColor;
    public Material defaultColor;

    // Player step tracking
    public int currentStepIndex = 0;

    // While pathVisible = true, the player cannot step on any grid tiles
    private bool pathVisible = true;

    private Coroutine lightUpCoroutine;

    // Player & components
    public GameObject player;
    public Rigidbody rb;
    public CharacterController cC;
    public InputActionManager inputActionManager;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        resetPlayer = GetComponent<ResetPlayer>();
        rb = player.GetComponent<Rigidbody>();
        cC = player.GetComponent<CharacterController>();
        inputActionManager = player.GetComponent<InputActionManager>();

        // Populate the master list by the "Tile" tag
        allTiles.AddRange(GameObject.FindGameObjectsWithTag("Tile"));
        Debug.Log("All tiles loaded: " + allTiles.Count);

        GeneratePath(5);
    }

    // Update is called once per frame
    void Update()
    {
        if (!hasStarted && startupTile.GetComponent<Renderer>().material.color == Color.green)
        {
            hasStarted = true;
            lightUpCoroutine = StartCoroutine(LightUpSequence());
        }
    }

    // Generates the path randomly while ensuring that
    // (1) The first tile is from the first row
    // (2) Following tiles are selected randomly from the list of possible choices
    void GeneratePath(int pathLength)
    {
        pathTiles.Clear();

        // Pick the 1st tile from the first row
        int randomIndex = Random.Range(0, firstRow.transform.childCount);
        Transform currentTileTransform = firstRow.transform.GetChild(randomIndex);
        GameObject currentTile = currentTileTransform.gameObject;
        pathTiles.Add(currentTile);
        Debug.Log("First tile added: " + currentTile.name);

        // Get the next tiles from possibleChoices
        for (int i = 1; i < pathLength; i++)
        {
            PossibleChoices possibleChoices = currentTile.GetComponent<PossibleChoices>();

            GameObject nextTile = possibleChoices.RandomSelection();

            pathTiles.Add(nextTile);
            Debug.Log("Next Tile : " + nextTile.name);

            currentTile = nextTile;
        }
    }

    public IEnumerator LightUpSequence()
    {
        yield return new WaitForSeconds(1.0f); // short delay before lighting up the first tile

        pathVisible = true;

        // Light up each tile desired in order w/ delay
        foreach (GameObject tile in pathTiles)
        {
            tile.GetComponent<Renderer>().material = correctColor;
            yield return new WaitForSeconds(1.0f); // short delay for memory effect
        }

        yield return new WaitForSeconds(1.0f); // pause before resetting

        // Return all tiles back to grey
        foreach (GameObject tile in pathTiles)
        {
            tile.GetComponent<Renderer>().material = defaultColor;
        }

        // Turn off the TriggerTile
        startupTile.GetComponent<Renderer>().material = defaultColor;

        hasStarted = false; // Make has started false so player can step on triggerTile again
        pathVisible = false;
    }

    // The events that happen when a tile is stepped on
    public void OnTileStepped(GameObject tile)
    {
        if (pathVisible) return; //Prevent input during path display

        // Ignore the startup tile, especially when initiating the game
        if (tile == startupTile)
        {
            Debug.Log("Startup tile triggered & ignored.");
            return;
        }

        // Stepped on a correct tile
        if (tile == pathTiles[currentStepIndex])
        {
            Debug.Log("correct tile: Step" + currentStepIndex);
            tile.GetComponent<Renderer>().material = correctColor;
            currentStepIndex++;

            if (currentStepIndex >= pathTiles.Count)
            {
                Debug.Log("Player completed the path!");
            }
        }
        // Stepped on an incorrect tile
        else
        {
            Debug.Log("Wrong Tile, resetting.");
            tile.GetComponent<Renderer>().material = wrongColor; // Show red
            StartCoroutine(ResetAfterDelay());
        }
    }

    // If an incorrect tile is stepped on, show red(^) on the
    // incorrect tile and reset all the tiles back to the default color
    IEnumerator ResetAfterDelay()
    {        
        yield return StartCoroutine(WaitUntilGrounded());
        cC.enabled = false;
        SetTeleportationAreasEnabled(false);

        yield return new WaitForSeconds(1.0f); // Let the red sit

        // Fades screen and resets player position
        yield return StartCoroutine(resetPlayer.TryAgain());

        // Change all tiles to default color
        foreach (GameObject tile in allTiles)
        {
            tile.GetComponent<Renderer>().material = defaultColor;
        }

        // Resets the current step count to start over at the first correct tile
        currentStepIndex = 0;

        // Restore movement
        cC.enabled = true;
        SetTeleportationAreasEnabled(true);
    }

    // Reset the light up sequence
    public void StopLightUpSequence()
    {
        if (lightUpCoroutine != null)
        {
            StopCoroutine(lightUpCoroutine);
            lightUpCoroutine = null;
        }
    }

    void SetTeleportationAreasEnabled(bool enabled)
    {
        UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea[] areas = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea>();
    
        foreach (var area in areas)
        {
            area.enabled = enabled;
        }
    }

    IEnumerator WaitUntilGrounded()
    {
        while (!IsGrounded())
        {
            yield return null; // wait for next frame
        }
    }

    bool IsGrounded()
    {
        // Raycast down from player position a short distance to detect ground
        float rayDistance = 0.2f;
        return Physics.Raycast(player.transform.position, Vector3.down, rayDistance);
    }



    public void ResetGrid()
    {

        if (pathVisible == false) // Only allows for a grid reset outside of an ongoing light up
        {
            StopLightUpSequence();
            hasStarted = false;
            currentStepIndex = 0;

            foreach (GameObject tile in allTiles)
            {
                tile.GetComponent<Renderer>().material = defaultColor;
            }

            startupTile.GetComponent<Renderer>().material = defaultColor;
        }
    }
}