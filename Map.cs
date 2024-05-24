using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

public struct Coppia
{
    public int xValue { get; }
    public int yValue { get; }
    public int zValue { get; }

    public Coppia(int primo, int secondo, int terzo)
    {
        xValue = primo;
        yValue = secondo;
        zValue = terzo;
    }

    public Coppia((int,int,int) pos)
    {
        xValue = pos.Item1;
        yValue = pos.Item2;
        zValue = pos.Item3;
    }

    public Coppia (Vector3 pos)
    {
        xValue = Mathf.RoundToInt(pos.x);
        yValue = Mathf.RoundToInt(pos.y);
        zValue = Mathf.RoundToInt(pos.z);
    }

    public override bool Equals(object obj)
    {
        if (!(obj is Coppia))
            return false;

        Coppia other = (Coppia)obj;
        return xValue == other.xValue && yValue == other.yValue && zValue==other.zValue;
    }

    public override int GetHashCode()
    {
        return xValue * 31 + yValue + zValue;
    }

    public override string ToString()
    {
        return $"({xValue}, {yValue})";
    }

    public int GetXValue()
    {
        return xValue;
    }
    public int GetYValue()
    {
        return yValue;
    }

    public int GetZValue()
    {
        return zValue;
    }
}
public enum CellType
{
    Empty,
    Platform,
    Stair
}

public class Map : MonoBehaviour
{

    public int platformNum = 100;
    public int platformToGenerate = 200;
    public const int upFloorYPos = 2;
    public const int downFloorYpos = 0;
    public const int roomSize = 3;
    Dictionary<Coppia, CellType> gridDown;
    Dictionary<Coppia, CellType> gridUp;

    [SerializeField]
    MapPiece stair;
    [SerializeField]
    Platform platformPrefab;
    [SerializeField]
    MapPiece emptyPiece;

    public Color gizmoColor = Color.yellow;
    public float gizmoSize = 0.1f;

    [SerializeField]
    Material greeMat;
    [SerializeField]
    Material redMat;
    [SerializeField]
    Material orangeMat;
    [SerializeField]
    Material purpleMat;
    [SerializeField]
    Material blackMat;

    [SerializeField]
    InventoryItem potion;
    [SerializeField]
    InventoryItem weapon;
    [SerializeField]
    InventoryItem lantern;

    [SerializeField]
    SpikeTrap spikeTrap;
    [SerializeField]
    BallTrap ballTrap;

    [SerializeField]
    GameObject ropeStart;

    public NavMeshSurface surface;

    [SerializeField]
    MapPiece start;
    MapPiece lastCreatePlatform;
    List<MapPiece> spawnedPlatform = new List<MapPiece>();

    public int hallWayLenght = 3;
    private static int _platformCount = 0;

    Player player;

    private void Start()
    {
        while (_platformCount < platformToGenerate)
        {

            // Genero la griglia
            gridDown = new Dictionary<Coppia, CellType>();
            gridUp = new Dictionary<Coppia, CellType>();
            _platformCount = 0;
            spawnedPlatform.Clear();
            lastCreatePlatform = null;

            //if (start != null)
            //{
               // Destroy(start.transform.gameObject);
            //}

            // Itera sui figli del GameObject passato come parametro
            foreach (Transform child in transform)
            {
                // Distrugge il GameObject figlio
                Destroy(child.gameObject);
            }
            GenerateGrid();

            //Creo primo elemento
            var firstElement = GetFirstElement(gridDown);

            gridDown[firstElement.Key] = CellType.Platform;
            lastCreatePlatform = Instantiate(start, new Vector3(firstElement.Key.xValue, downFloorYpos, firstElement.Key.yValue), Quaternion.identity, transform);

            //lastCreatePlatform = start;
            spawnedPlatform.Add(lastCreatePlatform);

            player = FindObjectOfType<Player>();
            player.transform.position = spawnedPlatform[0].transform.position + new Vector3(0, 1, 0);
            //Genero tutti gli altri elementi

            GeneratePlatfrom(gridDown);
        }
       
        RemoveAdjacentWalls();
        FindCorners();
        Findhallway();
        FindPlatformToSpawnFloorTrap();

        surface.BuildNavMesh();

        ropeStart.transform.position = spawnedPlatform[0].GetComponentInChildren<RopeStart>().transform.position;
    }

    private void GenerateGrid()
    {
        for (int x = 0; x < platformNum; x += roomSize)
        {
            for (int z = 0; z < platformNum; z += roomSize)
            {
                gridDown.Add(new Coppia(x, downFloorYpos, z), CellType.Empty);
                gridUp.Add(new Coppia(x, upFloorYPos, z), CellType.Empty);
            }
        }
    }

    public void GeneratePlatfrom(Dictionary<Coppia, CellType> dictionary)
    {
        bool generated = false;
        (int, int, int) pos = (0, 0, 0);
        bool isStair = lastCreatePlatform.GetComponent<Stair>() != null ? true : false;

        // Condizione di uscita per evitare un ciclo infinito
        if (dictionary == null || _platformCount >= 800) // Ad esempio, massimo 100 piattaforme
        {
            return;
        }

        if (isStair)
        {
            if (dictionary == gridDown)
            {
                Stair newStair = lastCreatePlatform.GetComponent<Stair>();
                newStair.FindPlatformsArroundDown();
                pos = newStair.GetRandomPlatform(dictionary);
            }
            else
            {
                Stair newStair = lastCreatePlatform.GetComponent<Stair>();
                newStair.FindPlatformsArroundUp();
                pos = newStair.GetRandomPlatform(dictionary);
            }
        }
        else
        {
            pos = lastCreatePlatform.GetRandomPlatform(dictionary);
        }

        bool nodeIsUsed = CheckIfTheNodeIsUsed(pos, dictionary);


        if (nodeIsUsed == false) // se non è utilizzato crea la piattaforma
        {

            lastCreatePlatform = Instantiate(
                      platformPrefab,
                      new Vector3(pos.Item1, pos.Item2, pos.Item3),
                      Quaternion.identity,
                      transform
                  );
            spawnedPlatform.Add(lastCreatePlatform);
            _platformCount++;

            generated = true;
            dictionary[new Coppia(pos.Item1, pos.Item2, pos.Item3)] =  CellType.Platform;

            if (isStair)
            {
                SetStairRotation(
                    spawnedPlatform[spawnedPlatform.Count - 2].GetComponent<Stair>(),
                    spawnedPlatform[spawnedPlatform.Count - 2].transform.position,
                    spawnedPlatform[spawnedPlatform.Count - 1].transform.position,// end point
                    gridUp,
                    gridDown
                    );                   
            }
        }

        if (generated)
        {
            GeneratePlatfrom(dictionary);
        }
        else
        {
            GoToOtherFloor(dictionary);
        }

        return;

    }

    private void GoToOtherFloor(Dictionary<Coppia, CellType> grigliaInUso)
    {
        Dictionary<Coppia, CellType> otherGrid;
        int height;
        bool platformUp;

        //Vado in un piano diverso
        if (gridDown == grigliaInUso)
        {
            otherGrid = gridUp;
            height = Mathf.RoundToInt(lastCreatePlatform.transform.position.y + 2);
            platformUp = true;
        }
        else
        {
            otherGrid = gridDown;

            height = Mathf.RoundToInt(lastCreatePlatform.transform.position.y - 2);
            platformUp = false;
        }

        bool nodeIsUsed;
        if (otherGrid.ContainsKey(new Coppia(
                    Mathf.RoundToInt(lastCreatePlatform.transform.position.x),
                     Mathf.RoundToInt(height),
                     Mathf.RoundToInt(lastCreatePlatform.transform.position.z)
                     )))
        {

            nodeIsUsed = otherGrid[
                    new Coppia(
                        Mathf.RoundToInt(lastCreatePlatform.transform.position.x),
                         Mathf.RoundToInt(height),
                         Mathf.RoundToInt(lastCreatePlatform.transform.position.z)
                         )
                    ] != 0 ? true : false;
        }
        else
        {
            nodeIsUsed = true;
        }

        if (nodeIsUsed == false)
        {
            lastCreatePlatform = Instantiate(
                  stair,
                  new Vector3(
                     Mathf.RoundToInt(lastCreatePlatform.transform.position.x),
                     Mathf.RoundToInt(platformUp ? height - upFloorYPos / 2 : height + upFloorYPos / 2),
                     Mathf.RoundToInt(lastCreatePlatform.transform.position.z)),
                  Quaternion.identity,
                  transform
              );
                //problema
            spawnedPlatform.Add(lastCreatePlatform);

            Stair instantiatedstair = lastCreatePlatform.GetComponent<Stair>();

            gridUp[new Coppia((int)lastCreatePlatform.transform.position.x, (int)instantiatedstair.upPoint.transform.position.y, (int)lastCreatePlatform.transform.position.z)] = CellType.Stair;

            gridDown[new Coppia((int)lastCreatePlatform.transform.position.x, (int)instantiatedstair.downPoint.transform.position.y, (int)lastCreatePlatform.transform.position.z)] = CellType.Stair;

            Destroy(spawnedPlatform[spawnedPlatform.Count - 2].gameObject);

            GeneratePlatfrom(otherGrid);
        }
    }

    private KeyValuePair<Coppia, CellType> GetFirstElement(Dictionary<Coppia, CellType> dictionary)
    {
        foreach (var element in dictionary)
        {
            return element;  // Ritorna il primo elemento trovato
        }
        return default;  // Restituisce il valore predefinito se il dizionario è vuoto
    }

    private bool CheckIfTheNodeIsUsed((int, int, int) node, Dictionary<Coppia, CellType> dictionary)
    {
        if (dictionary.ContainsKey(new Coppia(node.Item1, node.Item2, node.Item3)))
        {
            if (dictionary[new Coppia(node.Item1, node.Item2, node.Item3)] != CellType.Empty)
            {
                //se è utilizzato
                return true;
            }
            //false se non è utilizzato e possiamo utilizzarlo
            return false;
        }

        //se il nodo non è presente nella griglia
        return true;
    }

    private void RemoveAdjacentWalls()
    {
        foreach (var cell in gridDown)
        {
            if (cell.Value == CellType.Platform ) // Se c'è una piattaforma in questa cella
            {
                Coppia currentPos = cell.Key;

                // Controllo le stanze adiacenti
                Coppia northPos = new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize);
                Coppia southPos = new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue - roomSize);
                Coppia eastPos = new Coppia(currentPos.xValue + roomSize, currentPos.yValue, currentPos.zValue);
                Coppia westPos = new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue);

                Room currentRoom = GetRoomAtPosition(currentPos);

                if (gridDown.ContainsKey(northPos))
                {
                    if (gridDown[northPos] == CellType.Platform || gridDown[northPos] == CellType.Stair)
                    {
                        Room northRoom = GetRoomAtPosition(northPos);
                        if (northRoom != null)
                        {
                            currentRoom.RemoveWall("N");
                            northRoom.RemoveWall("S");
                        }
                    }
                }

                if (gridDown.ContainsKey(southPos))
                {
                    if (gridDown[southPos] == CellType.Platform || gridDown[southPos] == CellType.Stair)
                    {
                        Room southRoom = GetRoomAtPosition(southPos);
                        if (southRoom != null)
                        {
                            currentRoom.RemoveWall("S");
                            southRoom.RemoveWall("N");
                        }
                    }
                }

                if (gridDown.ContainsKey(eastPos))
                {
                    if (gridDown[eastPos] == CellType.Platform || gridDown[eastPos] == CellType.Stair)
                    {
                        Room eastRoom = GetRoomAtPosition(eastPos);
                        if (eastRoom != null)
                        {
                            currentRoom.RemoveWall("E");
                            eastRoom.RemoveWall("W");
                        }
                    }
                }

                if (gridDown.ContainsKey(westPos))
                {
                    if (gridDown[westPos] == CellType.Platform || gridDown[westPos] == CellType.Stair)
                    {
                        Room westRoom = GetRoomAtPosition(westPos);
                        if (westRoom != null)
                        {
                            currentRoom.RemoveWall("W");
                            westRoom.RemoveWall("E");
                        }
                    }
                }
            }
        }

        foreach (var cell in gridUp)
        {
            if (cell.Value == CellType.Platform) // Se c'è una piattaforma in questa cella
            {
                Coppia currentPos = cell.Key;

                // Controllo le stanze adiacenti
                Coppia northPos = new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize);
                Coppia southPos = new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue - roomSize);
                Coppia eastPos = new Coppia(currentPos.xValue + roomSize, currentPos.yValue, currentPos.zValue);
                Coppia westPos = new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue);

                Room currentRoom = GetRoomAtPosition(currentPos);

                if (gridUp.ContainsKey(northPos))
                {
                    if (gridUp[northPos] == CellType.Platform || gridUp[northPos] == CellType.Stair)
                    {

                        Room northRoom = GetRoomAtPosition(northPos);
                        if (northRoom != null)
                        {
                            currentRoom.RemoveWall("N");
                            northRoom.RemoveWall("S");
                        }
                    }
                }

                if (gridUp.ContainsKey(southPos)) {

                    if (gridUp[southPos] == CellType.Platform || gridUp[southPos] == CellType.Stair)
                    {
                        Room southRoom = GetRoomAtPosition(southPos);
                        if (southRoom != null)
                        {
                            currentRoom.RemoveWall("S");
                            southRoom.RemoveWall("N");
                        }
                    }
                }

                if (gridUp.ContainsKey(eastPos)) {

                    if (gridUp[eastPos] == CellType.Platform || gridUp[eastPos] == CellType.Stair)
                    {

                        Room eastRoom = GetRoomAtPosition(eastPos);
                        if (eastRoom != null)
                        {
                            currentRoom.RemoveWall("E");
                            eastRoom.RemoveWall("W");
                        }
                    }
                }

                if (gridUp.ContainsKey(westPos))
                {
                    if (gridUp[westPos] == CellType.Platform || gridUp[westPos] == CellType.Stair)
                    {
                        Room westRoom = GetRoomAtPosition(westPos);
                        if (westRoom != null)
                        {
                            currentRoom.RemoveWall("W");
                            westRoom.RemoveWall("E");
                        }
                    }
                }
            }
        }
    }

    private Room GetRoomAtPosition(Coppia pos)
    {
        foreach (var room in spawnedPlatform)
        {
            if (room.GetComponent<Platform>())
            {
                if (Mathf.RoundToInt(room.transform.position.x) == pos.xValue &&
                    Mathf.RoundToInt(room.transform.position.y) == pos.yValue &&
                    Mathf.RoundToInt(room.transform.position.z) == pos.zValue)
                {
                    return room.GetComponent<Room>();
                }
            }
        }
        return null;
    }

    private void SetStairRotation(Stair stair, Vector3 startPoint, Vector3 endPoint, Dictionary<Coppia, CellType> dictionaryUp, Dictionary<Coppia, CellType> dictionaryDown) {

        bool canCreateStair = false;


        int xPos =(int)stair.transform.position.x;
        int zPos = (int)stair.transform.position.z;

        Coppia rightDownGrid = new Coppia(xPos-roomSize,downFloorYpos,zPos);
        Coppia LeftDownGrid = new Coppia(xPos +roomSize, downFloorYpos, zPos);
        Coppia DownDownGrid = new Coppia(xPos, downFloorYpos, zPos-roomSize);
        Coppia UpDownGrid = new Coppia(xPos, downFloorYpos, zPos + roomSize);

        Coppia rightUpGrid = new Coppia(xPos - roomSize, upFloorYPos, zPos);
        Coppia LeftUpGrid = new Coppia(xPos + roomSize, upFloorYPos, zPos);
        Coppia DownUpGrid = new Coppia(xPos, upFloorYPos, zPos - roomSize);
        Coppia UpUpGrid = new Coppia(xPos, upFloorYPos, zPos + roomSize);

        
        if (startPoint.y < endPoint.y)
        {
            if (dictionaryDown.ContainsKey(rightDownGrid) && dictionaryDown[rightDownGrid] == CellType.Platform
                &&
                dictionaryUp.ContainsKey(LeftUpGrid) && dictionaryUp[LeftUpGrid] == CellType.Platform)
            {
                startPoint = new Vector3(rightDownGrid.xValue, rightDownGrid.yValue, rightDownGrid.zValue);
                endPoint = new Vector3(LeftUpGrid.xValue, LeftUpGrid.yValue, LeftUpGrid.zValue);
                canCreateStair = true;
            }
            else if (
               dictionaryDown.ContainsKey(LeftDownGrid) && dictionaryDown[LeftDownGrid] == CellType.Platform
               &&
               dictionaryUp.ContainsKey(rightUpGrid) && dictionaryUp[rightUpGrid] == CellType.Platform)
            {
                startPoint = new Vector3(LeftDownGrid.xValue, LeftDownGrid.yValue, LeftDownGrid.zValue);
                endPoint = new Vector3(rightUpGrid.xValue, rightUpGrid.yValue, rightUpGrid.zValue);
                canCreateStair = true;
            }
            else if (
                dictionaryDown.ContainsKey(DownDownGrid) && dictionaryDown[DownDownGrid] == CellType.Platform
                &&
                dictionaryUp.ContainsKey(UpUpGrid) && dictionaryUp[UpUpGrid] == CellType.Platform)
            {
                startPoint = new Vector3(DownDownGrid.xValue, DownDownGrid.yValue, DownDownGrid.zValue);
                endPoint = new Vector3(UpUpGrid.xValue, UpUpGrid.yValue, UpUpGrid.zValue);
                canCreateStair = true;
            }
            else if (
                dictionaryDown.ContainsKey(UpDownGrid) && dictionaryDown[UpDownGrid] == CellType.Platform
                &&
                dictionaryUp.ContainsKey(DownUpGrid) && dictionaryUp[DownUpGrid] == CellType.Platform)
            {
                startPoint = new Vector3(UpDownGrid.xValue, UpDownGrid.yValue, UpDownGrid.zValue);
                endPoint = new Vector3(DownUpGrid.xValue, DownUpGrid.yValue, DownUpGrid.zValue);
                canCreateStair = true;
            }
        }
        else
        {
            if (dictionaryUp.ContainsKey(rightUpGrid) && dictionaryUp[rightUpGrid] == CellType.Platform &&
                       dictionaryDown.ContainsKey(LeftDownGrid) && dictionaryDown[LeftDownGrid] == CellType.Platform)
            {
                startPoint = new Vector3(rightUpGrid.xValue, rightUpGrid.yValue, rightUpGrid.zValue);
                endPoint = new Vector3(LeftDownGrid.xValue, LeftDownGrid.yValue, LeftDownGrid.zValue);
                canCreateStair = true;
            }
            else if (dictionaryUp.ContainsKey(LeftUpGrid) && dictionaryUp[LeftUpGrid] == CellType.Platform &&
                     dictionaryDown.ContainsKey(rightDownGrid) && dictionaryDown[rightDownGrid] == CellType.Platform)
            {
                startPoint = new Vector3(LeftUpGrid.xValue, LeftUpGrid.yValue, LeftUpGrid.zValue);
                endPoint = new Vector3(rightDownGrid.xValue, rightDownGrid.yValue, rightDownGrid.zValue);
                canCreateStair = true;
            }
            else if (dictionaryUp.ContainsKey(DownUpGrid) && dictionaryUp[DownUpGrid] == CellType.Platform &&
                     dictionaryDown.ContainsKey(UpDownGrid) && dictionaryDown[UpDownGrid] == CellType.Platform)
            {
                startPoint = new Vector3(DownUpGrid.xValue, DownUpGrid.yValue, DownUpGrid.zValue);
                endPoint = new Vector3(UpDownGrid.xValue, UpDownGrid.yValue, UpDownGrid.zValue);
                canCreateStair = true;
            }
            else if (dictionaryUp.ContainsKey(UpUpGrid) && dictionaryUp[UpUpGrid] == CellType.Platform &&
                     dictionaryDown.ContainsKey(DownDownGrid) && dictionaryDown[DownDownGrid] == CellType.Platform)
            {
                startPoint = new Vector3(UpUpGrid.xValue, UpUpGrid.yValue, UpUpGrid.zValue);
                endPoint = new Vector3(DownDownGrid.xValue, DownDownGrid.yValue, DownDownGrid.zValue);
                canCreateStair = true;
            }
        }

        if (canCreateStair) {

            foreach(MapPiece obj in spawnedPlatform)
            {
                if(obj.transform.position == startPoint)
                {
                    var material = obj.GetComponent<MeshRenderer>().materials;
                    material[0] = redMat;
                    obj.GetComponent<MeshRenderer>().materials = material;


                }
                else if(obj.transform.position == endPoint)
                {
                    var material = obj.GetComponent<MeshRenderer>().materials;
                    material[0] = greeMat;
                    obj.GetComponent<MeshRenderer>().materials = material;
                }
            }

          
            // Calcola la direzione della scala
            Vector3 direction = endPoint - startPoint;

            // Calcola la rotazione basata sulla direzione
            Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

            Quaternion yRotation;

           if (startPoint.y > endPoint.y)// scala che va dal alto verso il basso
            {
                yRotation = Quaternion.Euler(0, rotation.eulerAngles.y -180, 0);
            }
            else// scala che va dall alto verso il basso
            {
                yRotation = Quaternion.Euler(0, rotation.eulerAngles.y, 0);
            }

            stair.transform.rotation = yRotation;

            stair.RemoveWall();

        }else
        {
            gridUp[new Coppia((int)stair.transform.position.x, (int)stair.upPoint.transform.position.y, (int)stair.transform.position.z)] = CellType.Empty;

            gridDown[new Coppia((int)stair.transform.position.x, (int)stair.downPoint.transform.position.y, (int)stair.transform.position.z)] = CellType.Empty;

            Destroy(stair.gameObject);
        }
    }

    private void Findhallway()
    {
        List<List<Platform>> hallways = new List<List<Platform>>();

        foreach (var node in gridUp)
        {
            if (node.Value == CellType.Platform)
            {
                //----------RIGHT---------------------------------
                Coppia currentPos = new Coppia(node.Key.xValue - roomSize, node.Key.yValue, node.Key.zValue);

                List<Platform> hallway1 = new List<Platform>();
                while (gridUp.ContainsKey(currentPos) && gridUp[currentPos] == CellType.Platform)
                {

                    bool isRightEmpty = gridUp.ContainsKey(new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize)) &&
                               gridUp[new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize)] == CellType.Empty;
                    bool isLeftEmpty = gridUp.ContainsKey(new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue - roomSize)) &&
                                       gridUp[new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue - roomSize)] == CellType.Empty;


                    if (isRightEmpty && isLeftEmpty)
                    {
                        foreach (MapPiece platform in spawnedPlatform)
                        {
                            if (platform.transform.position == new Vector3(currentPos.xValue, currentPos.yValue, currentPos.zValue))
                                hallway1.Add(platform.GetComponent<Platform>());
                        }

                        currentPos = new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue);
                    }
                    else
                    {
                        break;
                    }
                }

                if (hallway1.Count >= hallWayLenght)
                {
                    foreach (var platform in hallway1)
                    {
                        var material = platform.GetComponent<MeshRenderer>().materials;
                        material[0] = orangeMat;
                        platform.GetComponent<MeshRenderer>().materials = material;
                    }

                    hallways.Add(hallway1);

                }

 //               ----------UP-------------------------------- -
                currentPos = new Coppia(node.Key.xValue, node.Key.yValue, node.Key.zValue + roomSize);
                List<Platform> hallway2 = new List<Platform>();

                while (gridUp.ContainsKey(currentPos) && gridUp[currentPos] == CellType.Platform)
                {
                    bool isRightEmpty = gridUp.ContainsKey(new Coppia(currentPos.xValue + roomSize, currentPos.yValue, currentPos.zValue)) &&
                               gridUp[new Coppia(currentPos.xValue + roomSize, currentPos.yValue, currentPos.zValue)] == CellType.Empty;
                    bool isLeftEmpty = gridUp.ContainsKey(new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue)) &&
                                       gridUp[new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue)] == CellType.Empty;


                    if (isRightEmpty && isLeftEmpty)
                    {
                        foreach (MapPiece platform in spawnedPlatform)
                        {
                            if (platform.transform.position == new Vector3(currentPos.xValue, currentPos.yValue, currentPos.zValue))
                                hallway2.Add(platform.GetComponent<Platform>());
                        }

                        currentPos = new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize);
                    }
                    else
                    {
                        break;
                    }
                }

                if (hallway2.Count >= hallWayLenght)
                {
                    foreach (var platform in hallway2)
                    {
                        var material = platform.GetComponent<MeshRenderer>().materials;
                        material[0] = orangeMat;
                        platform.GetComponent<MeshRenderer>().materials = material;
                    }

                    hallways.Add(hallway2);
                }
            }
        }


        foreach (var node in gridDown)
        {
            if (node.Value == CellType.Platform)
            {
                List<Platform> hallway1 = new List<Platform>();

                //----------RIGHT---------------------------------
                Coppia currentPos = new Coppia(node.Key.xValue - roomSize, node.Key.yValue, node.Key.zValue);


                while (gridDown.ContainsKey(currentPos) && gridDown[currentPos] == CellType.Platform)
                {
                    bool isRightEmpty = gridDown.ContainsKey(new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize)) &&
                               gridDown[new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize)] == CellType.Empty;

                    bool isLeftEmpty = gridDown.ContainsKey(new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue - roomSize)) &&
                                       gridDown[new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue -
                                       roomSize)] == CellType.Empty;


                    if (isRightEmpty && isLeftEmpty)
                    {
                        foreach (MapPiece platform in spawnedPlatform)
                        {
                            if (platform.transform.position == new Vector3(currentPos.xValue, currentPos.yValue, currentPos.zValue))
                                hallway1.Add(platform.GetComponent<Platform>());
                        }

                        currentPos = new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue);
                    }
                    else
                    {
                        break;
                    }
                }
                if (hallway1.Count > hallWayLenght)
                {
                    foreach (var platform in hallway1)
                    {
                        var material = platform.GetComponent<MeshRenderer>().materials;
                        material[0] = orangeMat;
                        platform.GetComponent<MeshRenderer>().materials = material;
                    }
                    hallways.Add(hallway1);

                }

 //               ----------UP-------------------------------- -
                currentPos = new Coppia(node.Key.xValue, node.Key.yValue, node.Key.zValue + roomSize);
                List<Platform> hallway2 = new List<Platform>();

                while (gridDown.ContainsKey(currentPos) && gridDown[currentPos] == CellType.Platform)
                {
                    bool isRightEmpty = gridDown.ContainsKey(new Coppia(currentPos.xValue + roomSize, currentPos.yValue, currentPos.zValue)) &&
                               gridDown[new Coppia(currentPos.xValue + roomSize, currentPos.yValue, currentPos.zValue)] == CellType.Empty;
                    bool isLeftEmpty = gridDown.ContainsKey(new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue)) &&
                                       gridDown[new Coppia(currentPos.xValue - roomSize, currentPos.yValue, currentPos.zValue)] == CellType.Empty;


                    if (isRightEmpty && isLeftEmpty)
                    {
                        foreach (MapPiece platform in spawnedPlatform)
                        {
                            if (platform.transform.position == new Vector3(currentPos.xValue, currentPos.yValue, currentPos.zValue))
                                hallway2.Add(platform.GetComponent<Platform>());
                        }

                        currentPos = new Coppia(currentPos.xValue, currentPos.yValue, currentPos.zValue + roomSize);
                    }
                    else
                    {
                        break;
                    }
                }

                if (hallway2.Count >= hallWayLenght)
                {
                    foreach (var platform in hallway2)
                    {
                        var material = platform.GetComponent<MeshRenderer>().materials;
                        material[0] = orangeMat;
                        platform.GetComponent<MeshRenderer>().materials = material;
                    }
                    hallways.Add(hallway2);

                }
            }
        }

        for (int i = 0; i < 2; i++)
        {
            if (hallways.Count > 0)
            {
                Debug.Log("count of hallways" + hallways.Count);
                int index = Random.Range(0, hallways.Count - 1);
                List<Platform> hallway = hallways[index];
                BallTrap ball = Instantiate(
                    ballTrap,
                    hallway[0].transform.position + new Vector3(0, 1, 0),
                    Quaternion.identity,
                    transform
                );
                SetBallRotation(ball, hallway[0].transform.position, hallway[hallway.Count-1].transform.position);

                hallways.Remove(hallways[index]);
            }
        }

    }

    private void SetBallRotation(BallTrap ball, Vector3 startPoint, Vector3 endPoint)
    {
        // Calcola la direzione della palla
        Vector3 direction = endPoint - startPoint;

        // Calcola la rotazione basata sulla direzione
        Quaternion rotation = Quaternion.LookRotation(direction, Vector3.up);

        ball.transform.rotation = rotation;

    }

    private void FindCorners()
    {
        List<Platform> cornersUp = new List<Platform>();

        foreach (var node in gridUp)
        {
            if (node.Value == CellType.Platform)
            {
                Coppia currentPosition = node.Key;
                int x = currentPosition.xValue;
                int z = currentPosition.zValue;

                // Controlla se ci sono piattaforme adiacenti nelle quattro direzioni cardinali
                bool hasNorth = gridUp.ContainsKey(new Coppia(x, upFloorYPos, z + roomSize)) && gridUp[new Coppia(x, upFloorYPos, z + roomSize)] == CellType.Platform;
                bool hasSouth = gridUp.ContainsKey(new Coppia(x, upFloorYPos, z - roomSize)) && gridUp[new Coppia(x, upFloorYPos, z - roomSize)] == CellType.Platform;
                bool hasEast = gridUp.ContainsKey(new Coppia(x + roomSize, upFloorYPos, z)) && gridUp[new Coppia(x + roomSize, upFloorYPos, roomSize)] == CellType.Platform;
                bool hasWest = gridUp.ContainsKey(new Coppia(x - roomSize, upFloorYPos, z)) && gridUp[new Coppia(x - roomSize, upFloorYPos, roomSize)] == CellType.Platform;

                // Se la piattaforma ha piattaforme adiacenti solo in due direzioni, è un angolo
                int adjacentDirectionsCount = (hasNorth ? 1 : 0) + (hasSouth ? 1 : 0) + (hasEast ? 1 : 0) + (hasWest ? 1 : 0);
                if (adjacentDirectionsCount == 2)
                {
                    // Aggiungi la piattaforma agli angoli trovati
                    foreach (var platform in spawnedPlatform)
                    {
                        if (platform.transform.position == new Vector3(node.Key.xValue, node.Key.yValue, node.Key.zValue))
                        {
                            cornersUp.Add(platform.GetComponent<Platform>());

                            var material = platform.GetComponent<MeshRenderer>().materials;
                            material[0] = purpleMat;
                            platform.GetComponent<MeshRenderer>().materials = material;

                        }

                    }
                }
            }
        }

        List<Platform> cornersDown = new List<Platform>();

        foreach (var node in gridDown)
        {
            if (node.Value == CellType.Platform)
            {
                Coppia currentPosition = node.Key;
                int x = currentPosition.xValue;
                int z = currentPosition.zValue;

                // Controlla se ci sono piattaforme adiacenti nelle quattro direzioni cardinali
                bool hasNorth = gridDown.ContainsKey(new Coppia(x, downFloorYpos, z + roomSize)) && gridDown[new Coppia(x, downFloorYpos, z + roomSize)] == CellType.Platform;
                bool hasSouth = gridDown.ContainsKey(new Coppia(x, downFloorYpos, z - roomSize)) && gridDown[new Coppia(x, downFloorYpos, z - roomSize)] == CellType.Platform;
                bool hasEast = gridDown.ContainsKey(new Coppia(x + roomSize, downFloorYpos, z)) && gridDown[new Coppia(x + roomSize, downFloorYpos, z)] == CellType.Platform;
                bool hasWest = gridDown.ContainsKey(new Coppia(x - roomSize, downFloorYpos, z)) && gridDown[new Coppia(x - roomSize, downFloorYpos, z)] == CellType.Platform;

                // Se la piattaforma ha piattaforme adiacenti solo in due direzioni, è un angolo
                int adjacentDirectionsCount = (hasNorth ? 1 : 0) + (hasSouth ? 1 : 0) + (hasEast ? 1 : 0) + (hasWest ? 1 : 0);
                if (adjacentDirectionsCount == 2)
                {
                    // Aggiungi la piattaforma agli angoli trovati
                    foreach (var platform in spawnedPlatform)
                    {
                        if (platform.transform.position == new Vector3(node.Key.xValue, node.Key.yValue, node.Key.zValue))
                        {
                            cornersUp.Add(platform.GetComponent<Platform>());

                            var material = platform.GetComponent<MeshRenderer>().materials;
                            material[0] = purpleMat;
                            platform.GetComponent<MeshRenderer>().materials = material;

                        }

                    }
                }
            }
        }


    }

    private void FindPlatformToSpawnFloorTrap()
    {
        List<Platform> platforms = new List<Platform>();

        foreach(MapPiece mapPiece in spawnedPlatform)
        {
            Platform platform;

            if (platform = mapPiece.GetComponent<Platform>())
            {
                var platformArround = platform.GetPlatformArround();

                if (platform.gridType == Platform.Grid.Up)
                {
                    bool down = gridUp.ContainsKey(new Coppia(platformArround[0])) && gridUp[new Coppia(platformArround[0])] == CellType.Platform;
                    bool up = gridUp.ContainsKey(new Coppia(platformArround[1])) && gridUp[new Coppia(platformArround[1])] == CellType.Platform;
                    bool right = gridUp.ContainsKey(new Coppia(platformArround[2])) && gridUp[new Coppia(platformArround[2])] == CellType.Platform;
                    bool left = gridUp.ContainsKey(new Coppia(platformArround[3])) && gridUp[new Coppia(platformArround[3])] == CellType.Platform;

                    if ( up && down && left && right)
                    {
                        platforms.Add(platform);
                    }
                }
                else
                {
                    bool down = gridDown.ContainsKey(new Coppia(platformArround[0])) && gridDown[new Coppia(platformArround[0])] == CellType.Platform;
                    bool up = gridDown.ContainsKey(new Coppia(platformArround[1])) && gridDown[new Coppia(platformArround[1])] == CellType.Platform;
                    bool right = gridDown.ContainsKey(new Coppia(platformArround[2])) && gridDown[new Coppia(platformArround[2])] == CellType.Platform;
                    bool left = gridDown.ContainsKey(new Coppia(platformArround[3])) && gridDown[new Coppia(platformArround[3])] == CellType.Platform;

                    if (up && down && left && right)
                    {
                        platforms.Add(platform);
                    }
                }    
            }
        }
        foreach(Platform plat in platforms)
        {
            var platformArround = plat.GetPlatformArround();

            if (plat.gridType == Platform.Grid.Up)
            {
                bool down = gridUp.ContainsKey(new Coppia(platformArround[0])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[0])) == Platform.PlatformType.Trap);
                bool up = gridUp.ContainsKey(new Coppia(platformArround[1])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[1])) == Platform.PlatformType.Trap);
                bool right = gridUp.ContainsKey(new Coppia(platformArround[2])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[2])) == Platform.PlatformType.Trap);
                bool left = gridUp.ContainsKey(new Coppia(platformArround[3])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[3])) == Platform.PlatformType.Trap);

                if (up && down && left && right)
                {
                    plat.type = Platform.PlatformType.Trap;

                    SpikeTrap trap = Instantiate(
                      spikeTrap,
                      plat.transform.position,
                      Quaternion.identity,
                      transform
                  );
                    spawnedPlatform.Remove(plat);

                    spawnedPlatform.Add(trap);

                    trap.type = Platform.PlatformType.Trap;
                    trap.gridType = plat.gridType;
                    //var material = plat.GetComponent<MeshRenderer>().materials;
                    //material[0] = blackMat;
                    //plat.GetComponent<MeshRenderer>().materials = material;
                }
            }
            else
            {
                bool down = gridDown.ContainsKey(new Coppia(platformArround[0])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[0])) == Platform.PlatformType.Trap) ;
                bool up = gridDown.ContainsKey(new Coppia(platformArround[1])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[1])) == Platform.PlatformType.Trap);
                bool right = gridDown.ContainsKey(new Coppia(platformArround[2])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[2])) == Platform.PlatformType.Trap);
                bool left = gridDown.ContainsKey(new Coppia(platformArround[3])) && !(FindSpanwedPlatformTypeAtPosition(new Coppia(platformArround[3])) == Platform.PlatformType.Trap);

                if (up && down && left && right)
                {
                    plat.type = Platform.PlatformType.Trap;
                    var material = plat.GetComponent<MeshRenderer>().materials;
                    material[0] = blackMat;
                    plat.GetComponent<MeshRenderer>().materials = material;
                }
            }          
        }     
    }

    private Platform.PlatformType FindSpanwedPlatformTypeAtPosition(Coppia pos)
    {
        foreach (MapPiece platform in spawnedPlatform) {

            Platform plat;

            if (plat = platform.GetComponent<Platform>()){
                if (new Coppia(plat.transform.position).Equals(pos) && plat.type == Platform.PlatformType.Trap)
                {
                    return Platform.PlatformType.Trap;
                }
            }
        }
        return Platform.PlatformType.Normal;
    }

    //private List<List<Platform>> FindSpawnedLevels()
    //{

    //}


    private void OnDrawGizmos()
    {
        if (gridDown == null || gridUp == null)
            return;

        if (gridDown == null)
            return;

        foreach (var entry in gridDown.Keys)
        {
            Vector3 position = new Vector3(entry.GetXValue(), entry.GetYValue(), entry.GetZValue());
            if (gridDown[entry] == CellType.Platform)
            {
                Gizmos.color = Color.blue;
            }
            else if (gridDown[entry] == CellType.Stair)
            {
                Gizmos.color = Color.black;
            }
            else
            {
                Gizmos.color = Color.yellow;
            }
            Gizmos.DrawSphere(position, gizmoSize);
        }

        foreach (var entry in gridUp.Keys)
        {
            Vector3 position = new Vector3(entry.GetXValue(), entry.GetYValue(), entry.GetZValue());
            if (gridUp[entry] == CellType.Platform)
            {
                Gizmos.color = Color.blue;
            }
            else if (gridUp[entry] == CellType.Stair)
            {
                Gizmos.color = Color.black;
            }
            else
            {
                Gizmos.color = Color.red;
            }

            Gizmos.DrawSphere(position, gizmoSize);
        }
    }




}

