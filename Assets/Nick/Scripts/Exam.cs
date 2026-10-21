using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;                 // SelectEnterEventArgs
using UnityEngine.XR.Interaction.Toolkit.Interactables;   // XRSimpleInteractable
using TMPro;

public class Exam : MonoBehaviour
{
    // ---------------- DATA (just text + the correct answer) ----------------
    [System.Serializable]
    public class Question
    {
        [TextArea] public string questionString;
        public string answerAText;
        public string answerBText;
        public string answerCText;
        public string answerDText;

        [Tooltip("0 = A, 1 = B, 2 = C, 3 = D")]
        public int correctAnswerIndex;

        [HideInInspector] public int selectedIndex = -1; // -1 means "not answered yet"
    }

    // ---------------- INSPECTOR REFERENCES ----------------
    [Header("Questions")]
    public Question[] questions;

    [Header("Answer Buttons")]
    public XRSimpleInteractable answerA;
    public XRSimpleInteractable answerB;
    public XRSimpleInteractable answerC;
    public XRSimpleInteractable answerD;

    [Header("Navigation Buttons")]
    public XRSimpleInteractable previousButton;
    public XRSimpleInteractable nextButton;

    [Header("Paper")]
    public TextMeshProUGUI paperText;

    [Header("Hide until exam begins")]
    [Tooltip("Drag in Paper and Question1Colliders. These stay hidden until BeginExam() runs.")]
    public GameObject[] examVisuals;

    [Header("Testing")]
    [Tooltip("ON = exam starts immediately on Play (skips Timeline). Turn OFF for the real build.")]
    public bool beginOnStartForTesting = false;

    // ---------------- RUNTIME STATE ----------------
    private XRSimpleInteractable[] answerButtons;
    private int currentIndex = 0;
    private bool examStarted = false;
    private bool examFinished = false;

    // ---------------- SETUP ----------------
    private void Awake()
    {
        PopulateDefaultQuestionsIfEmpty();

        answerButtons = new XRSimpleInteractable[] { answerA, answerB, answerC, answerD };

        foreach (var b in answerButtons)
            if (b != null) b.selectEntered.AddListener(HandleAnswerPressed);

        if (previousButton != null) previousButton.selectEntered.AddListener(HandlePreviousPressed);
        if (nextButton != null)     nextButton.selectEntered.AddListener(HandleNextPressed);

        SetVisuals(false); // hidden until the video finishes
    }

    private void Start()
    {
        if (beginOnStartForTesting) BeginExam();
    }

    private void OnDestroy()
    {
        if (answerButtons != null)
            foreach (var b in answerButtons)
                if (b != null) b.selectEntered.RemoveListener(HandleAnswerPressed);

        if (previousButton != null) previousButton.selectEntered.RemoveListener(HandlePreviousPressed);
        if (nextButton != null)     nextButton.selectEntered.RemoveListener(HandleNextPressed);
    }

    // ---------------- TIMELINE CALLS THIS ----------------
    public void BeginExam()
    {
        if (questions == null || questions.Length == 0)
        {
            Debug.LogError("Exam has no questions assigned!");
            return;
        }

        examStarted = true;
        examFinished = false;
        currentIndex = 0;
        foreach (var q in questions) q.selectedIndex = -1; // reset for retakes

        SetVisuals(true);
        RefreshPaper();
    }

    // ---------------- BUTTON HANDLERS ----------------
    private void HandleAnswerPressed(SelectEnterEventArgs args)
    {
        if (!examStarted || examFinished) return;

        Transform pressed = args.interactableObject.transform;
        int picked = System.Array.FindIndex(answerButtons, b => b != null && b.transform == pressed);
        if (picked < 0) return;

        questions[currentIndex].selectedIndex = picked;

        // --- C1 hook: if you later want to highlight the physical button, do it here ---

        RefreshPaper();
        TryAutoGrade(); // D2: grade once everything is answered
    }

    private void HandlePreviousPressed(SelectEnterEventArgs args)
    {
        if (!examStarted || examFinished) return;
        if (currentIndex > 0) { currentIndex--; RefreshPaper(); }
    }

    private void HandleNextPressed(SelectEnterEventArgs args)
    {
        if (!examStarted || examFinished) return;
        if (currentIndex < questions.Length - 1) { currentIndex++; RefreshPaper(); }
    }

    // ---------------- DISPLAY ----------------
    private void RefreshPaper()
    {
        if (paperText == null) { Debug.LogError("Paper Text is not assigned!"); return; }

        Question q = questions[currentIndex];

        string text = $"Question {currentIndex + 1} of {questions.Length}\n\n";
        text += q.questionString + "\n\n";
        text += FormatOption(0, "A", q.answerAText, q.selectedIndex);
        text += FormatOption(1, "B", q.answerBText, q.selectedIndex);
        text += FormatOption(2, "C", q.answerCText, q.selectedIndex);
        text += FormatOption(3, "D", q.answerDText, q.selectedIndex);

        paperText.text = text;
    }

    private string FormatOption(int index, string letter, string optionText, int selectedIndex)
    {
        string marker = (index == selectedIndex) ? "► " : "   "; // C2: marks the chosen answer
        return $"{marker}{letter}. {optionText}\n";
    }

    // ---------------- GRADING (D2) ----------------
    private void TryAutoGrade()
    {
        foreach (var q in questions)
            if (q.selectedIndex < 0) return; // something still unanswered

        GradeExam();
    }

    private void GradeExam()
    {
        int correct = 0;
        foreach (var q in questions)
            if (q.selectedIndex == q.correctAnswerIndex) correct++;

        examFinished = true;

        paperText.text =
            $"Exam Complete!\n\n" +
            $"Score: {correct} / {questions.Length}\n\n" +
            $"{Mathf.RoundToInt(100f * correct / questions.Length)}%";
    }

    // ---------------- HELPERS ----------------
    private void SetVisuals(bool on)
    {
        if (examVisuals == null) return;
        foreach (var go in examVisuals)
            if (go != null) go.SetActive(on);
    }

    private void PopulateDefaultQuestionsIfEmpty()
    {
        if (questions != null && questions.Length > 0) return; // Inspector wins if filled

        questions = new Question[]
        {
            new Question {
                questionString = "What did the elderly woman want when she held the man's arm?",
                answerAText = "She was trying to avoid falling.",
                answerBText = "She wanted help crossing the street.",
                answerCText = "She wanted to go for a walk with him.",
                answerDText = "She mistook him for her son.",
                correctAnswerIndex = 1  // B
            },
            new Question {
                questionString = "What decision did the man make after looking at his hand?",
                answerAText = "He wanted to play.",
                answerBText = "He decided to go to sleep.",
                answerCText = "He wanted to find someone he cared about.",
                answerDText = "He decided to help people.",
                correctAnswerIndex = 3  // D
            },
            new Question {
                questionString = "Why did the man pretend to be blind?",
                answerAText = "He wanted the other man to help him.",
                answerBText = "He was hiding from another person.",
                answerCText = "He lost his ability to see.",
                answerDText = "He was conducting an experiment.",
                correctAnswerIndex = 0  // A
            }
        };
    }
}