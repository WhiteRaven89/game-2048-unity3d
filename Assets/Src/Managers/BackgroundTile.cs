using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackgroundTile : MonoBehaviour
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

    public override string ToString()
    {
        return "Background Tile : X : " + XCoord + " Y : " + YCoord;
    }
}
