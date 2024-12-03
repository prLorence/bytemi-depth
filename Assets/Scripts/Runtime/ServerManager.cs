using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField serverUriInput;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button saveSettingsButton;
    [SerializeField] private Button cancelButton;
    [SerializeField] private Button settingsButton;

    private static HttpClient httpClient;
    // private string serverUri = "http://localhost:5000"; // Default URI
    private string serverUri = "http://192.168.68.111:5000"; // Default URI

    public struct UploadParameters
    {
        public string rawFilePath;
        public string metaRawFilePath;
        public string rgbFilePath;
        public string metaRgbFilePath;
    }
    private void OnEnable()
    {
        settingsPanel.SetActive(false);
    }

    void Awake()
    {
        // Initialize HttpClient
        httpClient = new HttpClient();

        // Initialize UI
        if (serverUriInput != null)
        {
            serverUriInput.text = serverUri;
        }

        if (saveSettingsButton != null)
        {
            saveSettingsButton.onClick.AddListener(SaveSettings);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(HideSettingsPanel);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(ShowSettingsPanel);
        }
    }

    public void ShowSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(true);
        }
    }

    public void HideSettingsPanel()
    {
        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }
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

    public async Task<bool> UploadDepthData(UploadParameters request)
    {
        try
        {
            using (var multipartContent = new MultipartFormDataContent())
            {
                // Add raw file
                byte[] rawFileBytes = File.ReadAllBytes(request.rawFilePath);
                var rawContent = new ByteArrayContent(rawFileBytes);
                multipartContent.Add(rawContent, "raw", Path.GetFileName(request.rawFilePath));

                // Add meta file
                byte[] metaRawFileBytes = File.ReadAllBytes(request.metaRawFilePath);
                var metaRawContent = new ByteArrayContent(metaRawFileBytes);
                multipartContent.Add(metaRawContent, "metadata-raw", Path.GetFileName(request.metaRawFilePath));

                byte[] rgbFileBytes = File.ReadAllBytes(request.rawFilePath);
                var rgbContent = new ByteArrayContent(rawFileBytes);
                multipartContent.Add(rgbContent, "raw", Path.GetFileName(request.rawFilePath));

                byte[] metaRgbFileBytes = File.ReadAllBytes(request.metaRawFilePath);
                var metaRgbContent = new ByteArrayContent(metaRawFileBytes);
                multipartContent.Add(metaRgbContent, "metadata-rgb", Path.GetFileName(request.metaRawFilePath));

                // Send request
                var response = await httpClient.PostAsync($"{serverUri}/upload", multipartContent);

                if (response.IsSuccessStatusCode)
                {
                    // Save the returned image
                    byte[] imageBytes = await response.Content.ReadAsByteArrayAsync();
                    string outputPath = Path.Combine(Application.persistentDataPath,
                        $"depth_visualization_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                    File.WriteAllBytes(outputPath, imageBytes);
                    Debug.Log($"Depth visualization saved to: {outputPath}");
                    return true;
                }
                else
                {
                    Debug.LogError($"Server returned error: {response.StatusCode}");
                    return false;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error uploading depth data: {e}");
            return false;
        }
    }

    private void OnDestroy()
    {
        httpClient?.Dispose();
    }
}