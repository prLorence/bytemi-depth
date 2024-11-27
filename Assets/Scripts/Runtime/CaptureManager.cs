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

    private void Start()
    {
        if (captureButton != null)
        {
            captureButton.onClick.AddListener(CaptureAndUpload);
        }
    }

    private async void CaptureAndUpload()
    {
        try
        {
            // Get the latest captured files
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string baseFilename = Path.Combine(Application.persistentDataPath, $"depth_frame_{timestamp}");
            string rawFilePath = $"{baseFilename}.raw";
            string metaFilePath = $"{baseFilename}.meta";

            // Capture depth and RGB
            depthExporter.CaptureDepthFrame();
            rgbCapture.CaptureRGBFrame();

            // Small delay to ensure files are written
            await Task.Delay(100);

            // Upload to server
            bool success = await serverManager.UploadDepthData(rawFilePath, metaFilePath);
            if (success)
            {
                Debug.Log("Upload successful!");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in capture and upload process: {e.Message}");
        }
    }
}