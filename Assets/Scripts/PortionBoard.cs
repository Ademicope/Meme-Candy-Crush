using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

public class PortionBoard : MonoBehaviour
{
    // define the size of the board
    public int width = 6;
    public int height = 8;

    // define some spacing for the board
    public float spacingX;
    public float spacingY;

    // get a reference to the portion prefabs
    public GameObject[] portionPrefabs;

    // get a reference to the collection of node as game objects
    private Node[,] portionBoard;

    public GameObject portionBoardGO;

    public List<GameObject> portionsToDestroy = new();
    public GameObject portionsParent;

    [SerializeField]
    private Portion selectedPortion;

    [SerializeField]
    private bool isProcessingMove;

    [SerializeField]
    List<Portion> portionsToRemove = new();

    // Get a reference to layout array
    public ArrayLayout arrayLayout;
    // Get a public instance of PortionBoard

    public static PortionBoard Instance;

    // Start is called before the first frame update

    private void Awake()
    {
        Instance = this;
    }
    void Start()
    {
        InitializeBoard();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(ray.origin, ray.direction);

            if (hit.collider != null && hit.collider.gameObject.GetComponent<Portion>())
            {
                if (isProcessingMove)
                    return;
                Portion portion = hit.collider.gameObject.GetComponent<Portion>();
                Debug.Log("Clicked a portion");

                SelectPortion(portion);
            }
        }
    }

    private void InitializeBoard()
    {
        DestroyPortions();
        portionBoard = new Node[width, height];

        spacingX = (float)(width - 1) / 2;
        spacingY = (float)(height - 1) / 2;

        for(int y = 0; y < height; y++)
        {
            for(int x = 0; x < width; x++)
            {
                Vector2 pos = new Vector2(x - spacingX, y -spacingY);
                if (arrayLayout.rows[y].row[x])
                {
                    portionBoard[x, y] = new Node(false, null);
                }
                else
                {
                    int randomIndex = Random.Range(0, portionPrefabs.Length);

                    GameObject portion = Instantiate(portionPrefabs[randomIndex], pos, Quaternion.identity);
                    portion.transform.SetParent(portionsParent.transform);
                    portion.GetComponent<Portion>().SetIndices(x, y);
                    portionBoard[x, y] = new Node(true, portion);
                    portionsToDestroy.Add(portion);
                }
                
            }
        }
        if (CheckBoard())
        {
            Debug.Log("We have matches");
            InitializeBoard();
        }
        else
        {
            Debug.Log("there are no matches");
        }
    }

    private void DestroyPortions()
    {
        if (portionsToDestroy != null)
        {
            foreach (GameObject portion in portionsToDestroy)
            {
                Destroy(portion);
            }
            portionsToDestroy.Clear();
        }
    }

    public bool CheckBoard()
    {
        if (GameManager.Instance.isGameEnded)
            return false;
        Debug.Log("Checking board");
        bool hasMatched = false;

        //List<Portion> portionsToRemove = new();

        portionsToRemove.Clear();

        foreach (Node nodePortion in portionBoard)
        {
            if (nodePortion.portion != null)
            {
                nodePortion.portion.GetComponent<Portion>().isMatched = false;
            }
        }
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // checking if portion node is usable
                if (portionBoard[x, y].isUsable)
                {
                    // proceed to get portion class in node
                    Portion portion = portionBoard[x, y].portion.GetComponent<Portion>();

                    // ensure it is not matched
                    if (!portion.isMatched)
                    {
                        //Run matching logic
                        MatchResult matchedPortions = IsConnected(portion);

                        if (matchedPortions.connectedPortions.Count >= 3)
                        {
                            //complex matching
                            MatchResult superMatchedPortions = SuperMatched(matchedPortions);

                            portionsToRemove.AddRange(superMatchedPortions.connectedPortions);

                            foreach (Portion port in superMatchedPortions.connectedPortions)
                                port.isMatched = true;

                            hasMatched = true;
                        }
                    }
                }
            }
        }

        return hasMatched;
    }

    public IEnumerator ProcessTurnOnMatchedBoard(bool _subtractMoves)
    {
        foreach (Portion portionToRemove in portionsToRemove)
        {
            portionToRemove.isMatched = false;
        }

        RemoveAndRefill(portionsToRemove);
        GameManager.Instance.ProcessTurn(portionsToRemove.Count, _subtractMoves);
        yield return new WaitForSeconds(0.4f);

        if (CheckBoard())
        {
            StartCoroutine(ProcessTurnOnMatchedBoard(false));
        }
    }

    #region Cascading portions
    //RemoveAndRefill (List of portions)
    private void RemoveAndRefill(List<Portion> _portionsToRemove)
    {
        //Remove portion and clear board at that location
        foreach (Portion portion in _portionsToRemove)
        {
            // get its x and y indices and store them
            int _xIndex = portion.xIndex;
            int _yIndex = portion.yIndex;

            // destroy portion
            Destroy(portion.gameObject);

            //create a blank node on the portion board
            portionBoard[_xIndex, _yIndex] = new Node(true, null);
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (portionBoard[x, y].portion == null)
                {
                    Debug.Log("The location X: " + x + "Y" + y + "is empty, attempting to refill it");
                    RefillPortion(x, y);
                }
            }
        }
    }

    //RefillPortions
    private void RefillPortion(int x, int y)
    {
        //y offset
        int yOffset = 1;

        // while the cell above current cell is null and we are below the height of the ball
        while (y + yOffset < height && portionBoard[x, y + yOffset].portion == null)
        {
            Debug.Log("cell above current level is null");
            yOffset++;
        }

        // we 've either hit the top of board of found a portion
        if (y + yOffset < height && portionBoard[x, y + yOffset].portion != null)
        {
            Portion portionAbove = portionBoard[x, y + yOffset].portion.GetComponent<Portion>();

            // move to current location
            Vector3 targetPos = new Vector3(x - spacingX, y - spacingY, portionAbove.transform.position.z);
            //move to location
            portionAbove.MoveToTarget(targetPos);
            //update indices
            portionAbove.SetIndices(x, y);
            //update portionBoard
            portionBoard[x, y] = portionBoard[x, y + yOffset];
            //set location the portion came from to null
            portionBoard[x, y + yOffset] = new Node(true, null);
        }

        //if we 've hit top of the board without finding a portion
        if (y + yOffset == height)
        {
            Debug.Log("Reached the top of the board without finding a portion");
            SpawnPortionAtTop(x);
        }
    }

    //SpawnPortionAtTop ()
    private void SpawnPortionAtTop(int x)
    {
        int index = FindIndexOfLowestNull(x);
        int locationToMoveTo = 8 - index;
        Debug.Log("About to spawn a portion");
        //get a random portion
        int randomIndex = Random.Range(0, portionPrefabs.Length);
        GameObject newPortion = Instantiate(portionPrefabs[randomIndex], new Vector2(x - spacingX, height - spacingY), Quaternion.identity);
        newPortion.transform.SetParent(portionsParent.transform);
        //Get indices
        newPortion.GetComponent<Portion>().SetIndices(x, index);
        //set it on the portion borad
        portionBoard[x, index] = new Node(true, newPortion);
        //move it to that location
        Vector3 targetPosition = new Vector3(newPortion.transform.position.x, newPortion.transform.position.y - locationToMoveTo, newPortion.transform.position.z);
        newPortion.GetComponent<Portion>().MoveToTarget(targetPosition);
    }

    //FindIndexOfLowestNull() 
    private int FindIndexOfLowestNull(int x)
    {
        int lowestNull = 99;
        for (int y = 7; y >= 0; y--)
        {
            if (portionBoard[x, y].portion == null)
            {
                lowestNull = y;
            }
        }
        
        return lowestNull;
    }

    #endregion

    #region Matching logic

    private MatchResult SuperMatched(MatchResult _matchedResults)
    {
        // if there is a horizontal or long horizontal match
        if (_matchedResults.direction == MatchDirection.Horizontal || _matchedResults.direction == MatchDirection.LongHorizontal)
        {
            //loop through portions in match
            foreach (Portion port in _matchedResults.connectedPortions)
            {
                //create new list of extra matches
                List<Portion> extraConnectedPortions = new();

                //check direction up
                CheckDirection(port, new Vector2Int(0, 1), extraConnectedPortions);
                //check direction down
                CheckDirection(port, new Vector2Int(0, -1), extraConnectedPortions);

                //are there 2 or more extra matches
                if (extraConnectedPortions.Count >= 2)
                {
                    //we have a super match; return a new match result of type super
                    Debug.Log("Super horizontal match");
                    extraConnectedPortions.AddRange(_matchedResults.connectedPortions);

                    return new MatchResult
                    {
                        connectedPortions = extraConnectedPortions,
                        direction = MatchDirection.Super
                    };
                }
            }
            //return extra matches
            return new MatchResult
            {
                connectedPortions = _matchedResults.connectedPortions,
                direction = _matchedResults.direction
            };
        }

        // if there is a vertical or long vertical match
        else if (_matchedResults.direction == MatchDirection.Vertical || _matchedResults.direction == MatchDirection.LongVertical)
        {
            //loop through portions in match
            foreach (Portion port in _matchedResults.connectedPortions)
            {
                //create new list of extra matches
                List<Portion> extraConnectedPortions = new();

                //check direction up
                CheckDirection(port, new Vector2Int(1, 0), extraConnectedPortions);
                //check direction down
                CheckDirection(port, new Vector2Int(-1, 0), extraConnectedPortions);

                //are there 2 or more extra matches
                if (extraConnectedPortions.Count >= 2)
                {
                    //we have a super match; return a new match result of type super
                    Debug.Log("Super vertical match");
                    extraConnectedPortions.AddRange(_matchedResults.connectedPortions);

                    return new MatchResult
                    {
                        connectedPortions = extraConnectedPortions,
                        direction = MatchDirection.Super
                    };
                }
            }
            //return extra matches
            return new MatchResult
            {
                connectedPortions = _matchedResults.connectedPortions,
                direction = _matchedResults.direction
            };
        }
        return null; 
    }

    MatchResult IsConnected(Portion portion)
    {
        List<Portion> connectedPortions = new();
        PortionType portionType = portion.portionType;

        connectedPortions.Add(portion);

        //check right
        CheckDirection(portion, new Vector2Int(1, 0), connectedPortions);

        //check left
        CheckDirection(portion, new Vector2Int(-1, 0), connectedPortions);

        //have we made a three match(horizontal match)
        if (connectedPortions.Count == 3)
        {
            Debug.Log("Normal horizontal match with color " + connectedPortions[0].portionType);

            return new MatchResult
            {
                connectedPortions = connectedPortions,
                direction = MatchDirection.Horizontal
            };
        }

        //check for more than three match (long horizontal match)
        else if (connectedPortions.Count > 3)
        {
            Debug.Log("Long horizontal match with color " + connectedPortions[0].portionType);

            return new MatchResult
            {
                connectedPortions = connectedPortions,
                direction = MatchDirection.LongHorizontal,
            };
        }
        //clear out connected portions
        connectedPortions.Clear();
        //re-add initial portion
        connectedPortions.Add(portion);

        //check up
        CheckDirection(portion, new Vector2Int(0, 1), connectedPortions);

        //check down
        CheckDirection(portion, new Vector2Int(0, -1), connectedPortions);

        //have we made a three match(vertical match)
        if (connectedPortions.Count == 3)
        {
            Debug.Log("Normal vertical match with color " + connectedPortions[0].portionType);

            return new MatchResult
            {
                connectedPortions = connectedPortions,
                direction = MatchDirection.Vertical,
            };
        }

        //check for more than three match (long vertical match)
        else if (connectedPortions.Count > 3)
        {
            Debug.Log("Long vertical match with color " + connectedPortions[0].portionType);

            return new MatchResult
            {
                connectedPortions = connectedPortions,
                direction = MatchDirection.LongVertical,
            };
        }
        else
        {
            return new MatchResult
            {
                connectedPortions = connectedPortions,
                direction = MatchDirection.None
            };
        }
    }

    // CheckDirection
    void CheckDirection(Portion port, Vector2Int direction, List<Portion> connectedPortions)
    {
        PortionType portionType = port.portionType;

        int x = port.xIndex + direction.x;
        int y = port.yIndex + direction.y;

        //check that we are within the boundaries of the board
        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            if (portionBoard[x, y].isUsable)
            {
                Portion neighbourPortion = portionBoard[x, y].portion.GetComponent<Portion>();

                //does portion type match
                if (!neighbourPortion.isMatched && neighbourPortion.portionType == portionType)
                {
                    connectedPortions.Add(neighbourPortion);

                    x += direction.x;
                    y += direction.y;
                }
                else
                {
                    break;
                }
            }
            else
            {
                break;
            }
        }
    }

    #region Swapping portions

    //select portion
    public void SelectPortion(Portion _portion)
    {
       /* if we do not have a portion currenctly selected
       set the portion just clicked to selsected*/
       if (selectedPortion == null)
        {
            Debug.Log(_portion);
            selectedPortion = _portion;
        }

        // if same potion is selected twice, make selected portion null
        else if (selectedPortion == _portion)
        {
            selectedPortion = null;
        }

        // if seleced portion is not null and is not the current portion, attemp a swap
        // set selected portion back to null
        else if (selectedPortion != _portion)
        {
            SwapPortion(selectedPortion, _portion);
            selectedPortion = null;
        }
    }

    //swap portion
    private void SwapPortion(Portion _currentPortion,  Portion _targetPortion)
    {
        // if not adjacent, do not do anything
        if (!IsAdjacent(_currentPortion, _targetPortion))
        {
            return;
        }

        DoSwap(_currentPortion, _targetPortion);

        isProcessingMove = true;

        StartCoroutine(ProcessMatches(_currentPortion, _targetPortion));
    }

    //do swap
    private void DoSwap(Portion _currentPortion, Portion _targetPortion)
    {
        GameObject temp = portionBoard[_currentPortion.xIndex, _currentPortion.yIndex].portion;

        portionBoard[_currentPortion.xIndex, _currentPortion.yIndex].portion = portionBoard[_targetPortion.xIndex, _targetPortion.yIndex].portion;
        portionBoard[_targetPortion.xIndex, _targetPortion.yIndex].portion = temp;

        //update indices
        int tempXIndex = _currentPortion.xIndex;
        int tempYIndex = _currentPortion.yIndex;
        _currentPortion.xIndex = _targetPortion.xIndex;
        _currentPortion.yIndex = _targetPortion.yIndex;
        _targetPortion.xIndex = tempXIndex;
        _targetPortion.yIndex = tempYIndex;

        _currentPortion.MoveToTarget(portionBoard[_targetPortion.xIndex, _targetPortion.yIndex].portion.transform.position);
        _targetPortion.MoveToTarget(portionBoard[_currentPortion.xIndex, _currentPortion.yIndex].portion.transform.position);
    }

    private IEnumerator ProcessMatches(Portion _currentPortion, Portion _targetPortion)
    {
        yield return new WaitForSeconds(0.2f);

        if (CheckBoard())
        {
            StartCoroutine(ProcessTurnOnMatchedBoard(true));
        }
        else
        {
            DoSwap(_currentPortion, _targetPortion);
        }
        isProcessingMove = false;
    }

    //IsAdjacent
    private bool IsAdjacent(Portion _currentPortion, Portion _targetPortion)
    {
        return Mathf.Abs(_currentPortion.xIndex - _targetPortion.xIndex) + Mathf.Abs(_currentPortion.yIndex - _targetPortion.yIndex) == 1;

    }

    #endregion
    #endregion
}

public class MatchResult
{
    public List<Portion> connectedPortions;
    public MatchDirection direction;
}

public enum MatchDirection
{
    Vertical,
    Horizontal,
    LongVertical,
    LongHorizontal,
    Super,
    None
}
