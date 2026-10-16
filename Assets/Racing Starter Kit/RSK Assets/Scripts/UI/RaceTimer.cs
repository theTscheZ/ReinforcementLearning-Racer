
using UnityEngine;
using TMPro;

public class RaceTimer : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;

    private float elapsedTime;
    private bool isRunning = true;

    void Update()
    {
        if (!isRunning)
            return;

        elapsedTime += Time.deltaTime;

        timerText.text = $"Time: {elapsedTime:F2} s";
    }

    public void StopTimer()
    {
        isRunning = false;
    }

    public void ResetTimer()
    {
        elapsedTime = 0f;
        isRunning = true;
        timerText.text = "Time: 0.00 s";
    }

    public float GetElapsedTime()
    {
        return elapsedTime;
    }
}
