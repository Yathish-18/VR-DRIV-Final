using UnityEngine;

/// <summary>
/// Inspector-friendly connection definition
/// Allows you to visually define connections between nodes with bidirectional/unidirectional options
/// </summary>
[System.Serializable]
public class ConnectionDefinition
{
    [Tooltip("Starting node ID")]
    public int fromNodeID;

    [Tooltip("Ending node ID")]
    public int toNodeID;

    [Tooltip("If true, creates a two-way connection. If false, only from -> to")]
    public bool bidirectional = false;

    public ConnectionDefinition(int from, int to, bool bidir = false)
    {
        fromNodeID = from;
        toNodeID = to;
        bidirectional = bidir;
    }
}