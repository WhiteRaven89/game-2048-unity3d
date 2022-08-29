using mrathod.Input;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityRest;

/// <summary>
/// Holds Level Info
/// </summary>
public class LevelManager : MonoBehaviour
{
    [SerializeField]
    LevelDataSO levelDataHandler = null;

    GameHandler gameHandlerRef = null;

    [SerializeField]
    List<SingleLevelData> lstLevelInfo = new List<SingleLevelData>();

    [SerializeField]
    Transform backGroundTilePrefab = null;

    [SerializeField]
    List<Transform> lstNumbersTilePrefabs = null;

    mrathod.Utility.SimplePool backgroundTilePool = null;
    mrathod.Utility.SimplePool numbersTilePool = null;

    bool isLevelLoaded = false;

    [SerializeField]
    StateManager stateManager = null;

    [SerializeField]
    private float xyDistance = 1;

    [SerializeField]
    private int zDistance = 0;

    InputType inputType;    //  Later refactor

    int[,] availableSlots = null;

    private static int lowestNewTileValue = 2;
    private static int highestNewTileValue = 4;

    int rows = 0;
    int columns = 0;

    private List<GameObject> backgroundTiles; 

    private List<GameObject> numberTiles;

    /// <summary>
    /// Set Level Data from file may be from server else load it from scriptabe objects
    /// </summary>
    /// <param name="levelData"></param>
    public void SetLevelData(string levelData)
    {
        if (string.IsNullOrEmpty(levelData))
        {
            lstLevelInfo = levelDataHandler.GetAllLevelInfo();
        }
        else
        {
            lstLevelInfo = JsonHelper.FromJson<SingleLevelData>(levelData).ToList();
        }
    }

    public void SetGameHandler(GameHandler handler)
    {
        gameHandlerRef = handler;
    }

    public void LoadGameLevel()
    {
        SetLevelData(string.Empty);
        StartCoroutine(LoadGameLevelCoroutine(gameHandlerRef.GetUserLevel()));
    }
    IEnumerator LoadGameLevelCoroutine(int aLevel)
    {
        yield return new WaitUntil(() => lstLevelInfo != null && lstLevelInfo.Count > 0);
        Debug.Log(":: LoadGameLevel :: Trying to Load level : " + aLevel);
        InitializeBackgroundTile(aLevel);
        InitializeSlots();
        CreatetileAtRandomPosition();
        CreatetileAtRandomPosition();
        PrintSlotsAvailable();
        gameHandlerRef.OnSetupCompleted();
    }

    void InitializeBackgroundTile(int aLevel)
    {
        Vector2 gridDimension = lstLevelInfo[aLevel].GetGridData().GetDimension();
        rows = (int)gridDimension.x;
        columns = (int)gridDimension.y;
        int totalTiles = rows * columns;
        backgroundTilePool = new mrathod.Utility.SimplePool(backGroundTilePrefab.gameObject, totalTiles, this.gameObject);

        if (backgroundTiles == null) backgroundTiles = new List<GameObject>();
        else backgroundTiles.Clear();

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                GameObject backgroundTile = backgroundTilePool.Spawn;
                backgroundTile.transform.position = this.transform.position + new Vector3(j + j * xyDistance, i + i * xyDistance, zDistance);
                backgroundTile.transform.rotation = Quaternion.identity;
                //  Make it visible after position
                backgroundTile.SetActive(true);

                BackgroundTile bgscript = backgroundTile.GetComponent<BackgroundTile>();
                bgscript.XCoord = i;
                bgscript.YCoord = j;

                backgroundTiles.Add(backgroundTile);
            }
        }
    }

    void InitializeSlots()
    {
        availableSlots = new int[rows,columns];

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                availableSlots[i, j] = 0;   //  Creating empty slots
            }
        }
    }

    void CreatetileAtRandomPosition()
    {
        if (numberTiles == null) numberTiles = new List<GameObject>();
        
        int value;
        // find out if we are generating a tile with the lowest or highest value
        float highOrLowChance = Random.Range(0f, 0.99f);
        if (highOrLowChance >= 0.9f)
        {
            value = highestNewTileValue;
        }
        else
        {
            value = lowestNewTileValue;
        }

        // attempt to get the starting position
        int x = Random.Range(0, rows);
        int y = Random.Range(0, columns);

        // starting from the random starting position, loop through
        // each cell in the grid until we find an empty position
        bool found = false;
        
        while (!found)
        {
            if (availableSlots[x, y] == 0)
            {
                found = true;
                GameObject obj;

                if (value == lowestNewTileValue)
                {
                    obj = GetNumberTile(lstNumbersTilePrefabs[0], x, y,value);
                    //  Once slot is used flag the slot
                    FlagSlotAsOccupied(x, y);
                }
                else
                {
                    obj = GetNumberTile(lstNumbersTilePrefabs[1], x, y,value);
                    //  Once slot is used flag the slot
                    FlagSlotAsOccupied(x, y);
                }
                numberTiles.Add(obj);
            }

            x++;
            if (x >= rows)
            {
                y++;
                x = 0;
            }

            if (y >= columns)
            {
                y = 0;
            }
        }
    }

    public void OnInputRecieved(InputType inputType)
    {
        this.inputType = inputType;
        gameHandlerRef.OnInputProcessed();
    }

    public void ProcessTileShiftAlgorithm()
    {
        //  After recieving input process it on tiles
        //  Check for Game logic
        if (inputType == InputType.Up) MoveTilesUp();
        else if (inputType == InputType.Down) MoveTilesDown();
        else if (inputType == InputType.Left) MoveTilesLeft();
        else if (inputType == InputType.Right) MoveTilesRight();

        if (IsMoveLeft())
        {
            CheckTilesForMerging();
            CreatetileAtRandomPosition();
            PrintSlotsAvailable();
            gameHandlerRef.MovesAvailable();
        }
        else
        {
            gameHandlerRef.NoMovesLeft();
        }
    }

    void CheckTileStatus()
    {
        CreatetileAtRandomPosition();
    }

    GameObject GetNumberTile(Transform tileToSpawn, int x, int y, int tileValue)
    {
        GameObject tile = null;

        //  Later change to pool condition
        tile = Instantiate(tileToSpawn.gameObject);
        tile.transform.SetParent(this.transform);
        tile.transform.position = this.transform.position + new Vector3(y + y * xyDistance, x + x * xyDistance, zDistance);
        tile.transform.rotation = Quaternion.identity;
        tile.GetComponent<NumberTile>().XCoord = x;
        tile.GetComponent<NumberTile>().YCoord = y;
        tile.GetComponent<NumberTile>().TileValue = tileValue;

        return tile;
    }

    /// <summary>
    /// Mark slot as occupied
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="value"></param>
    void FlagSlotAsOccupied(int x, int y)
    {
        availableSlots[x, y] = 1;
    }

    /// <summary>
    /// Release occupied slot
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    void ReleaseOccupiedSlot(int x, int y)
    {
        availableSlots[x, y] = 0;   //  Slot unoccupied
    }

    void PrintSlotsAvailable()
    {
        string x = string.Empty;

        for (int i = rows - 1; i >= 0; i--)
        {
            for (int j = 0; j < columns; j++)
            {
                if (availableSlots[i, j] == 1) x += "1|";
                else x += "X" + "|";
            }
            x += "\n";
        }
        Debug.Log(x);
    }

    #region Game Logic

    private void MoveTilesLeft()
    {
        for (int x = 0; x < rows; x++)
        {
            for (int y = 1; y < columns; y++) 
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x, y - 1);
            }
        }
        for (int x = 0; x < rows; x++)
        {
            for (int y = 1; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                UpgradeTiles(x, y, x, y - 1);
            }
        }
        for (int x = 0; x < rows; x++)
        {
            for (int y = 1; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x, y - 1);
            }
        }
    }

    private void MoveTilesRight()
    {
        for (int x = 0; x < rows; x++) 
        {
            for (int y = columns - 2; y >= 0; y--)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x, y + 1);
            }
        }
        for (int x = 0; x < rows; x++)
        {
            for (int y = columns - 2; y >= 0; y--)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                UpgradeTiles(x, y, x, y + 1);
            }
        }
        for (int x = 0; x < rows; x++)
        {
            for (int y = columns - 2; y >= 0; y--)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x, y + 1);
            }
        }
    }

    private void MoveTilesUp()
    {
        for (int x = rows - 2; x >= 0; x--)
        {
            for (int y = 0; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : "+x+" Y : "+y+" Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x + 1, y);
            }
        }
        for (int x = rows - 2; x >= 0; x--)
        {
            for (int y = 0; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : "+x+" Y : "+y+" Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                UpgradeTiles(x, y, x + 1, y);
            }
        }
        for (int x = rows - 2; x >= 0; x--)
        {
            for (int y = 0; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : "+x+" Y : "+y+" Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x + 1, y);
            }
        }
    }

    private void MoveTilesDown()
    {
        for (int x = 1; x < rows; x++)
        {
            for (int y = 0; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x - 1, y);
            }
        }
        for (int x = 1; x < rows; x++)
        {
            for (int y = 0; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                UpgradeTiles(x, y, x - 1, y);
            }
        }
        for (int x = 1; x < rows; x++)
        {
            for (int y = 0; y < columns; y++)
            {
                //Debug.Log("Slot availability : X : " + x + " Y : " + y + " Value : " + availableSlots[x, y]);
                if (availableSlots[x, y] == 0) continue;

                MoveTiles(x, y, x - 1, y);
            }
        }
    }

    void MoveTiles(int xCurrentCoord, int yCurrentCoord, int xTargetCoord, int yTargetCoord)
    {
        GameObject currentTile = GetNumberTileBasedOnCoord(xCurrentCoord, yCurrentCoord);
        GameObject targetTile = GetNumberTileBasedOnCoord(xTargetCoord, yTargetCoord);
        GameObject targetGrid = GetBackgroundTileBasedOnCoord(xTargetCoord, yTargetCoord);

        Debug.Log("Tile position : " + currentTile.GetComponent<NumberTile>().ToString());
        Debug.Log("Move position : " + targetGrid.GetComponent<BackgroundTile>().ToString());

        int targetIndex = 0;
        bool targetFound = false;

        if (xCurrentCoord == xTargetCoord)
        {   //  Check for movement in horiontal direction
            targetIndex = yTargetCoord;
            if (yCurrentCoord > yTargetCoord)
            {   //  Check for left movement 
                while (targetIndex >= 0)
                {
                    targetTile = GetNumberTileBasedOnCoord(xTargetCoord, targetIndex);
                    if (targetTile != null)
                    {
                        //  Switch to target tile
                        targetGrid = GetBackgroundTileBasedOnCoord(xTargetCoord, targetIndex + 1);
                        currentTile.transform.position = targetGrid.transform.position;

                        currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                        currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                        ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                        FlagSlotAsOccupied(xTargetCoord, targetIndex + 1);
                        targetFound = true;
                        break;
                    }
                    targetIndex--;
                }

                if(!targetFound)
                {
                    targetGrid = GetBackgroundTileBasedOnCoord(xTargetCoord, 0);
                    //  Switch to end tile
                    currentTile.transform.position = targetGrid.transform.position;

                    currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                    currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                    ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                    FlagSlotAsOccupied(xTargetCoord, 0);
                }
            }
            else
            {   //  Check for right movement
                while (targetIndex < columns)
                {
                    targetTile = GetNumberTileBasedOnCoord(xTargetCoord, targetIndex);
                    if (targetTile != null)
                    {
                        //  Switch to target tile
                        targetGrid = GetBackgroundTileBasedOnCoord(xTargetCoord, targetIndex - 1);
                        currentTile.transform.position = targetGrid.transform.position;

                        currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                        currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                        ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                        FlagSlotAsOccupied(xTargetCoord, targetIndex - 1);
                        targetFound = true;
                        break;
                    }
                    targetIndex++;
                }

                if (!targetFound)
                {
                    targetGrid = GetBackgroundTileBasedOnCoord(xTargetCoord, columns - 1);
                    //  Switch to end tile
                    currentTile.transform.position = targetGrid.transform.position;

                    currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                    currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                    ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                    FlagSlotAsOccupied(xTargetCoord, columns - 1);
                }
            }
        }

        if (yCurrentCoord == yTargetCoord)
        {   //  Check for movement in vertical direction
            targetIndex = xTargetCoord;
            if (xCurrentCoord > xTargetCoord)
            {   //  Check for down movement
                while (targetIndex >= 0)
                {
                    targetTile = GetNumberTileBasedOnCoord(targetIndex, yTargetCoord);
                    if (targetTile != null)
                    {
                        //  Switch to target tile
                        targetGrid = GetBackgroundTileBasedOnCoord(targetIndex + 1, yTargetCoord);
                        currentTile.transform.position = targetGrid.transform.position;

                        currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                        currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                        ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                        FlagSlotAsOccupied(targetIndex + 1, yTargetCoord);
                        targetFound = true;
                        break;
                    }
                    targetIndex--;
                }

                if (!targetFound)
                {
                    targetGrid = GetBackgroundTileBasedOnCoord(0, yTargetCoord);
                    //  Switch to end tile
                    currentTile.transform.position = targetGrid.transform.position;

                    currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                    currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                    ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                    FlagSlotAsOccupied(0, yTargetCoord);
                }
            }
            else
            {   //  Check for up movement
                while (targetIndex < columns)
                {
                    targetTile = GetNumberTileBasedOnCoord(targetIndex, yTargetCoord);
                    if (targetTile != null)
                    {
                        //  Switch to target tile
                        targetGrid = GetBackgroundTileBasedOnCoord(targetIndex - 1, yTargetCoord);
                        currentTile.transform.position = targetGrid.transform.position;

                        currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                        currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                        ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                        FlagSlotAsOccupied(targetIndex - 1, yTargetCoord);
                        targetFound = true;
                        break;
                    }
                    targetIndex++;
                }

                if (!targetFound)
                {
                    targetGrid = GetBackgroundTileBasedOnCoord(rows - 1, yTargetCoord);
                    //  Switch to end tile
                    currentTile.transform.position = targetGrid.transform.position;

                    currentTile.GetComponent<NumberTile>().XCoord = targetGrid.GetComponent<BackgroundTile>().XCoord;
                    currentTile.GetComponent<NumberTile>().YCoord = targetGrid.GetComponent<BackgroundTile>().YCoord;

                    ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                    FlagSlotAsOccupied(rows - 1, yTargetCoord);
                }
            }
        }
    }

    void UpgradeTiles(int xCurrentCoord, int yCurrentCoord, int xTargetCoord, int yTargetCoord)
    {
        GameObject currentTile = GetNumberTileBasedOnCoord(xCurrentCoord, yCurrentCoord);
        GameObject targetTile = GetNumberTileBasedOnCoord(xTargetCoord, yTargetCoord);
        GameObject targetGrid = GetBackgroundTileBasedOnCoord(xTargetCoord, yTargetCoord);

        if(targetTile != null)
        {
            //  Check for upgrade
            if(CanUpgradeTile(currentTile, targetTile))
            {
                //  Upgrade Tile
                GameObject upgradedTile = GetNumberTile(lstNumbersTilePrefabs[SpawnTileIndex(currentTile.GetComponent<NumberTile>().TileValue *= 2)], 0, 0, currentTile.GetComponent<NumberTile>().TileValue);

                upgradedTile.transform.position = targetTile.transform.position;
                upgradedTile.GetComponent<NumberTile>().XCoord = targetTile.GetComponent<NumberTile>().XCoord;
                upgradedTile.GetComponent<NumberTile>().YCoord = targetTile.GetComponent<NumberTile>().YCoord;
                upgradedTile.GetComponent<NumberTile>().IsMerged = true;


                //  Remove current tile from the list
                RemoveTileFromList(currentTile.GetComponent<NumberTile>().XCoord, currentTile.GetComponent<NumberTile>().YCoord);

                //  Replace target tile in the list
                ReplaceTileInTheList(targetTile.GetComponent<NumberTile>().XCoord, targetTile.GetComponent<NumberTile>().YCoord, upgradedTile);

                //  Destroy current object
                Destroy(currentTile);

                //  Destroy target Object
                Destroy(targetTile);

                ReleaseOccupiedSlot(xCurrentCoord, yCurrentCoord);
                FlagSlotAsOccupied(xTargetCoord, yTargetCoord);
            }
        }
    }

    bool CanUpgradeTile(GameObject currTileObj, GameObject targeTileObj)
    {
        NumberTile currentTile = currTileObj.GetComponent<NumberTile>();
        NumberTile targetTile = targeTileObj.GetComponent<NumberTile>();

        if (currentTile.TileValue == targetTile.TileValue && !currentTile.IsMerged && !targetTile.IsMerged)
            return true;

        return false;
    }

    #endregion

    GameObject GetBackgroundTileBasedOnCoord(int x, int y)
    {
        GameObject bgTileObj = null;

        bgTileObj = backgroundTiles.Find(t => t.GetComponent<BackgroundTile>().XCoord == x && t.GetComponent<BackgroundTile>().YCoord == y);

        return bgTileObj;
    }

    GameObject GetNumberTileBasedOnCoord(int x, int y)
    {
        GameObject numberTile = null;

        numberTile = numberTiles.Find(t => t.GetComponent<NumberTile>().XCoord == x && t.GetComponent<NumberTile>().YCoord == y);

        return numberTile;
    }

    void RemoveTileFromList(int x, int y)
    {
        numberTiles.RemoveAt(numberTiles.IndexOf(numberTiles.Find(t => t.GetComponent<NumberTile>().XCoord == x && t.GetComponent<NumberTile>().YCoord == y)));
    }

    void ReplaceTileInTheList(int x, int y, GameObject newTileObj)
    {
        int indexOfObject = numberTiles.IndexOf(numberTiles.Find(t => t.GetComponent<NumberTile>().XCoord == x && t.GetComponent<NumberTile>().YCoord == y));
        numberTiles[indexOfObject] = newTileObj;
    }

    int SpawnTileIndex(int powerOf2Value)
    {
        switch (powerOf2Value)
        {
            case 2: return 0;   //  tile of 2
            case 4: return 1;   //  tile of 4
            case 8: return 2;   //  tile of 8
            case 16: return 3;   //  tile of 16
            case 32: return 4;   //  tile of 32
            case 64: return 5;   //  tile of 64
            case 128: return 6;   //  tile of 128
            case 256: return 7;   //  tile of 256
            case 512: return 8;   //  tile of 512
            case 1024: return 9;   //  tile of 1204
            default:
                break;
        }

        return 0;
    }

    void CheckTilesForMerging()
    {
        for (int i = 0; i < numberTiles.Count; i++)
        {
            numberTiles[i].GetComponent<NumberTile>().IsMerged = false;
        }
    }

    /// <summary>
    /// If any move left then move ahead or else game over
    /// </summary>
    /// <returns></returns>
    bool IsMoveLeft()
    {
        if (numberTiles.Count < rows * columns)
        {
            return true;
        }

        for (int x = 0; x < rows; x++)
        {
            for (int y = 0; y < columns; y++)
            {
                NumberTile currentTile = GetNumberTileBasedOnCoord(x, y).GetComponent<NumberTile>();
                NumberTile rightTile = GetNumberTileBasedOnCoord(x , y + 1).GetComponent<NumberTile>();
                NumberTile upTile = GetNumberTileBasedOnCoord(x + 1 , y).GetComponent<NumberTile>();

                if (x != rows - 1 && currentTile.TileValue == rightTile.TileValue)
                {
                    return true;
                }
                else if (y != columns - 1 && currentTile.TileValue == upTile.TileValue)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
