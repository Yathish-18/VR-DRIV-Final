using UnityEngine;

[CreateAssetMenu(fileName = "New Track", menuName = "Racing Game/Track Data", order = 1)]
public class TrackDataSO : ScriptableObject
{
    [Header("Track Information")]
    public string trackName;
    public string trackNumber;
    public string countryName;
    public Sprite countryFlag;
    public float trackLength;
    public int totalTurns;
    public string sceneName;

    [Header("Track Preview")]
    public Sprite trackLayoutImage;
    public Sprite trackPreviewImage;

    [Header("Track Description")]
    [TextArea(3, 5)]
    public string trackDescription;
}
