using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// This file should be what manages/invokes the anomalies
public class AnomalyManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public List<GameObject> anomalies; // This is a public list so add to it via Unity's GUI
    public bool anomalyIsActive = false; // This needs to be read by the RunManager to determine if the player won that round

    List<GameObject> usedAnomalies = new List<GameObject>(); // Private list to keep track of anomalies used so far

    void Start()
    {
        // Random seed for testing in Unity editor
        Random.InitState(System.DateTime.Now.Millisecond);

        // Make sure everything is disabled at the start
        DeactivateAllAnomalies();
        StartRound();
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void StartRound(int roundNumber=0)
    {
        anomalyIsActive = roundNumber == 0 ? false : !(Random.Range(0, 4) == 1); // 1 in 4 chance of no anomaly
        Debug.Log("Anomaly is active: " + anomalyIsActive);

        // Recorded so the analysis knows the ground truth of each round without replaying it.
        SessionLogger.Event("ROUND_START", nameof(AnomalyManager),
            $"round={roundNumber}; anomaly_active={anomalyIsActive}");

        if (anomalyIsActive)
        {
            ActivateAnAnomaly();
        }
    }

    void ActivateAnAnomaly()
    {
        if (anomalies.Count == 0)
        {
            ResetAnomalyTracker();
        }

        // For testing purposes
        // GameObject chosenAnomalyObject = anomalies[8];

        // Choose a random anomaly for the round
        GameObject chosenAnomalyObject = anomalies[Random.Range(0, anomalies.Count)]; 
        Anomaly chosenAnomaly = chosenAnomalyObject.GetComponent<Anomaly>();
        Debug.Log("Randomly chosen Anomaly: " + chosenAnomaly);

        chosenAnomaly.Activate(); // Activate the anomaly

        // Which anomaly was shown is the key independent variable, so it is logged at the moment
        // it becomes visible rather than reconstructed afterwards.
        SessionLogger.Event("ANOMALY_ACTIVATED", chosenAnomalyObject.name,
            $"type={chosenAnomaly.GetType().Name}");
        usedAnomalies.Add(chosenAnomalyObject);
        anomalies.Remove(chosenAnomalyObject);
    }

    void DeactivateAllAnomalies()
    {
        anomalies.ForEach(anomalyObject => anomalyObject.GetComponent<Anomaly>().Deactivate());
        usedAnomalies.ForEach(anomalyObject => anomalyObject.GetComponent<Anomaly>().Deactivate());
    }


    public void ResetAnomalyTracker()
    {
        usedAnomalies.ForEach(anomaly => anomalies.Add(anomaly));
        usedAnomalies.Clear();
    }

    public void ResetRound(int roundNumber=0)
    {
        DeactivateAllAnomalies();
        StartRound(roundNumber);
    }

    public void ResetGame()
    {
        ResetAnomalyTracker();
        ResetRound();
    }

}
