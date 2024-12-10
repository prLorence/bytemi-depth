using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class CaptureManager : MonoBehaviour
{
    [SerializeField] private DepthImageExporter depthExporter;
    [SerializeField] private RGBImageCapture rgbCapture;
    [SerializeField] private ServerManager serverManager;
    [SerializeField] private Button captureButton;

    private bool isCapturing = false;

    private void Start()
    {
        if (captureButton != null)
        {
            captureButton.onClick.AddListener(CaptureFrames);
        }

        ValidateComponents();
    }

    private void ValidateComponents()
    {
        if (depthExporter == null)
            Debug.LogError("DepthImageExporter not assigned!");
        if (rgbCapture == null)
            Debug.LogError("RGBImageCapture not assigned!");
    }

    private async void CaptureFrames()
    {
        if (isCapturing)
        {
            Debug.Log("Already capturing, please wait...");
            return;
        }

        try
        {
            isCapturing = true;
            captureButton.interactable = false;

            // Create parameters with consistent timestamp
            ServerManager.UploadParameters uploadParameters = new()
            {
                timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss"),
                basePath = Application.persistentDataPath,
                depthPrefix = "depth_frame_",
                rgbPrefix = "rgb_frame_"
            };

            Debug.Log($"Starting capture sequence with timestamp: {uploadParameters.timestamp}");

            // Ensure the directory exists
            Directory.CreateDirectory(uploadParameters.basePath);

            // Capture both frames
            depthExporter.CaptureDepthFrame();
            rgbCapture.CaptureRGBFrame();

            // Add small delay to ensure files are written
            await Task.Delay(1000);

            // Let server manager handle the upload
            if (serverManager != null)
            {
                await serverManager.UploadDepthData(uploadParameters);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during capture: {e.Message}");
        }
        finally
        {
            isCapturing = false;
            captureButton.interactable = true;
        }
    }
}
