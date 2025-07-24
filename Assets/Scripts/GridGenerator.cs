using System.Collections.Generic; // Add this line
using UnityEngine;

public class GridGenerator : MonoBehaviour
{
    public GameObject cellPrefab;
    public int cellCount = 16; // Changed to 16 for your 4x4 grid
    
    // Add a reference to the GameManager
    public GameManager gameManager;

    void Start()
    {
        GenerateGrid();
    }

    void GenerateGrid()
    {
        // Create a temporary list to hold the new cells
        List<GridCell> createdCells = new List<GridCell>();

        for (int i = 0; i < cellCount; i++)
        {
            // Create the new cell object
            GameObject newCellObject = Instantiate(cellPrefab, transform);
            
            // Get the GridCell component and add it to our temporary list
            createdCells.Add(newCellObject.GetComponent<GridCell>());
        }
        
        // After the grid is built, tell the GameManager to initialize the game
        // and pass it the list of all the cells we just created.
        gameManager.InitializeGame(createdCells);
    }
}