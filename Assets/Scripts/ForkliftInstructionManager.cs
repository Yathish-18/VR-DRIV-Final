using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForkliftInstructionManager : MonoBehaviour
{
    [System.Serializable]
    public class InstructionStep
    {
        public string text;
        public bool requiresCheck;
        [HideInInspector] public bool isCompleted;
    }

    [Header("Instruction Steps")]
    public List<InstructionStep> steps = new List<InstructionStep>();

    [Header("UI References")]
    public Canvas instructionCanvas;
    public TextMeshProUGUI instructionText;
    public Button nextButton;

    [Header("Vehicle Reference")]
    public G29VehicleInput vehicle;

    [Header("Audio")]
    public AudioSource audioSource;
    public List<AudioClip> instructionVoiceovers; // Should match number of steps

    private int currentStep = 0;
    private bool awaitingCheck = false;

    void Start()
    {
        nextButton.onClick.AddListener(HandleNextPressed);
        ShowInstruction(currentStep);
    }

    void Update()
    {
        if (awaitingCheck && steps[currentStep].requiresCheck)
        {
            if (CheckConditionForStep(currentStep))
            {
                steps[currentStep].isCompleted = true;
                awaitingCheck = false;
                GoToNextStep();
            }
        }
    }

    void HandleNextPressed()
    {
        if (steps[currentStep].requiresCheck)
        {
            instructionCanvas.enabled = false;
            awaitingCheck = true;
        }
        else
        {
            GoToNextStep();
        }
    }

    void GoToNextStep()
    {
        currentStep++;
        if (currentStep < steps.Count)
        {
            ShowInstruction(currentStep);
        }
        else
        {
            instructionCanvas.enabled = false; // End of steps
        }
    }

    void ShowInstruction(int index)
    {
        instructionText.text = steps[index].text;
        instructionCanvas.enabled = true;

        // Play corresponding voiceover
        if (audioSource != null && instructionVoiceovers != null && index < instructionVoiceovers.Count)
        {
            audioSource.clip = instructionVoiceovers[index];
            audioSource.Play();
        }
    }

    public void OnBoxLiftedExternally()
    {
        if (currentStep < steps.Count && steps[currentStep].requiresCheck)
        {
            steps[currentStep].isCompleted = true;
            awaitingCheck = false;
            GoToNextStep();
        }
    }

    public void OnHolderMoved()
    {
        if (currentStep < steps.Count && steps[currentStep].requiresCheck)
        {
            steps[currentStep].isCompleted = true;
            awaitingCheck = false;
            GoToNextStep();
        }
    }

    bool CheckConditionForStep(int stepIndex)
    {
        switch (stepIndex)
        {
            case 1: // Step 2 - Gear Forward
                return vehicle != null && vehicle.GearState == 1;

            case 2: // Step 3 - Pedal Pressed
                return vehicle != null && vehicle.AcceleratorValue > 0.1f;

            case 3: // Step 4 - Steering used
                return vehicle != null && Mathf.Abs(vehicle.SteeringValue) > 0.1f;

            case 6: // Step 7 - Box lift (handled externally)
                return false;

            case 8: // Step 8 - Holder moved (handled externally)
                return false;

            default:
                return false;
        }
    }
}
