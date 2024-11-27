using UnityEngine;
using UnityEngine.UI;

// In your UI or button handler script
public class CaptureButton : MonoBehaviour
{
    [SerializeField]
    private Button captureButton;
    private DepthImageExporter depthExporter;
    private RGBImageCapture rgbImageCapture;

    void Start()
    {
        // Find the DepthImageExporter in the scene
        depthExporter = Object.FindFirstObjectByType<DepthImageExporter>();
        rgbImageCapture = Object.FindFirstObjectByType<RGBImageCapture>();

        // Setup button click handler
        captureButton.onClick.AddListener(OnCaptureButtonPressed);
    }

    void OnCaptureButtonPressed()
    {
        if (depthExporter != null)
        {
            depthExporter.CaptureDepthFrame();
        }

        if (rgbImageCapture != null)
        {
            rgbImageCapture.CaptureRGBFrame();
        }
    }
}
