
//using UnityEngine;

//[System.Serializable]
//public class ViolationData
//{
//    public ViolationType type;
//    public float timestamp;
//    public float penaltyPoints;
//    public ViolationLevel severity;
//    public string description;
//    public Vector3 location;
//    public string trafficLightID; // Track which traffic light caused violation

//    public ViolationData(ViolationType type, float penaltyPoints, ViolationLevel severity, string description, Vector3 location, string trafficLightID = "")
//    {
//        this.type = type;
//        this.timestamp = Time.time;
//        this.penaltyPoints = penaltyPoints;
//        this.severity = severity;
//        this.description = description;
//        this.location = location;
//        this.trafficLightID = trafficLightID;
//    }
//}

//public enum ViolationType
//{
//    TrafficLight,
//    Speeding,
//    OffTrack,
//    WrongLane,
//    IllegalTurn
//}

//public enum ViolationLevel
//{
//    Minor,
//    Moderate,
//    Severe,
//    Critical
//}

//public enum DrivingGrade
//{
//    F,      // 0-54%
//    D,      // 55-59%
//    D_Plus, // 60-64%
//    C,      // 65-69%
//    C_Plus, // 70-74%
//    B,      // 75-79%
//    B_Plus, // 80-84%
//    A,      // 85-89%
//    A_Plus  // 90-100%
//}
