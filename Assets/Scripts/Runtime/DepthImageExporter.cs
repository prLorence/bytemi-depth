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

    void Awake()
    {
        // Auto-assign if not set in inspector
        if (occlusionManager == null)
            occlusionManager = GetComponent<AROcclusionManager>();

        if (displayDepthImage == null)
            displayDepthImage = GetComponent<DisplayDepthImage>();
    }


    // Structure to hold depth frame metadata
    [Serializable]
    public struct DepthFrameMetadata
    {
        public int width;
        public int height;
        public XRCpuImage.Format format;
        public double timestamp;
        // Add depth statistics
        public float centerPixelDepth;
        public float minDepth;
        public float maxDepth;
    }

    public void CaptureDepthFrame()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string baseFilename = Path.Combine(Application.persistentDataPath, $"depth_frame_{timestamp}");
        if (occlusionManager.TryAcquireEnvironmentDepthCpuImage(out XRCpuImage image))
        {
            using (image)
            {
                try
                {
                    SaveDepthData(image, $"{baseFilename}.raw");

                    // Save metadata
                    SaveMetadata(image, $"{baseFilename}.meta");

                    Debug.Log($"Depth frame saved: {baseFilename}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to save depth frame: {e.Message}");
                }
            }
        }

        LoadAndAnalyzeDepthFrame(timestamp);
    }

    private void SaveDepthData(XRCpuImage image, string filename)
    {
        var plane = image.GetPlane(0);

        using (FileStream fs = new FileStream(filename, FileMode.Create))
        using (BinaryWriter writer = new BinaryWriter(fs))
        {
            // Write raw depth data
            byte[] buffer = new byte[plane.data.Length];
            plane.data.CopyTo(buffer);
            writer.Write(buffer);
        }
    }

    private void SaveMetadata(XRCpuImage image, string filename)
    {
        // Calculate depth statistics
        var plane = image.GetPlane(0);
        var dataLength = plane.data.Length;
        var pixelStride = plane.pixelStride;
        var rowStride = plane.rowStride;

        // Get center pixel depth
        var centerRowIndex = dataLength / rowStride / 2;
        var centerPixelIndex = rowStride / pixelStride / 2;
        var centerPixelData = plane.data.GetSubArray(centerRowIndex * rowStride + centerPixelIndex * pixelStride, pixelStride);
        float centerDepth = displayDepthImage.convertPixelDataToDistanceInMeters(centerPixelData.ToArray(), image.format);

        // Calculate min/max depths and sample grid
        float minDepth = float.MaxValue;
        float maxDepth = float.MinValue;
        StringBuilder gridDepths = new StringBuilder();

        // Get 5x5 grid from center
        int gridSize = 5;
        int startX = (image.width / 2) - (gridSize / 2);
        int startY = (image.height / 2) - (gridSize / 2);

        for (int y = 0; y < gridSize; y++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                int index = (startY + y) * rowStride + (startX + x) * pixelStride;
                var pixelData = plane.data.GetSubArray(index, pixelStride).ToArray();
                float depth = displayDepthImage.convertPixelDataToDistanceInMeters(pixelData, image.format);

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

        var metadata = new DepthFrameMetadata
        {
            width = image.width,
            height = image.height,
            format = image.format,
            timestamp = image.timestamp,
            centerPixelDepth = centerDepth,
            minDepth = minDepth,
            maxDepth = maxDepth,
        };

        string jsonMetadata = JsonUtility.ToJson(metadata, true);
        File.WriteAllText(filename, jsonMetadata);
    }


    // Utility method to read back the depth data
    public static float[,] LoadDepthFrame(string rawFilename, string metaFilename)
    {
        // Read metadata
        string jsonMetadata = File.ReadAllText(metaFilename);
        DepthFrameMetadata metadata = JsonUtility.FromJson<DepthFrameMetadata>(jsonMetadata);

        // Read raw data
        byte[] rawData = File.ReadAllBytes(rawFilename);
        float[,] depthMap = new float[metadata.width, metadata.height];

        // Convert based on format
        for (int y = 0; y < metadata.height; y++)
        {
            for (int x = 0; x < metadata.width; x++)
            {
                int index = (y * metadata.width + x) * (metadata.format == XRCpuImage.Format.DepthFloat32 ? 4 : 2);

                if (metadata.format == XRCpuImage.Format.DepthFloat32)
                {
                    depthMap[x, y] = BitConverter.ToSingle(rawData, index);
                }
                else if (metadata.format == XRCpuImage.Format.DepthUint16)
                {
                    depthMap[x, y] = BitConverter.ToUInt16(rawData, index) / 1000f;
                }
            }
        }

        return depthMap;
    }

    public void LoadAndAnalyzeDepthFrame(string timestamp)
    {
        string basePath = Application.persistentDataPath;
        string rawFile = Path.Combine(basePath, $"depth_frame_{timestamp}.raw");
        string metaFile = Path.Combine(basePath, $"depth_frame_{timestamp}.meta");

        if (File.Exists(rawFile) && File.Exists(metaFile))
        {
            float[,] depthMap = DepthImageExporter.LoadDepthFrame(rawFile, metaFile);

            // Get dimensions
            int width = depthMap.GetLength(0);   // Typically 160 for ARCore
            int height = depthMap.GetLength(1);  // Typically 90 for ARCore

            // Print some example values
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"Depth Map Size: {width}x{height}");

            // Print center pixel depth
            float centerDepth = depthMap[width / 2, height / 2];
            sb.AppendLine($"Center pixel depth: {centerDepth:F3}m");

            // Print min/max depths
            float minDepth = float.MaxValue;
            float maxDepth = float.MinValue;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float depth = depthMap[x, y];
                    if (depth > 0) // Ignore invalid/zero depths
                    {
                        minDepth = Mathf.Min(minDepth, depth);
                        maxDepth = Mathf.Max(maxDepth, depth);
                    }
                }
            }
            sb.AppendLine($"Min depth: {minDepth:F3}m");
            sb.AppendLine($"Max depth: {maxDepth:F3}m");

            // Print a small sample grid (e.g., 5x5 from center)
            sb.AppendLine("\nSample 5x5 grid from center (in meters):");
            int gridSize = 5;
            int startX = (width / 2) - (gridSize / 2);
            int startY = (height / 2) - (gridSize / 2);

            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    float depth = depthMap[startX + x, startY + y];
                    sb.Append($"{depth:F3}m ");
                }
                sb.AppendLine();
            }

            Debug.Log(sb.ToString());
        }
    }
}