using UnityEngine;
using UnityEngine.UI;
using TMPro; // Required for TextMeshPro

public class PlayerNameUI : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField nameInputField;
    public Button confirmButton;
    public TextMeshProUGUI errorText; // Optional: To show "Please enter a name"

    [Header("Panel Navigation")]
    public GameObject currentPanel; // The panel with the input field (NamePanel)
    public GameObject nextPanel;    // The panel to open next (TrackSelectionPanel)

    private void Start()
    {
        // 1. Load saved name if it exists
        if (GamePersistenceManager.Instance != null)
        {
            nameInputField.text = GamePersistenceManager.Instance.playerName;
        }

        // 2. Add listener to the button
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(SubmitName);
        }

        // 3. Hide error text initially
        if (errorText != null) errorText.gameObject.SetActive(false);
    }

    public void SubmitName()
    {
        string enteredName = nameInputField.text;

        // Validation: Check if empty
        if (string.IsNullOrWhiteSpace(enteredName))
        {
            if (errorText != null)
            {
                errorText.text = "Please enter a name!";
                errorText.gameObject.SetActive(true);
            }
            return;
        }

        // 1. Save the name to the Manager
        if (GamePersistenceManager.Instance != null)
        {
            GamePersistenceManager.Instance.SetPlayerName(enteredName);
        }

        // 2. Switch Panels
        if (currentPanel != null) currentPanel.SetActive(false);
        if (nextPanel != null) nextPanel.SetActive(true);

        Debug.Log($"Name '{enteredName}' saved. Switching panels.");
    }
}