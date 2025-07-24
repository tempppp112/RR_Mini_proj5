using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;

public enum GameMode { Spatial, Visual, Auditory, Combined }
public enum ProgressionMode { Adaptive, Fixed }

public class GameManager : MonoBehaviour
{
    [Header("UI Elements")]
    public GameObject startButton;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI infoText;

    [Header("Game Progression")]
    public ProgressionMode progressionMode = ProgressionMode.Adaptive;
    public float delayBetweenPhases = 2.0f;
    private GameMode[] gameProgression = { GameMode.Visual, GameMode.Auditory, GameMode.Spatial, GameMode.Combined };
    private int currentPhaseIndex = 0;
    private GameMode currentMode;

    [Header("Adaptive Difficulty")]
    public int trialsPerBlock = 20;
    public float accuracyThreshold = 0.85f;
    public int maxConsecutiveFailures = 3;
    private int blockCorrectResponses = 0;
    private int blockExpectedMatches = 0;
    private int blockFalseAlarms = 0;
    private int consecutiveFailureCount = 0;

    [Header("Scoring")]
    public int pointsForCorrect = 10;
    public int pointsForMiss = -5;
    public int pointsForFalseAlarm = -5;

    [Header("General Settings")]
    public int nValue = 2;
    public float stimulusDuration = 1.5f;
    public float delayBetweenStimuli = 1.0f;

    [Header("Assets")]
    public Color[] colorPalette;
    public AudioClip[] audioClips;

    // --- Private State Variables ---
    private AudioSource audioSource;
    private List<GridCell> allCells = new List<GridCell>();
    private List<int> spatialHistory = new List<int>();
    private List<Color> visualHistory = new List<Color>();
    private List<AudioClip> soundHistory = new List<AudioClip>();
    private List<TrialData> allTrialsData = new List<TrialData>();
    private int trialCounter = 0;
    private float trialStartTime;
    private int playerScore = 0;
    private bool isLocationMatchExpected, isColorMatchExpected, isAuditoryMatchExpected, awaitingInput;
    private Coroutine gameLoopCoroutine;

    public void InitializeGame(List<GridCell> cellsFromGenerator)
    {
        allCells = cellsFromGenerator;
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        UpdateScore(0);
        if (infoText != null) infoText.text = "Press 'Start' to begin the assessment.";
        if (startButton != null) startButton.SetActive(true);
    }

    public void StartGame()
    {
        if (startButton != null) startButton.SetActive(false);
        // Reset all state for a fresh start
        currentPhaseIndex = 0;
        currentMode = gameProgression[currentPhaseIndex];
        nValue = 2;
        playerScore = 0;
        trialCounter = 0;
        consecutiveFailureCount = 0;
        allTrialsData.Clear();
        spatialHistory.Clear(); visualHistory.Clear(); soundHistory.Clear();
        
        UpdateInfoText();
        UpdateScore(0);
        
        if(gameLoopCoroutine != null) StopCoroutine(gameLoopCoroutine);
        gameLoopCoroutine = StartCoroutine(GameLoop());
    }

    private void EndGame(string message)
    {
        if (gameLoopCoroutine != null)
        {
            StopCoroutine(gameLoopCoroutine);
        }
        awaitingInput = false;
        infoText.text = message;
        if (startButton != null)
        {
            startButton.SetActive(true);
        }
        Debug.LogWarning(message);
    }

    private void UpdateScore(int pointsToAdd)
    {
        playerScore += pointsToAdd;
        if (scoreText != null) scoreText.text = "Score: " + playerScore;
    }
    
    private void UpdateInfoText()
    {
        if (infoText == null) return;
        string instruction = "";
        switch (currentMode)
        {
            case GameMode.Visual:   instruction = $"Phase: Color Matching (N={nValue})"; break;
            case GameMode.Auditory: instruction = $"Phase: Sound Matching (N={nValue})"; break;
            case GameMode.Spatial:  instruction = $"Phase: Location Matching (N={nValue})"; break;
            case GameMode.Combined:
                if(nValue > 2) instruction = $"Challenge: Combined Mode (N={nValue})";
                else instruction = $"Phase: Combined Mode (N={nValue})";
                break;
        }
        infoText.text = instruction;
    }

    void Update()
    {
        if (awaitingInput)
        {
            if (Input.GetKeyDown(KeyCode.L)) HandleResponse("L", isLocationMatchExpected, ref isLocationMatchExpected);
            if (Input.GetKeyDown(KeyCode.C)) HandleResponse("C", isColorMatchExpected, ref isColorMatchExpected);
            if (Input.GetKeyDown(KeyCode.A)) HandleResponse("A", isAuditoryMatchExpected, ref isAuditoryMatchExpected);
        }
    }

   // --- MODIFIED HandleResponse to allow multiple correct inputs per trial ---
void HandleResponse(string key, bool wasExpected, ref bool expectationFlag)
{
    if (!awaitingInput) return;
    if (allTrialsData.Count == 0) return;
    TrialData currentTrial = allTrialsData[allTrialsData.Count - 1];
    
    // Initialize response string if it's the first press for this trial
    if (currentTrial.userResponseKey == "None")
    {
        currentTrial.userResponseKey = "";
    }

    // Don't register the exact same key press twice
    if (currentTrial.userResponseKey.Contains(key)) return; 
    
    currentTrial.userResponseKey += key + ";"; // Append the new key (e.g., "C;A;")
    
    // Only record the reaction time for the very first key press
    if(currentTrial.reactionTime == 0)
    {
        currentTrial.reactionTime = (Time.time - trialStartTime) * 1000f;
    }

    if (wasExpected)
    {
        currentTrial.outcome = "Correct"; // This might be overwritten by a subsequent false alarm, which is okay
        currentTrial.pointsAwarded += pointsForCorrect; // Use += to add points
        UpdateScore(pointsForCorrect);
        blockCorrectResponses++;
        Debug.Log($"--- Correct {key} Match! ---");
        expectationFlag = false; // Mark this specific expectation as met so it can't be triggered again
    }
    else
    {
        currentTrial.outcome = "FalseAlarm";
        currentTrial.pointsAwarded += pointsForFalseAlarm; // Use += to add penalty
        UpdateScore(pointsForFalseAlarm);
        blockFalseAlarms++;
        Debug.LogWarning($"--- False Alarm for {key}! ---");
    }
}
    private IEnumerator GameLoop()
    {
        while (true)
        {
            trialCounter++;
            isLocationMatchExpected = false;
            isColorMatchExpected = false;
            isAuditoryMatchExpected = false;
            TrialData newTrial = new TrialData { trialNumber = trialCounter, nLevel = nValue, mode = currentMode, timestamp = Time.time, pointsAwarded = 0, userResponseKey = "None" };            allTrialsData.Add(newTrial);
            trialStartTime = Time.time;
            
            void CheckAndSetMatch(bool isMatch, ref bool flag) { if (isMatch) { flag = true; blockExpectedMatches++; } }

            // --- STIMULUS PRESENTATION ---
            switch (currentMode)
            {
                 case GameMode.Visual:
                    Color randomColor = colorPalette[Random.Range(0, colorPalette.Length)];
                    CheckAndSetMatch(visualHistory.Count >= nValue && visualHistory[visualHistory.Count - nValue] == randomColor, ref isColorMatchExpected);
                    newTrial.colorMatchExpected = isColorMatchExpected;
                    int cellForColor = Random.Range(0, allCells.Count);
                    allCells[cellForColor].Highlight(randomColor);
                    yield return StartCoroutine(WaitForResponse());
                    allCells[cellForColor].ResetColor();
                    visualHistory.Add(randomColor);
                    spatialHistory.Add(-1); soundHistory.Add(null);
                    break;
                case GameMode.Auditory:
                    AudioClip randomClip = audioClips[Random.Range(0, audioClips.Length)];
                    CheckAndSetMatch(soundHistory.Count >= nValue && soundHistory[soundHistory.Count - nValue] == randomClip, ref isAuditoryMatchExpected);
                    newTrial.auditoryMatchExpected = isAuditoryMatchExpected;
                    audioSource.PlayOneShot(randomClip);
                    yield return StartCoroutine(WaitForResponse());
                    soundHistory.Add(randomClip);
                    spatialHistory.Add(-1); visualHistory.Add(Color.clear);
                    break;
                case GameMode.Spatial:
                    int randomCellIndex = Random.Range(0, allCells.Count);
                    CheckAndSetMatch(spatialHistory.Count >= nValue && spatialHistory[spatialHistory.Count - nValue] == randomCellIndex, ref isLocationMatchExpected);
                    newTrial.locationMatchExpected = isLocationMatchExpected;
                    allCells[randomCellIndex].Highlight(Color.yellow);
                    yield return StartCoroutine(WaitForResponse());
                    allCells[randomCellIndex].ResetColor();
                    spatialHistory.Add(randomCellIndex);
                    visualHistory.Add(Color.clear); soundHistory.Add(null);
                    break;
                case GameMode.Combined:
                    bool presentVisual = Random.value > 0.3f;
                    bool presentAuditory = Random.value > 0.3f;
                    if (!presentVisual && !presentAuditory) { if (Random.value > 0.5f) presentVisual = true; else presentAuditory = true; }
                    int lastUsedCellIndex = -1;
                    if (presentVisual)
                    {
                        randomCellIndex = Random.Range(0, allCells.Count);
                        randomColor = colorPalette[Random.Range(0, colorPalette.Length)];
                        lastUsedCellIndex = randomCellIndex;
                        CheckAndSetMatch(spatialHistory.Count >= nValue && spatialHistory[spatialHistory.Count - nValue] == randomCellIndex, ref isLocationMatchExpected);
                        CheckAndSetMatch(visualHistory.Count >= nValue && visualHistory[visualHistory.Count - nValue] == randomColor, ref isColorMatchExpected);
                        newTrial.locationMatchExpected = isLocationMatchExpected;
                        newTrial.colorMatchExpected = isColorMatchExpected;
                        allCells[randomCellIndex].Highlight(randomColor);
                        spatialHistory.Add(randomCellIndex); visualHistory.Add(randomColor);
                    } else { spatialHistory.Add(-1); visualHistory.Add(Color.clear); }
                    if (presentAuditory)
                    {
                        randomClip = audioClips[Random.Range(0, audioClips.Length)];
                        CheckAndSetMatch(soundHistory.Count >= nValue && soundHistory[soundHistory.Count - nValue] == randomClip, ref isAuditoryMatchExpected);
                        newTrial.auditoryMatchExpected = isAuditoryMatchExpected;
                        audioSource.PlayOneShot(randomClip); soundHistory.Add(randomClip);
                    } else { soundHistory.Add(null); }
                    yield return StartCoroutine(WaitForResponse());
                    if (presentVisual && lastUsedCellIndex != -1) allCells[lastUsedCellIndex].ResetColor();
                    break;
            }

            yield return new WaitForSeconds(delayBetweenStimuli);

            // --- PHASE & DIFFICULTY TRANSITION LOGIC (REVISED) ---
            if (trialCounter > 0 && trialCounter % trialsPerBlock == 0)
            {
                float calculatedAccuracy = (blockExpectedMatches > 0) ? ((float)blockCorrectResponses - blockFalseAlarms) / blockExpectedMatches : 1.0f;
                float blockAccuracy = Mathf.Max(0, calculatedAccuracy);
                Debug.Log($"Block ended for {currentMode}. Correct: {blockCorrectResponses}, False Alarms: {blockFalseAlarms}, Expected: {blockExpectedMatches}, Accuracy: {blockAccuracy:P0}");

                bool shouldAdvance = (progressionMode == ProgressionMode.Fixed) || (blockAccuracy >= accuracyThreshold);

                if (shouldAdvance)
                {
                    consecutiveFailureCount = 0;
                    if (delayBetweenPhases > 0)
                    {
                        infoText.text = "Get Ready for the Next Phase...";
                        yield return new WaitForSeconds(delayBetweenPhases);
                    }
                    
                    if (nValue == 2 && currentPhaseIndex < gameProgression.Length - 1)
                    {
                        // Case 1: Still in the N=2 training phases, move to the next one.
                        currentPhaseIndex++;
                        currentMode = gameProgression[currentPhaseIndex];
                        Debug.LogWarning($"--- New Phase Starting: {currentMode} at N={nValue} ---");
                    }
                    else if (nValue < 3)
                    {
                        // Case 2: Finished N=2 training, now level up to N=3.
                        nValue = 3;
                        currentMode = GameMode.Combined;
                        Debug.LogWarning($"--- LEVEL UP! New N-Value is: {nValue}. Mode is now Combined. ---");
                        spatialHistory.Clear(); visualHistory.Clear(); soundHistory.Clear();
                    }
                    else
                    {
                        // Case 3: We have just finished a block at N=3 (or higher).
                        if (progressionMode == ProgressionMode.Fixed)
                        {
                            // If in Fixed mode, the test is over.
                            EndGame("Fixed Mode assessment complete.");
                            yield break; // Exit the GameLoop
                        }
                        else // progressionMode is Adaptive
                        {
                            // In Adaptive mode, we can keep leveling up.
                            nValue++;
                            currentMode = GameMode.Combined;
                            Debug.LogWarning($"--- LEVEL UP! New N-Value is: {nValue}. Mode is now Combined. ---");
                            spatialHistory.Clear(); visualHistory.Clear(); soundHistory.Clear();
                        }
                    }
                }
                else 
                { 
                    consecutiveFailureCount++;
                    Debug.Log($"Progression Mode: Adaptive. Accuracy too low. Failure count: {consecutiveFailureCount}");
                    if (consecutiveFailureCount >= maxConsecutiveFailures)
                    {
                        EndGame("Game Over: Failed to meet accuracy threshold.");
                        yield break;
                    }
                }
                
                blockCorrectResponses = 0;
                blockExpectedMatches = 0;
                blockFalseAlarms = 0;
                UpdateInfoText();
            }
        }
    }
    
    private IEnumerator WaitForResponse()
    {
        awaitingInput = true;
        yield return new WaitForSeconds(stimulusDuration);
        awaitingInput = false;
        
        TrialData currentTrial = allTrialsData.Count > 0 ? allTrialsData[allTrialsData.Count - 1] : null;
       if (currentTrial != null && currentTrial.userResponseKey == "None")
        {
            bool wasAMiss = isLocationMatchExpected || isColorMatchExpected || isAuditoryMatchExpected;
            if (wasAMiss)
            {
                 UpdateScore(pointsForMiss);
                 currentTrial.pointsAwarded = pointsForMiss;
                 if (isLocationMatchExpected) currentTrial.outcome = "Miss_Location";
                 else if (isColorMatchExpected) currentTrial.outcome = "Miss_Color";
                 else if (isAuditoryMatchExpected) currentTrial.outcome = "Miss_Audio";
                 Debug.LogError("--- Missed Match! ---");
            }
        }
    }
    
    private void OnApplicationQuit() { SaveDataToCSV(); }

    void SaveDataToCSV()
    {
        if (allTrialsData.Count == 0) return;
        StringBuilder sb = new StringBuilder();
        string[] headers = { "TrialNumber", "Timestamp", "Mode", "N_Level", "PointsAwarded", "LocationMatchExpected", "ColorMatchExpected", "AuditoryMatchExpected", "UserResponseKey", "Outcome", "ReactionTime_ms" };
        sb.AppendLine(string.Join(",", headers));
        foreach (TrialData trial in allTrialsData)
        {
            string[] row = {
                trial.trialNumber.ToString(),
                trial.timestamp.ToString("F3"),
                trial.mode.ToString(),
                trial.nLevel.ToString(),
                trial.pointsAwarded.ToString(),
                trial.locationMatchExpected.ToString(),
                trial.colorMatchExpected.ToString(),
                trial.auditoryMatchExpected.ToString(),
                trial.userResponseKey ?? "None",
                trial.outcome ?? "NoResponse",
                trial.reactionTime.ToString("F0")
            };
            sb.AppendLine(string.Join(",", row));
        }
        string filePath = Path.Combine(Application.persistentDataPath, $"PlayerData_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.csv");
        try { File.WriteAllText(filePath, sb.ToString()); Debug.Log($"Data successfully saved to: {filePath}"); }
        catch (System.Exception e) { Debug.LogError($"Failed to save data: {e.Message}"); }
    }
}

[System.Serializable]
public class TrialData
{
    public int trialNumber;
    public float timestamp;
    public GameMode mode;
    public int nLevel;
    public int pointsAwarded;
    public bool locationMatchExpected;
    public bool colorMatchExpected;
    public bool auditoryMatchExpected;
    public string userResponseKey;
    public string outcome;
    public float reactionTime;
}