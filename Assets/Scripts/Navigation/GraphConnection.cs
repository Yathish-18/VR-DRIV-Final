using System;

[Serializable]
public class GraphConnection
{
    public int fromNodeID;
    public int toNodeID;
    public float weight = 1f;
    public bool bidirectional = true;

    public GraphConnection() { }

    public GraphConnection(int from, int to, bool bidir = true, float w = 1f)
    {
        fromNodeID = from;
        toNodeID = to;
        bidirectional = bidir;
        weight = w;
    }
}
