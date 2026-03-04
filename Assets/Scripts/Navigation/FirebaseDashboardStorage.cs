using UnityEngine;
using System;
using Proyecto26;

// ============================================================
//  SETUP
// ============================================================
//  1. RestClient already in your project 
//
//  2. In Firebase Console:
//     • Realtime Database → copy your Database URL
//     • Rules → set write access (see below)
//
//  3. In the Inspector:
//     • Paste your Database URL
//     • Paste your Auth Secret (Project Settings → Service Accounts → Database secrets)
//
//  4. Hook up the end trigger:
//     • Select the GameObject that has GenericTriggerTimer
//     • In the Inspector, find "On Timer Complete ()"
//     • Click + → drag this GameObject → choose:
//          FirebaseDashboardStorage → SaveSessionData()
//
//  Firebase Rules (Realtime Database → Rules tab):
//  Development:  { "rules": { ".read": true, ".write": true } }
//  Production:   { "rules": { "drivingSessions": { "$user": { ".write": true } } } }
// ============================================================

/// <summary>
/// Saves the full driving session to Firebase Realtime Database when the
/// end-of-session trigger fires (GenericTriggerTimer → OnTimerComplete).
///
/// Pulls automatically from:
///   • GamePersistenceManager  → playerName, trackName, weather, time-of-day
///   • DashboardDataProvider   → speed, score, penalties, reaction times
/// </summary>
public class FirebaseDashboardStorage : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static FirebaseDashboardStorage Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────────
    [Header("Firebase Config")]
    [Tooltip("Firebase Console → Realtime Database → the URL shown at the top.\n" +
      "Example: https://my-project-default-rtdb.firebaseio.com")]
    public string databaseUrl = "https://YOUR-PROJECT.firebaseio.com";

    [Tooltip("Firebase Console → Project Settings → Service Accounts → Database secrets.\n" +
        "Leave blank only if your DB rules allow public writes (dev only).")]
    public string authSecret = "";

    [Header("Data Settings")]
    [Tooltip("Root node name in your Realtime Database.")]
    public string rootNode = "drivingSessions";

    [Tooltip("Print verbose logs to the Console.")]
    public bool enableDebugLogs = true;

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ── Main Entry Point ──────────────────────────────────────────────────────
    /// <summary>
    /// Assign this to GenericTriggerTimer → OnTimerComplete () in the Inspector.
    /// It will fire automatically when the end-of-session trigger completes.
    /// </summary>
    public void SaveSessionData()
    {
        // ── Validation ────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(databaseUrl) || databaseUrl.Contains("YOUR-PROJECT"))
        {
            Debug.LogError(
              "[FirebaseDashboardStorage] Database URL is not set.\n" +
              "Paste your Firebase Realtime Database URL into the Inspector.");
            return;
        }

        if (!DashboardDataProvider.HasStoredData())
        {
            Debug.LogWarning(
              "[FirebaseDashboardStorage] No session data found.\n" +
              "Make sure DashboardDataProvider is active in the driving scene.");
            return;
        }

        // ── Final flush so the very last frame's values are included ──────────
        DashboardDataProvider.CaptureSessionEndData();

        // ── Build & send ──────────────────────────────────────────────────────
        var payload = BuildPayload();
        PostToFirebase(payload);
    }

    // ── Payload Assembly ──────────────────────────────────────────────────────
    [Serializable]
    class SessionPayload
    {
        // ── Identity ──────────────────────────────────────────────────────────
        public string playerName;
        public string timestamp;
        public long unixTimestamp;

        // ── Track / Session Context ───────────────────────────────────────────
        public string trackName;
        public string weatherCondition;
        public string timeOfDay;
        public string sessionStartTime;

        // ── Driving Basics ────────────────────────────────────────────────────
        public float maxSpeed_kmh;
        public float averageSpeed_kmh;
        public float totalDistance_km;
        public float totalTime_sec;

        // ── Score ─────────────────────────────────────────────────────────────
        public float finalScore;
        public float finalPercentage;
        public string performanceGrade;
        public float baseScore;

        // ── Positive Metrics ──────────────────────────────────────────────────
        public float smoothDrivingPercentage;
        public float smoothDrivingPoints;
        public float carHealthPercentage;
        public float carHealthPoints;

        // ── Penalties ─────────────────────────────────────────────────────────
        public float penalty_trafficLight;
        public float penalty_lane;
        public float penalty_speeding;
        public float penalty_turnIndicator;
        public float penalty_total;

        // ── Reaction Time ─────────────────────────────────────────────────────
        public float reactionTime_avg_sec;
        public float reactionTime_worst_sec;

        // ── Legacy ────────────────────────────────────────────────────────────
        public float laneConsistency;
        public float vehicleCare;
    }

    SessionPayload BuildPayload()
    {
        var d = DashboardDataProvider.GetStoredData();
        var gpm = GamePersistenceManager.Instance;

        // ── Pull from GamePersistenceManager ──────────────────────────────────
        string playerName = gpm != null ? gpm.playerName : "UnknownDriver";
        string trackName = gpm != null && gpm.HasTrackData() ? gpm.GetSelectedTrack().trackName : "Unknown Track";
        string weatherName = gpm != null && gpm.HasWeatherData() ? gpm.GetSelectedWeather().weatherName : "Unknown";
        string timeName = gpm != null && gpm.HasTimeData() ? gpm.GetSelectedTime().timeName : "Unknown";
        string sessionStart = gpm != null ? gpm.sessionStartTime.ToString("yyyy-MM-dd HH:mm:ss") : "Unknown";

        Log($"Saving session — Player: \"{playerName}\"  Track: \"{trackName}\"  " +
          $"Weather: \"{weatherName}\"  Time: \"{timeName}\"");

        return new SessionPayload
        {
            // Identity
            playerName = playerName,
            timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss"),
            unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),

            // Context
            trackName = trackName,
            weatherCondition = weatherName,
            timeOfDay = timeName,
            sessionStartTime = sessionStart,

            // Driving basics
            maxSpeed_kmh = Round(d.maxSpeed),
            averageSpeed_kmh = Round(d.averageSpeed),
            totalDistance_km = Round(d.totalDistance, 3),
            totalTime_sec = Round(d.totalTime, 1),

            // Score
            finalScore = Round(d.finalScore),
            finalPercentage = Round(d.finalPercentage),
            performanceGrade = d.performanceGrade,
            baseScore = Round(d.baseScore),

            // Positive metrics
            smoothDrivingPercentage = Round(d.smoothDrivingPercentage),
            smoothDrivingPoints = Round(d.smoothDrivingPoints),
            carHealthPercentage = Round(d.carHealthPercentage),
            carHealthPoints = Round(d.carHealthPoints),

            // Penalties
            penalty_trafficLight = Round(d.trafficLightPenalty),
            penalty_lane = Round(d.lanePenalty),
            penalty_speeding = Round(d.speedingPenalty),
            penalty_turnIndicator = Round(d.turnIndicatorPenalty),
            penalty_total = Round(d.totalPenalty),

            // Reaction time
            reactionTime_avg_sec = Round(d.avgReactionTimeSec, 3),
            reactionTime_worst_sec = Round(d.worstReactionTimeSec, 3),

            // Legacy
            laneConsistency = Round(d.laneConsistency),
            vehicleCare = Round(d.vehicleCare),
        };
    }

    // ── Firebase REST POST ────────────────────────────────────────────────────
    void PostToFirebase(SessionPayload payload)
    {
        // Sanitize player name for use as a Firebase path segment
        string safePlayer = SanitizeKey(payload.playerName);

        // Path: drivingSessions/{playerName}.json
        // POST creates a new child with a unique push-ID each call,
        // so all sessions per player are grouped under their name node.
        string url = $"{databaseUrl.TrimEnd('/')}/{rootNode}/{safePlayer}.json";
        if (!string.IsNullOrWhiteSpace(authSecret))
            url += $"?auth={authSecret}";

        string json = JsonUtility.ToJson(payload);

        Log($"POSTing to Firebase → {url}");

        RestClient.Post(new RequestHelper
        {
            Uri = url,
            BodyString = json,
            ContentType = "application/json",
            EnableDebug = enableDebugLogs,
            Retries = 2,
            RetrySecondsDelay = 3,
        })
        .Then(response =>
        {
            Log($" Session saved!  HTTP {response.StatusCode}  Key: {response.Text}");
            PrintSummary(payload);
        })
        .Catch(err =>
        {
            var rex = err as RequestException;
            Debug.LogError(
          $"[FirebaseDashboardStorage] Save failed.\n" +
          $"  Error    : {err.Message}\n" +
          $"  Response : {rex?.Response}\n" +
          "  → Check your Database URL, auth secret, and Firebase rules.");
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    // Firebase path segments cannot contain  . # $ [ ] /
    static string SanitizeKey(string s) =>
    s.Replace(".", "_").Replace("#", "_").Replace("$", "_")
    .Replace("[", "_").Replace("]", "_").Replace("/", "_");

    static float Round(float v, int d = 2) => (float)Math.Round(v, d);

    void Log(string msg)
    {
        if (enableDebugLogs) Debug.Log($"[FirebaseDashboardStorage] {msg}");
    }

    void PrintSummary(SessionPayload p)
    {
        if (!enableDebugLogs) return;
        Debug.Log(
          "[FirebaseDashboardStorage] ── Session Summary ─────────────────────────\n" +
          $"  Player      : {p.playerName}\n" +
          $"  Track       : {p.trackName}   Weather: {p.weatherCondition}   Time: {p.timeOfDay}\n" +
          $"  Session     : started {p.sessionStartTime}  saved {p.timestamp} UTC\n" +
          $"  Score       : {p.finalScore} / 100   Grade: {p.performanceGrade}\n" +
          $"  Distance    : {p.totalDistance_km} km   Avg Speed: {p.averageSpeed_kmh} km/h   Max: {p.maxSpeed_kmh} km/h\n" +
          $"  Smooth Drive: {p.smoothDrivingPercentage}%   Car Health: {p.carHealthPercentage}%\n" +
          $"  Penalties   → Lane: {p.penalty_lane}  Traffic: {p.penalty_trafficLight}  " +
          $"Speed: {p.penalty_speeding}  Indicator: {p.penalty_turnIndicator}  Total: {p.penalty_total}\n" +
          $"  Reaction    : avg={p.reactionTime_avg_sec}s   worst={p.reactionTime_worst_sec}s\n" +
          "──────────────────────────────────────────────────────────────────────");
    }

    // ── Editor Helpers ────────────────────────────────────────────────────────
    [ContextMenu("Debug: Save Session Now (Play Mode only)")]
    void EditorSave()
    {
        if (Application.isPlaying) SaveSessionData();
        else Debug.LogWarning("[FirebaseDashboardStorage] Enter Play Mode first.");
    }
}