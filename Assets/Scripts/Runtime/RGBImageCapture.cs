using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class RGBImageCapture : MonoBehaviour
{
    [SerializeField]
    private ARCameraManager cameraManager;

    void Awake()
    {
        if (cameraManager == null)
            cameraManager = GetComponent<ARCameraManager>();
    }

    public void CaptureRGBFrame()
    {
        if (cameraManager.TryAcquireLatestCpuImage(out UnityEngine.XR.ARSubsystems.XRCpuImage image))
        {
            using (image)
            {
                try
                {
                    // Create timestamp for filename
                    string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    string baseFilename = Path.Combine(Application.persistentDataPath, $"rgb_frame_{timestamp}");

                    // Create the texture
                    var conversionParams = new UnityEngine.XR.ARSubsystems.XRCpuImage.ConversionParams
                    {
                        inputRect = new RectInt(0, 0, image.width, image.height),
                        outputDimensions = new Vector2Int(image.width, image.height),
                        outputFormat = TextureFormat.RGB24,
                        transformation = UnityEngine.XR.ARSubsystems.XRCpuImage.Transformation.MirrorY
                    };

                    int size = image.GetConvertedDataSize(conversionParams);
                    var buffer = new Unity.Collections.NativeArray<byte>(size, Unity.Collections.Allocator.Temp);
                    try
                    {
                        image.Convert(conversionParams, new Unity.Collections.NativeSlice<byte>(buffer));

                        // Create and save texture
                        var texture = new Texture2D(
                            conversionParams.outputDimensions.x,
                            conversionParams.outputDimensions.y,
                            TextureFormat.RGB24,
                            false);

                        texture.LoadRawTextureData(buffer);
                        texture.Apply();

                        // Save to PNG, etc...
                        // Save to file
                        byte[] pngData = texture.EncodeToPNG();
                        string pngPath = $"{baseFilename}.png";
                        File.WriteAllBytes(pngPath, pngData);

                        // Save metadata
                        SaveMetadata(image, $"{baseFilename}.meta");

                        Debug.Log($"RGB frame saved:\nPNG: {pngPath}");

                        // Clean up
                        Destroy(texture);
                    }
                    finally
                    {
                        if (buffer.IsCreated)
                            buffer.Dispose();
                    }

                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to save RGB frame: {e.Message}");
                }
            }
        }
        else
        {
            Debug.LogError("Failed to acquire latest camera image");
        }
    }

    private void SaveMetadata(UnityEngine.XR.ARSubsystems.XRCpuImage image, string filename)
    {
        var metadata = new ImageMetadata
        {
            width = image.width,
            height = image.height,
            timestamp = image.timestamp
        };

        string jsonMetadata = JsonUtility.ToJson(metadata, true);
        File.WriteAllText(filename, jsonMetadata);
    }

    [Serializable]
    private struct ImageMetadata
    {
        public int width;
        public int height;
        public double timestamp;
    }
}