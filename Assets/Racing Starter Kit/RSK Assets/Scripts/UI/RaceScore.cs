
using UnityEngine;
using TMPro;

public class RaceScore : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreText;

    private int score = 0;

    void Start()
    {
        UpdateScoreUI();
    }

    public void CheckpointReached()
    {
        score++;
        UpdateScoreUI();

        Debug.Log("Checkpoint reached! Score: " + score);
    }

    private void UpdateScoreUI()
    {
        scoreText.text = "Score: " + score;
    }

    public void ResetScore()
    {
        score = 0;
        UpdateScoreUI();
    }

    public int GetScore()
    {
        return score;
    }
}
