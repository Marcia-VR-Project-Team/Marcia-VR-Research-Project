using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RunManager : MonoBehaviour
{
    public Vector3 playerStartPosition = new Vector3(25, 0, 0);
    public int runCounter = 0;
    public int totalRuns = 7;
    public GameObject anomalyManager;
    public GameObject exitSignManager;
    public GameObject roundTextRight;
    public GameObject roundTextLeft;
    private bool roundHasBegun = false; // i.e. player has entered the room
    private bool roundHasEnded = false; // i.e. roundHasBegun and the player has left the room
    private bool playerEnteredThroughRightDoor = false;
    private bool allowCollisions = true;
    public FadeController fadeController;
    public Dialogue dialogue;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Debug.Log("Runner started");
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void OnTriggerEnter(Collider other)
    {
        // Player exits classroom
        if (roundHasBegun && !roundHasEnded && (other.name == "door_frame_right" || other.name == "door_frame_left") && allowCollisions)
        {
            Debug.Log("Round " + runCounter + " Exited door");
            SessionLogger.Event("DOOR_EXIT", other.name, $"round={runCounter}");
            bool rightDoorExitedThrough = other.name == "door_frame_right";
            bool playerExitedThroughEntryDoor = (rightDoorExitedThrough && playerEnteredThroughRightDoor) || (!rightDoorExitedThrough && !playerEnteredThroughRightDoor);
            bool anomalyIsActive = anomalyManager.GetComponent<AnomalyManager>().anomalyIsActive;
            bool playerMadeRightChoice = (playerExitedThroughEntryDoor && anomalyIsActive) || (!playerExitedThroughEntryDoor && !anomalyIsActive);

            StartCoroutine(FinishRound(playerMadeRightChoice));
        }
        // Player enters classroom
        else if (!roundHasBegun && !roundHasEnded && (other.name == "door_frame_right" || other.name == "door_frame_left") && allowCollisions)
        {
            Debug.Log("Round " + runCounter + " Entered door");
            SessionLogger.Event("DOOR_ENTER", other.name, $"round={runCounter}");

            roundHasBegun = true;
            playerEnteredThroughRightDoor = other.name == "door_frame_right";      
            exitSignManager.GetComponent<ExitSignManager>().UpdateExitSignColor(playerEnteredThroughRightDoor);
        }

        StartCoroutine(ColissionCooldown());

    }
    IEnumerator ColissionCooldown()
    {
        allowCollisions = false;
        yield return new WaitForSeconds(1);
        allowCollisions = true;
    }

    IEnumerator FinishRound(bool playerMadeRightChoice)
    {
        // Logged before the fade so the timestamp marks the participant's decision, not the end
        // of the transition animation.
        SessionLogger.Event("ROUND_END", nameof(RunManager),
            $"round={runCounter}; correct={playerMadeRightChoice}");

        yield return fadeController.FadeOut();
        // Player chose correct door or there is no anomaly
        if (playerMadeRightChoice)
        {
            runCounter ++;
            anomalyManager.GetComponent<AnomalyManager>().ResetRound(runCounter);
        }
        else // Player chose wrong door
        {
            // yield return StartCoroutine(fadeController.PlayDeathScene());
            runCounter = 0;
            anomalyManager.GetComponent<AnomalyManager>().ResetGame();
        }

        // Dialogue to indicate exit sign switch on round 1 (first time only)
        int exitSignHint = PlayerPrefs.GetInt("ExitSignHintKey", 0);
        if(dialogue != null && runCounter == 1 && exitSignHint == 0) 
        {
            dialogue.AddLine("Ah, the exit door colors switch when I walk through the green exit door.... I need to be careful.");
            PlayerPrefs.SetInt("ExitSignHintKey", 1);
            PlayerPrefs.Save();
        }

        // playerHasWon
        if (runCounter == totalRuns)
        {
            SessionLogger.Event("GAME_WON", nameof(RunManager), $"rounds={totalRuns}");

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            anomalyManager.GetComponent<AnomalyManager>().ResetRound(runCounter);
            SceneManager.LoadScene("Victory");
            yield break;
        }

        roundHasBegun = false;
        roundHasEnded = false; 
        
        // Teleport player to spawn
        // fade to black;
        String roundText = $"Round:\n{runCounter} / {totalRuns}";
        roundTextRight.GetComponent<TextMeshPro>().text = roundText;
        roundTextLeft.GetComponent<TextMeshPro>().text = roundText;

        yield return new WaitForSeconds(0.1f);

        // Fade in AFTER room loads
        yield return fadeController.FadeIn();

    }
}