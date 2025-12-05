using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraModeUI : MonoBehaviour
{
    public FlyCamera cameraController;
    public Button toggleButton;
    public TextMeshProUGUI buttonText;

    void Start() {
        if (toggleButton != null) {
            toggleButton.onClick.AddListener(ToggleMode);
        }
        UpdateButtonText();
    }

    void Update() {
        if (Input.GetKeyDown(cameraController.toggleKey)) {
            ToggleMode();
        }
    }

    public void ToggleMode() {
        if (cameraController != null) {
            cameraController.manualPilot = !cameraController.manualPilot;
            
            if (cameraController.manualPilot) {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            } else {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            
            UpdateButtonText();
            Debug.Log("Camera mode: " + (cameraController.manualPilot ? "MANUAL" : "AUTOPILOT"));
        }
    }

    void UpdateButtonText() {
        if (buttonText != null && cameraController != null) {
            buttonText.text = cameraController.manualPilot ? "Mode: Manual" : "Mode: Autopilot";
        }
    }
}