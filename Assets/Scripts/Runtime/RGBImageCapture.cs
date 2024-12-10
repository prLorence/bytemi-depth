using System;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

public class RGBImageCapture : MonoBehaviour
{
    [SerializeField]
    private ARCameraManager cameraManager;

    private bool isSaving = false;

    void Awake()
    {
        if (cameraManager == null)
            cameraManager = FindAnyObjectByType<ARCameraManager>();

        if (cameraManager == null)
            Debug.LogError("ARCameraManager not found!");
    }

    public async void CaptureRGBFrame()
    {
        if (isSaving)
        {
            Debug.Log("Already saving an image, please wait...");
            return;
        }

        if (cameraManager == null)
        {
            Debug.LogError("Cannot capture RGB frame - ARCameraManager is null");
            return;
        }

        try
        {
            isSaving = true;
            Debug.Log("Starting RGB capture...");

            if (!cameraManager.TryAcquireLatestCpuImage(out UnityEngine.XR.ARSubsystems.XRCpuImage image))
            {
                Debug.LogError("Failed to acquire latest CPU image");
                return;
            }

            using (image)
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string baseFilename = Path.Combine(Application.persistentDataPath, $"rgb_frame_{timestamp}");
                string pngPath = $"{baseFilename}.png";

                Debug.Log($"Preparing to save RGB image to: {pngPath}");

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

                    var texture = new Texture2D(
                        conversionParams.outputDimensions.x,
                        conversionParams.outputDimensions.y,
                        TextureFormat.RGB24,
                        false);

                    texture.LoadRawTextureData(buffer);
                    texture.Apply();

                    // Convert to PNG
                    byte[] pngData = texture.EncodeToPNG();
                    if (pngData == null || pngData.Length == 0)
                    {
                        Debug.LogError("Failed to encode texture to PNG");
                        return;
                    }

                    // Ensure directory exists
                    string directory = Path.GetDirectoryName(pngPath);
                    if (!Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Save PNG file
                    File.WriteAllBytes(pngPath, pngData);
                    Debug.Log($"Successfully saved RGB image to: {pngPath}");

                    // Save metadata
                    SaveMetadata(image, $"{baseFilename}.meta");

                    // Wait for file to be written
                    int attempts = 0;
                    while (!File.Exists(pngPath) && attempts < 10)
                    {
                        await System.Threading.Tasks.Task.Delay(100);
                        attempts++;
                    }

                    if (!File.Exists(pngPath))
                    {
                        Debug.LogError($"File was not created after save operation: {pngPath}");
                    }

                    // Clean up
                    Texture2D.Destroy(texture);
                }
                finally
                {
                    if (buffer.IsCreated)
                        buffer.Dispose();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error capturing RGB frame: {e.Message}\nStack trace: {e.StackTrace}");
        }
        finally
        {
            isSaving = false;
        }
    }

    private void SaveMetadata(UnityEngine.XR.ARSubsystems.XRCpuImage image, string filename)
    {
        try
        {
            var metadata = new ImageMetadata
            {
                width = image.width,
                height = image.height,
                timestamp = image.timestamp
            };

            string jsonMetadata = JsonUtility.ToJson(metadata, true);
            File.WriteAllText(filename, jsonMetadata);
            Debug.Log($"Successfully saved RGB metadata to: {filename}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Error saving RGB metadata: {e.Message}");
        }
    }

    [Serializable]
    private struct ImageMetadata
    {
        public int width;
        public int height;
        public double timestamp;
    }
}