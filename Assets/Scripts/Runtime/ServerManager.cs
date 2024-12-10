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
    private string serverUri = "http://192.168.68.111:5000"; // Default URI

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
            Debug.Log($"Starting upload process for timestamp: {request.timestamp}");

            using (var multipartContent = new MultipartFormDataContent())
            {
                // Get full file paths
                string rgbPath = request.GetRgbImagePath();
                string depthPath = request.GetDepthRawPath();
                string rgbMetaPath = request.GetRgbMetaPath();
                string depthMetaPath = request.GetDepthMetaPath();

                // Log all paths for debugging
                Debug.Log($"RGB Image Path: {rgbPath}");
                Debug.Log($"Depth Raw Path: {depthPath}");
                Debug.Log($"RGB Meta Path: {rgbMetaPath}");
                Debug.Log($"Depth Meta Path: {depthMetaPath}");

                // Add RGB image file
                if (File.Exists(rgbPath))
                {
                    byte[] rgbFileBytes = File.ReadAllBytes(rgbPath);
                    var rgbContent = new ByteArrayContent(rgbFileBytes);
                    multipartContent.Add(rgbContent, "rgb_image", Path.GetFileName(rgbPath));
                    Debug.Log("Added RGB image to form data");
                }
                else
                {
                    Debug.LogError($"RGB file not found at: {rgbPath}");
                    return false;
                }

                // Add depth image file
                if (File.Exists(depthPath))
                {
                    byte[] depthFileBytes = File.ReadAllBytes(depthPath);
                    var depthContent = new ByteArrayContent(depthFileBytes);
                    multipartContent.Add(depthContent, "depth_image", Path.GetFileName(depthPath));
                    Debug.Log("Added depth image to form data");
                }
                else
                {
                    Debug.LogError($"Depth file not found at: {depthPath}");
                    return false;
                }

                // Add metadata files
                if (File.Exists(rgbMetaPath) && File.Exists(depthMetaPath))
                {
                    string rgbMetaContent = File.ReadAllText(rgbMetaPath);
                    string depthMetaContent = File.ReadAllText(depthMetaPath);

                    multipartContent.Add(new StringContent(rgbMetaContent), "rgb_meta");
                    multipartContent.Add(new StringContent(depthMetaContent), "depth_meta");
                    Debug.Log("Added metadata to form data");
                }
                else
                {
                    Debug.LogError($"Metadata files not found. RGB Meta: {rgbMetaPath}, Depth Meta: {depthMetaPath}");
                    return false;
                }

                // Send request
                Debug.Log($"Sending files to server: {serverUri}/process");
                var response = await httpClient.PostAsync($"{serverUri}/process", multipartContent);
                Debug.Log($"Server responded with status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    if (useTestResponse)
                    {
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