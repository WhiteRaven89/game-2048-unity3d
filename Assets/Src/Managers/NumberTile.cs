using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NumberTile : MonoBehaviour
{
    [SerializeField]
    int xCoord = 0;

    public int XCoord
    {
        set
        {
            xCoord = value;
        }
        get
        {
            return xCoord;
        }
    }

    [SerializeField]
    int yCoord = 0;

    public int YCoord
    {
        set
        {
            yCoord = value;
        }
        get
        {
            return yCoord;
        }
    }

    [SerializeField]
    int tileValue = 0;

    public int TileValue
    {
        set
        {
            tileValue = value;
        }
        get
        {
            return tileValue;
        }
    }

    [SerializeField]
    bool isMerged = false;

    public bool IsMerged
    {
        set
        {
            isMerged = value;
        }
        get
        {
            return isMerged;
        }
    }

    void OnEnable()
    {

    }

    public override string ToString()
    {
        return "Number Tile : X : " + XCoord + " Y : " + YCoord;
    }
}
