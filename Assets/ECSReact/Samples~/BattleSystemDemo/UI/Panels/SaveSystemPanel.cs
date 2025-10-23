using UnityEngine;
using UnityEngine.UI;
using ECSReact.Core;
using TMPro;

namespace ECSReact.Samples.BattleSystem
{
  /// <summary>
  /// UI Panel for save system controls and status display.
  /// Demonstrates async operation feedback and user controls.
  /// </summary>
  public class SaveSystemPanel : ReactiveUIComponent<SaveState>
  {
    [Header("Save Controls")]
    [SerializeField] private Button manualSaveButton;

    [Header("Status Display")]
    [SerializeField] private TextMeshProUGUI saveStatusText;
    [SerializeField] private GameObject loadingSpinner;
    [SerializeField] private Image statusIcon;
    [SerializeField] private TextMeshProUGUI lastSaveTimeText;

    [Header("Error Display")]
    [SerializeField] private GameObject errorPanel;
    [SerializeField] private TextMeshProUGUI errorMessageText;
    [SerializeField] private Button clearErrorButton;

    [Header("Visual Configuration")]
    [SerializeField] private Color idleColor = Color.white;
    [SerializeField] private Color savingColor = Color.yellow;
    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color errorColor = Color.red;

    private SaveState currentSaveState;
    private float spinnerRotation = 0f;

    public override void OnStateChanged(SaveState newState)
    {
      currentSaveState = newState;
      UpdateSaveStatus();
    }

    protected override void Start()
    {
      base.Start();
      SetupUIControls();
    }

    private void Update()
    {
      // Animate loading spinner
      if (currentSaveState.IsSaving && loadingSpinner && loadingSpinner.activeInHierarchy) {
        spinnerRotation += 360f * Time.deltaTime; // One rotation per second
        loadingSpinner.transform.rotation = Quaternion.Euler(0, 0, spinnerRotation);
      }
    }

    private void SetupUIControls()
    {
      if (manualSaveButton) {
        manualSaveButton.onClick.AddListener(OnManualSaveClicked);
      }

      if (clearErrorButton) {
        clearErrorButton.onClick.AddListener(OnClearErrorClicked);
      }
    }

    private void UpdateSaveStatus()
    {
      // Update status text
      if (saveStatusText) {
        saveStatusText.text = currentSaveState.currentStatus switch
        {
          SaveStatus.Idle => "Ready to save",
          SaveStatus.InProgress => $"Saving {currentSaveState.currentFileName.ToString()}...",
          SaveStatus.Completed => "Save completed successfully",
          SaveStatus.Failed => "Save failed",
          _ => "Unknown status"
        };
        saveStatusText.color = GetStatusColor(currentSaveState.currentStatus);
      }

      // Update status icon
      if (statusIcon) {
        statusIcon.color = GetStatusColor(currentSaveState.currentStatus);
      }

      // Show/hide loading spinner
      if (loadingSpinner) {
        loadingSpinner.SetActive(currentSaveState.IsSaving);
      }

      // Update manual save button
      if (manualSaveButton) {
        manualSaveButton.interactable = !currentSaveState.IsSaving;
        var buttonText = manualSaveButton.GetComponentInChildren<Text>();
        if (buttonText) {
          buttonText.text = currentSaveState.IsSaving ? "Saving..." : "Save Battle";
        }
      }

      // Update last save time
      if (lastSaveTimeText) {
        if (currentSaveState.lastSaveCompletedTime > 0) {
          float timeSince = currentSaveState.TimeSinceLastSave;
          lastSaveTimeText.text = timeSince < 60
              ? $"Last saved: {timeSince:F0}s ago"
              : $"Last saved: {timeSince / 60:F1}m ago";
        } else {
          lastSaveTimeText.text = "No saves yet";
        }
      }

      // Update error panel
      if (errorPanel) {
        errorPanel.SetActive(currentSaveState.HasError);
        if (currentSaveState.HasError && errorMessageText) {
          errorMessageText.text = currentSaveState.lastErrorMessage.ToString();
        }
      }
    }

    private Color GetStatusColor(SaveStatus status)
    {
      return status switch
      {
        SaveStatus.Idle => idleColor,
        SaveStatus.InProgress => savingColor,
        SaveStatus.Completed => successColor,
        SaveStatus.Failed => errorColor,
        _ => idleColor
      };
    }

    private void OnManualSaveClicked()
    {
      if (!currentSaveState.IsSaving) {
        DispatchAction(new SaveBattleAction
        {
          fileName = default, // Auto-generate filename (empty FixedString)
          format = SaveFormat.JSON
        });
      }
    }

    private void OnClearErrorClicked()
    {
      DispatchAction(new ClearSaveErrorAction());
    }
  }
}