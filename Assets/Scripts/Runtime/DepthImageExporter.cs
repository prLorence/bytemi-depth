using System;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARFoundation.Samples;
using UnityEngine.XR.ARSubsystems;

public class DepthImageExporter : MonoBehaviour
{
    [SerializeField]
    private AROcclusionManager occlusionManager;

    [SerializeField]
    private DisplayDepthImage displayDepthImage;

    private bool isSaving = false;

    void Awake()
    {
        // Auto-assign if not set in inspector
        if (occlusionManager == null)
            occlusionManager = FindAnyObjectByType<AROcclusionManager>();

        if (occlusionManager == null)
            Debug.LogError("AROcclusionManager not found!");

        if (displayDepthImage == null)
            displayDepthImage = FindAnyObjectByType<DisplayDepthImage>();
    }

    [Serializable]
    public struct DepthFrameMetadata
    {
        public int width;
        public int height;
        public XRCpuImage.Format format;
        public double timestamp;
        public float centerPixelDepth;
        public float minDepth;
        public float maxDepth;
    }

    public async void CaptureDepthFrame()
    {
        if (isSaving)
        {
            Debug.Log("Already saving a depth frame, please wait...");
            return;
        }

        if (occlusionManager == null)
        {
            Debug.LogError("Cannot capture depth frame - AROcclusionManager is null");
            return;
        }

        try
        {
            isSaving = true;
            Debug.Log("Starting depth capture...");

            if (!occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage image))
            {
                Debug.LogError("Failed to acquire depth image");
                return;
            }

            using (image)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseFilename = Path.Combine(Application.persistentDataPath, $"depth_frame_{timestamp}");
                string rawPath = $"{baseFilename}.raw";
                string metaPath = $"{baseFilename}.meta";

                Debug.Log($"Preparing to save depth data to: {rawPath}");

                // Ensure directory exists
                string directory = Path.GetDirectoryName(rawPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                try
                {
                    // Save raw depth data
                    SaveDepthData(image, rawPath);

                    // Save metadata
                    SaveMetadata(image, metaPath);

                    // Wait for files to be written
                    int attempts = 0;
                    while ((!File.Exists(rawPath) || !File.Exists(metaPath)) && attempts < 10)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        attempts++;
                    }

                    if (!File.Exists(rawPath))
                    {
                        Debug.LogError($"Depth raw file was not created: {rawPath}");
                    }
                    if (!File.Exists(metaPath))
                    {
                        Debug.LogError($"Depth meta file was not created: {metaPath}");
                    }

                    Debug.Log($"Successfully saved depth frame to: {rawPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error saving depth data: {e.Message}\nStack trace: {e.StackTrace}");
                    throw;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in depth capture: {e.Message}\nStack trace: {e.StackTrace}");
        }
        finally
        {
            isSaving = false;
        }
    }

    private void SaveDepthData(XRCpuImage image, string filename)
    {
        var plane = image.GetPlane(0);
        Debug.Log($"Saving depth data with size: {plane.data.Length} bytes");

        using (FileStream fs = new FileStream(filename, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            byte[] buffer = new byte[plane.data.Length];
            plane.data.CopyTo(buffer);
            writer.Write(buffer);
        }

        Debug.Log($"Depth data saved to: {filename}");
    }

    private void SaveMetadata(XRCpuImage image, string filename)
    {
        try
        {
            var plane = image.GetPlane(0);
            var dataLength = plane.data.Length;
            var pixelStride = plane.pixelStride;
            var rowStride = plane.rowStride;

            // Calculate center pixel depth
            var centerRowIndex = dataLength / rowStride / 2;
            var centerPixelIndex = rowStride / pixelStride / 2;
            var centerPixelData = plane.data.GetSubArray(centerRowIndex * rowStride + centerPixelIndex * pixelStride, pixelStride);
            float centerDepth = displayDepthImage != null ?
                displayDepthImage.convertPixelDataToDistanceInMeters(centerPixelData.ToArray(), image.format) : 0;

            // Calculate min/max depths
            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;
            var gridDepths = CalculateGridDepths(image, plane, ref minDepth, ref maxDepth);

            var metadata = new DepthFrameMetadata
            {
                width = image.width,
                height = image.height,
                format = image.format,
                timestamp = image.timestamp,
                centerPixelDepth = centerDepth,
                minDepth = minDepth,
                maxDepth = maxDepth
            };

            string jsonMetadata = JsonUtility.ToJson(metadata, true);
            File.WriteAllText(filename, jsonMetadata);
            Debug.Log($"Depth metadata saved to: {filename}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving depth metadata: {e.Message}");
            throw;
        }
    }

    private string CalculateGridDepths(XRCpuImage image, XRCpuImage.Plane plane, ref float minDepth, ref float maxDepth)
    {
        StringBuilder gridDepths = new StringBuilder();
        int gridSize = 5;
        int startX = (image.width / 2) - (gridSize / 2);
        int startY = (image.height / 2) - (gridSize / 2);

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int index = (startY + y) * plane.rowStride + (startX + x) * plane.pixelStride;
                var pixelData = plane.data.GetSubArray(index, plane.pixelStride).ToArray();
                float depth = displayDepthImage != null ?
                    displayDepthImage.convertPixelDataToDistanceInMeters(pixelData, image.format) : 0;

                gridDepths.Append($"{depth:F3}");
                if (x < gridSize - 1) gridDepths.Append(" ");

                if (depth > 0)
                {
                    minDepth = Mathf.Min(minDepth, depth);
                    maxDepth = Mathf.Max(maxDepth, depth);
                }
            }
            if (y < gridSize - 1) gridDepths.AppendLine();
        }

        return gridDepths.ToString();
    }
}