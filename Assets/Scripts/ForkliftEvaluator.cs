using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForkliftEvaluator : MonoBehaviour
{
    [Header("Box and Zones")]
    public GameObject box;
    public Transform endZone;

    [Header("UI")]
    public TextMeshProUGUI timeText;
    public GameObject resultPanel;
    public TextMeshProUGUI resultTimeText;
    public TextMeshProUGUI scoreText;

    [Header("Timing")]
    public float maxAllowedTime = 60f;

    private float timer = 0f;
    private bool timerRunning = false;
    private bool taskCompleted = false;
    private bool boxLifted = false;

    private void Update()
    {
        if (timerRunning && !taskCompleted)
        {
            timer += Time.deltaTime;
            timeText.text = ":" + timer.ToString("F2");

            if (timer >= maxAllowedTime)
            {
                CompleteTask();
            }
        }

        // Check if box has been placed in end zone after it's lifted
        if (boxLifted && !taskCompleted)
        {
            float distanceToEnd = Vector3.Distance(box.transform.position, endZone.position);
            if (distanceToEnd < 1.5f) // Adjust as per your setup
            {
                CompleteTask();
            }
        }
    }

    public void OnBoxLifted()
    {
        if (!boxLifted)
        {
            boxLifted = true;
            timerRunning = true;
        }
    }

    public void CompleteTask()
    {
        taskCompleted = true;
        timerRunning = false;

        resultPanel.SetActive(true);
        resultTimeText.text = "Time Taken: " + timer.ToString("F2") + "s";

        int score = CalculateScore(timer);
        scoreText.text = "Score: " + score.ToString();
    }

    private int CalculateScore(float timeTaken)
    {
        if (timeTaken <= maxAllowedTime * 0.5f)
            return 100;
        else if (timeTaken <= maxAllowedTime * 0.75f)
            return 75;
        else if (timeTaken <= maxAllowedTime)
            return 50;
        else
            return 0;
    }
}
