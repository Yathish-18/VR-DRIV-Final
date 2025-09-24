using UnityEngine;

[System.Serializable]
public class GraphConnection
{
    public int fromNodeID;
    public int toNodeID;
    public float weight;
    public bool bidirectional = true;

    public GraphConnection(int from, int to, float w, bool bidir = true)
    {
        fromNodeID = from;
        toNodeID = to;
        weight = w;
        bidirectional = bidir;
    }
}