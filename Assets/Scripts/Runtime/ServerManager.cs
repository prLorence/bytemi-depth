using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation.Samples.Assets.Scripts.Runtime;

public class ServerManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField serverUriInput;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private Text resultText;
    [SerializeField] private Button saveSettingsButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button closeResultsPanelButton;

    [Header("Debug Settings")]
    [SerializeField] private bool useTestResponse = false;

    private static HttpClient httpClient;
    private string serverUri = "http://192.168.210.25:5000"; // Default URI

    public struct UploadParameters
    {
        public string timestamp;       // Added timestamp field
        public string basePath;        // Base path for all files
        public string depthPrefix;     // Prefix for depth files (e.g., "depth_frame_")
        public string rgbPrefix;       // Prefix for RGB files (e.g., "rgb_frame_")

        // Helper methods to get full paths
        public string GetDepthRawPath() => Path.Combine(basePath, $"{depthPrefix}{timestamp}.raw");
        public string GetDepthMetaPath() => Path.Combine(basePath, $"{depthPrefix}{timestamp}.meta");
        public string GetRgbImagePath() => Path.Combine(basePath, $"{rgbPrefix}{timestamp}.png");
        public string GetRgbMetaPath() => Path.Combine(basePath, $"{rgbPrefix}{timestamp}.meta");
    }

    private void OnEnable()
    {
        settingsPanel.SetActive(false);
        resultsPanel.SetActive(false);
    }

    void Awake()
    {
        httpClient = new HttpClient();

        if (serverUriInput != null)
            serverUriInput.text = serverUri;

        if (saveSettingsButton != null)
            saveSettingsButton.onClick.AddListener(SaveSettings);

        if (cancelButton != null)
            cancelButton.onClick.AddListener(HideSettingsPanel);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(ShowSettingsPanel);

        if (closeResultsPanelButton != null)
            closeResultsPanelButton.onClick.AddListener(HideResultsPanel);
    }

    public void HideResultsPanel()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(false);
    }

    public void ShowResultsPanel()
    {
        if (resultsPanel != null)
            resultsPanel.SetActive(true);
    }

    public void ShowSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    public void HideSettingsPanel()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void SaveSettings()
    {
        if (serverUriInput != null)
        {
            serverUri = serverUriInput.text;
            Debug.Log($"Server URI updated to: {serverUri}");
        }
        HideSettingsPanel();
    }

    private void DisplayNutritionResults(NutritionApiResponse response)
    {
        if (resultText == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("Nutrition Analysis Results:\n");

        foreach (var macroData in response.macronutrients.data)
        {
            var nutritionInfo = FoodNutritionInfo.FromApiData(response.data, macroData);

            sb.AppendLine($"Food: {nutritionInfo.foodName.ToUpper()}");
            sb.AppendLine($"Estimated Volume (EV): {nutritionInfo.estimatedVolume:F2} {nutritionInfo.volumeUnit}");
            sb.AppendLine($"Calculated Weight (from EV): {nutritionInfo.calculatedWeight} {nutritionInfo.weightUnit}");
            sb.AppendLine("Macronutrients:");
            sb.AppendLine($"  Calories: {nutritionInfo.calories} ");
            sb.AppendLine($"  Protein: {nutritionInfo.protein:F1}g");
            sb.AppendLine($"  Fat: {nutritionInfo.fat:F1}g");
            sb.AppendLine($"  Carbs: {nutritionInfo.carbohydrates:F1}g\n");
        }

        resultText.text = sb.ToString();
        ShowResultsPanel();
    }

    public async Task<bool> UploadDepthData(UploadParameters request)
    {
        try
        {
            Debug.Log("Starting file upload process...");

            httpClient.Timeout = TimeSpan.FromMinutes(5);

            using (var multipartContent = new MultipartFormDataContent())
            {
                try
                {
                    // 3. Add RGB image with explicit error handling
                    string rgbPath = request.GetRgbImagePath();
                    if (File.Exists(rgbPath))
                    {
                        try
                        {
                            byte[] rgbFileBytes = await File.ReadAllBytesAsync(rgbPath);
                            var rgbContent = new ByteArrayContent(rgbFileBytes);
                            multipartContent.Add(rgbContent, "rgb_image", Path.GetFileName(rgbPath));
                            Debug.Log($"Successfully added RGB image ({rgbFileBytes.Length} bytes)");
                        }
                        catch (IOException ex)
                        {
                            Debug.LogError($"Error reading RGB file: {ex.Message}");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"RGB file not found at: {rgbPath}");
                        return false;
                    }

                    // 4. Add depth image with explicit error handling
                    if (File.Exists(request.GetDepthRawPath()))
                    {
                        try
                        {
                            byte[] depthFileBytes = await File.ReadAllBytesAsync(request.GetDepthRawPath());
                            var depthContent = new ByteArrayContent(depthFileBytes);
                            multipartContent.Add(depthContent, "depth_image", Path.GetFileName(request.GetDepthRawPath()));
                            Debug.Log($"Successfully added depth image ({depthFileBytes.Length} bytes)");
                        }
                        catch (IOException ex)
                        {
                            Debug.LogError($"Error reading depth file: {ex.Message}");
                            return false;
                        }
                    }
                    else
                    {
                        Debug.LogError($"Depth file not found at: {request.GetDepthRawPath()}");
                        return false;
                    }

                    // 5. Add metadata files with explicit error handling
                    try
                    {
                        if (File.Exists(request.GetRgbMetaPath()))
                        {
                            string rgbMetaContent = await File.ReadAllTextAsync(request.GetRgbMetaPath());
                            var rgbMetaTextContent = new StringContent(rgbMetaContent);
                            multipartContent.Add(rgbMetaTextContent, "rgb_meta");
                            Debug.Log("Added RGB metadata");
                        }

                        if (File.Exists(request.GetDepthMetaPath()))
                        {
                            string depthMetaContent = await File.ReadAllTextAsync(request.GetDepthMetaPath());
                            var depthMetaTextContent = new StringContent(depthMetaContent);
                            multipartContent.Add(depthMetaTextContent, "depth_meta");
                            Debug.Log("Added depth metadata");
                        }
                    }
                    catch (IOException ex)
                    {
                        Debug.LogError($"Error reading metadata files: {ex.Message}");
                        return false;
                    }

                    // 6. Send request with retry logic
                    const int maxRetries = 3;
                    for (int retry = 0; retry < maxRetries; retry++)
                    {
                        try
                        {
                            Debug.Log($"Sending files to server (attempt {retry + 1}/{maxRetries}): {serverUri}/process");

                            using (var response = await httpClient.PostAsync($"{serverUri}/process", multipartContent))
                            {
                                Debug.Log($"Server responded with status: {response.StatusCode}");

                                if (response.IsSuccessStatusCode)
                                {
                                    if (useTestResponse)
                                    {
                                        Debug.Log("Using test response data");
                                        var dummyResponse = NutritionTestData.CreateDummyResponse();
                                        DisplayNutritionResults(dummyResponse);
                                        return true;
                                    }
                                    else
                                    {
                                        string jsonResponse = await response.Content.ReadAsStringAsync();
                                        var nutritionResponse = JsonUtility.FromJson<NutritionApiResponse>(jsonResponse);
                                        DisplayNutritionResults(nutritionResponse);
                                        return true;
                                    }
                                }
                                else
                                {
                                    string errorContent = await response.Content.ReadAsStringAsync();
                                    Debug.LogError($"Server returned error {response.StatusCode}: {errorContent}");

                                    // Only retry on specific status codes
                                    if ((int)response.StatusCode < 500)  // Don't retry on 4xx errors
                                        return false;

                                    if (retry < maxRetries - 1)
                                        await Task.Delay(1000 * (retry + 1));  // Exponential backoff
                                }
                            }
                        }
                        catch (HttpRequestException ex)
                        {
                            Debug.LogError($"Network error (attempt {retry + 1}): {ex.Message}");
                            if (retry < maxRetries - 1)
                                await Task.Delay(1000 * (retry + 1));
                        }
                    }

                    Debug.LogError("Failed to upload after all retry attempts");
                    return false;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error preparing form data: {e.Message}\nStack trace: {e.StackTrace}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error in upload process: {e.Message}\nStack trace: {e.StackTrace}");
            return false;
        }
    }

    private void OnDestroy()
    {
        httpClient?.Dispose();
    }

    // For testing the UI directly from the Unity Editor
    [ContextMenu("Test Display With Dummy Data")]
    private void TestDisplayWithDummyData()
    {
        var dummyResponse = NutritionTestData.CreateDummyResponse();
        DisplayNutritionResults(dummyResponse);
    }
}