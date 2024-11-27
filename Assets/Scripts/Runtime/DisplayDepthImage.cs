using System;
using System.Collections;
using System.Text;
using UnityEngine.Assertions;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;

namespace UnityEngine.XR.ARFoundation.Samples
{
    /// <summary>
    /// This component displays a picture-in-picture view of the environment depth texture, the human depth texture, or
    /// the human stencil texture.
    /// </summary>
    public class DisplayDepthImage : MonoBehaviour
    {
        /// <summary>
        /// The display mode for the texture widget. Values must match the UI dropdown.
        /// </summary>
        enum DisplayMode
        {
            EnvironmentDepthRaw = 0,
            EnvironmentDepthSmooth = 1,
            HumanDepth = 2,
            HumanStencil = 3,
        }

        /// <summary>
        /// Name of the max distance property in the shader.
        /// </summary>
        const string k_MaxDistanceName = "_MaxDistance";

        /// <summary>
        /// Name of the display rotation matrix in the shader.
        /// </summary>
        const string k_DisplayRotationPerFrameName = "_DisplayRotationPerFrame";

        /// <summary>
        /// The default texture aspect ratio.
        /// </summary>
        const float k_DefaultTextureAspectRadio = 1.0f;

        /// <summary>
        /// ID of the max distance  property in the shader.
        /// </summary>
        static readonly int k_MaxDistanceId = Shader.PropertyToID(k_MaxDistanceName);

        /// <summary>
        /// ID of the display rotation matrix in the shader.
        /// </summary>
        static readonly int k_DisplayRotationPerFrameId = Shader.PropertyToID(k_DisplayRotationPerFrameName);

        /// <summary>
        /// A string builder for construction of strings.
        /// </summary>
        readonly StringBuilder m_StringBuilder = new StringBuilder();

        /// <summary>
        /// The current screen orientation remembered so that we are only updating the raw image layout when it changes.
        /// </summary>
        ScreenOrientation m_CurrentScreenOrientation;

        /// <summary>
        /// The current texture aspect ratio remembered so that we can resize the raw image layout when it changes.
        /// </summary>
        float m_TextureAspectRatio = k_DefaultTextureAspectRadio;

        /// <summary>
        /// The mode indicating which texture to display.
        /// </summary>
        DisplayMode m_DisplayMode = DisplayMode.EnvironmentDepthSmooth;

        /// <summary>
        /// The display rotation matrix for the shader.
        /// </summary.
        Matrix4x4 m_DisplayRotationMatrix = Matrix4x4.identity;

#if UNITY_ANDROID
        /// <summary>
        /// A matrix to flip the Y coordinate for the Android platform.
        /// </summary>
        Matrix4x4 k_AndroidFlipYMatrix = Matrix4x4.identity;
#endif // UNITY_ANDROID

        /// <summary>
        /// Get or set the <c>AROcclusionManager</c>.
        /// </summary>
        public AROcclusionManager occlusionManager
        {
            get => m_OcclusionManager;
            set => m_OcclusionManager = value;
        }

        [SerializeField]
        [Tooltip("The AROcclusionManager which will produce depth textures.")]
        AROcclusionManager m_OcclusionManager;

        /// <summary>
        /// Get or set the <c>ARCameraManager</c>.
        /// </summary>
        public ARCameraManager cameraManager
        {
            get => m_CameraManager;
            set => m_CameraManager = value;
        }

        [SerializeField]
        [Tooltip("The ARCameraManager which will produce camera frame events.")]
        ARCameraManager m_CameraManager;

        /// <summary>
        /// The UI RawImage used to display the image on screen.
        /// </summary>
        public RawImage rawImage
        {
            get => m_RawImage;
            set => m_RawImage = value;
        }

        [SerializeField]
        RawImage m_RawImage;

        /// <summary>
        /// The UI Text used to display information about the image on screen.
        /// </summary>
        public Text imageInfo
        {
            get => m_ImageInfo;
            set => m_ImageInfo = value;
        }

        [SerializeField]
        Text m_ImageInfo;

        /// <summary>
        /// The depth material for rendering depth textures.
        /// </summary>
        public Material depthMaterial
        {
            get => m_DepthMaterial;
            set => m_DepthMaterial = value;
        }

        [SerializeField]
        Material m_DepthMaterial;

        /// <summary>
        /// The stencil material for rendering stencil textures.
        /// </summary>
        public Material stencilMaterial
        {
            get => m_StencilMaterial;
            set => m_StencilMaterial = value;
        }

        [SerializeField]
        Material m_StencilMaterial;

        /// <summary>
        /// The max distance value for the shader when showing an environment depth texture.
        /// </summary>
        public float maxEnvironmentDistance
        {
            get => m_MaxEnvironmentDistance;
            set => m_MaxEnvironmentDistance = value;
        }

        [SerializeField]
        float m_MaxEnvironmentDistance = 8.0f;

        /// <summary>
        /// The max distance value for the shader when showing an human depth texture.
        /// </summary>
        public float maxHumanDistance
        {
            get => m_MaxHumanDistance;
            set => m_MaxHumanDistance = value;
        }

        public float DepthDistance
        {
            get => depthDistance;
            set => depthDistance = value;
        }

        public float DepthDistanceCentimeters
        {
            get => depthDistance * 100f; // Convert meters to centimeters
        }

        float depthDistance = 0;

        [SerializeField]
        float m_MaxHumanDistance = 3.0f;

        void Awake()
        {
#if UNITY_ANDROID
            k_AndroidFlipYMatrix[1, 1] = -1.0f;
            k_AndroidFlipYMatrix[2, 1] = 1.0f;
#endif // UNITY_ANDROID
        }

        void OnEnable()
        {
            // Subscribe to the camera frame received event, and initialize the display rotation matrix.
            Debug.Assert(m_CameraManager != null, "no camera manager");
            m_CameraManager.frameReceived += OnCameraFrameEventReceived;
            m_DisplayRotationMatrix = Matrix4x4.identity;

            // When enabled, get the current screen orientation, and update the raw image UI.
            m_CurrentScreenOrientation = Screen.orientation;
            UpdateRawImage();

            StartCoroutine(RetrieveDepthDistance());
        }

        void OnDisable()
        {
            // Unsubscribe from the camera frame received event, and initialize the display rotation matrix.
            m_DisplayRotationMatrix = Matrix4x4.identity;
            if (m_CameraManager != null)
                m_CameraManager.frameReceived -= OnCameraFrameEventReceived;

            StopAllCoroutines();
        }

        void Update()
        {
            // If we are on a device that does supports neither human stencil, human depth, nor environment depth,
            // display a message about unsupported functionality and return.
            Debug.Assert(m_OcclusionManager != null, "no occlusion manager");

            var descriptor = m_OcclusionManager.descriptor;
            switch (m_DisplayMode)
            {
                case DisplayMode.HumanDepth:
                case DisplayMode.HumanStencil:
                    {
                        if (descriptor != null &&
                            (descriptor.humanSegmentationDepthImageSupported == Supported.Supported ||
                            descriptor.humanSegmentationStencilImageSupported == Supported.Supported))
                        {
                            break;
                        }

                        if (descriptor != null &&
                            (descriptor.humanSegmentationStencilImageSupported == Supported.Unknown ||
                             descriptor.humanSegmentationDepthImageSupported == Supported.Unknown))
                        {
                            LogText("Determining human segmentation support...");
                        }
                        else
                        {
                            LogText("Human segmentation is not supported on this device.");
                        }

                        m_RawImage.texture = null;
                        if (!Mathf.Approximately(m_TextureAspectRatio, k_DefaultTextureAspectRadio))
                        {
                            m_TextureAspectRatio = k_DefaultTextureAspectRadio;
                            UpdateRawImage();
                        }

                        return;
                    }
                case DisplayMode.EnvironmentDepthRaw:
                case DisplayMode.EnvironmentDepthSmooth:
                default:
                    {
                        if (descriptor == null || descriptor.environmentDepthImageSupported == Supported.Unsupported)
                        {
                            LogText("Environment depth is not supported on this device.");
                        }
                        else if (descriptor.environmentDepthImageSupported == Supported.Unknown)
                        {
                            LogText("Determining environment depth support...");
                        }
                        else if (descriptor.environmentDepthImageSupported == Supported.Supported)
                        {
                            m_OcclusionManager.environmentDepthTemporalSmoothingRequested = m_DisplayMode == DisplayMode.EnvironmentDepthSmooth;
                            break;
                        }

                        m_RawImage.texture = null;
                        if (!Mathf.Approximately(m_TextureAspectRatio, k_DefaultTextureAspectRadio))
                        {
                            m_TextureAspectRatio = k_DefaultTextureAspectRadio;
                            UpdateRawImage();
                        }

                        return;
                    }
            }

            // Get all of the occlusion textures.
            Texture2D humanStencil = m_OcclusionManager.humanStencilTexture;
            Texture2D humanDepth = m_OcclusionManager.humanDepthTexture;
            Texture2D envDepth = m_OcclusionManager.environmentDepthTexture;

            // Display some text information about each of the textures.
            m_StringBuilder.Clear();
            BuildTextureInfo(m_StringBuilder, "stencil", humanStencil);
            BuildTextureInfo(m_StringBuilder, "depth", humanDepth);
            BuildTextureInfo(m_StringBuilder, "env", envDepth);
            RetrieveDepthDistance();

            LogText(m_StringBuilder.ToString());

            // Decide which to display based on the current mode.
            Texture2D displayTexture;
            switch (m_DisplayMode)
            {
                case DisplayMode.HumanStencil:
                    displayTexture = humanStencil;
                    break;
                case DisplayMode.HumanDepth:
                    displayTexture = humanDepth;
                    break;
                case DisplayMode.EnvironmentDepthRaw:
                case DisplayMode.EnvironmentDepthSmooth:
                default:
                    displayTexture = envDepth;
                    break;
            }

            // Assign the texture to display to the raw image.
            Debug.Assert(m_RawImage != null, "no raw image");
            m_RawImage.texture = displayTexture;

            // Get the aspect ratio for the current texture.
            float textureAspectRatio = (displayTexture == null) ? 1.0f : ((float)displayTexture.width / (float)displayTexture.height);

            // If the raw image needs to be updated because of a device orientation change or because of a texture
            // aspect ratio difference, then update the raw image with the new values.
            if ((m_CurrentScreenOrientation != Screen.orientation)
                || !Mathf.Approximately(m_TextureAspectRatio, textureAspectRatio))
            {
                m_CurrentScreenOrientation = Screen.orientation;
                m_TextureAspectRatio = textureAspectRatio;
                UpdateRawImage();
            }
        }

        /// <summary>
        /// When the camera frame event is raised, capture the display rotation matrix.
        /// </summary>
        /// <param name="cameraFrameEventArgs">The arguments when a camera frame event is raised.</param>
        void OnCameraFrameEventReceived(ARCameraFrameEventArgs cameraFrameEventArgs)
        {
            Debug.Assert(m_RawImage != null, "no raw image");
            if (m_RawImage.material != null)
            {
                // Copy the display rotation matrix from the camera.
                Matrix4x4 cameraMatrix = cameraFrameEventArgs.displayMatrix ?? Matrix4x4.identity;

                // The camera display matrix includes scaling and offsets to fit the aspect ratio of the device. In most
                // cases, the camera display matrix should be used directly without modification when applying depth to
                // the scene because that will line up the depth image with the camera image. However, for this demo,
                // we want to show the full depth image as a picture-in-picture, so we remove these scaling and offset
                // factors while preserving the orientation.
                Vector2 affineBasisX = new Vector2(cameraMatrix[0, 0], cameraMatrix[1, 0]);
                Vector2 affineBasisY = new Vector2(cameraMatrix[0, 1], cameraMatrix[1, 1]);
                Vector2 affineTranslation = new Vector2(cameraMatrix[2, 0], cameraMatrix[2, 1]);
                affineBasisX = affineBasisX.normalized;
                affineBasisY = affineBasisY.normalized;
                m_DisplayRotationMatrix = Matrix4x4.identity;
                m_DisplayRotationMatrix[0, 0] = affineBasisX.x;
                m_DisplayRotationMatrix[0, 1] = affineBasisY.x;
                m_DisplayRotationMatrix[1, 0] = affineBasisX.y;
                m_DisplayRotationMatrix[1, 1] = affineBasisY.y;
                m_DisplayRotationMatrix[2, 0] = Mathf.Round(affineTranslation.x);
                m_DisplayRotationMatrix[2, 1] = Mathf.Round(affineTranslation.y);

                // Set the matrix to the raw image material.
                m_RawImage.material.SetMatrix(k_DisplayRotationPerFrameId, m_DisplayRotationMatrix);
            }
        }

        /// <summary>
        /// Create log information about the given texture.
        /// </summary>
        /// <param name="stringBuilder">The string builder to which to append the texture information.</param>
        /// <param name="textureName">The semantic name of the texture for logging purposes.</param>
        /// <param name="texture">The texture for which to log information.</param>
        void BuildTextureInfo(StringBuilder stringBuilder, string textureName, Texture2D texture)
        {
            stringBuilder.AppendLine($"texture : {textureName}");
            if (texture == null)
            {
                stringBuilder.AppendLine("   <null>");
            }
            else
            {
                stringBuilder.AppendLine($"   format : {texture.format}");
                stringBuilder.AppendLine($"   width  : {texture.width}");
                stringBuilder.AppendLine($"   height : {texture.height}");
                stringBuilder.AppendLine($"   mipmap : {texture.mipmapCount}");
                stringBuilder.AppendLine($"   distance (meters): {DepthDistance:F2}");
                stringBuilder.AppendLine($"   distance (centimeters): {DepthDistanceCentimeters:F1}");

                // Add color mapping info
                if (texture == m_OcclusionManager.environmentDepthTexture)
                {
                    stringBuilder.AppendLine("\n" + GetDepthColorMapping());
                }
            }
        }


        IEnumerator RetrieveDepthDistance()
        {
            while (true)
            {
                try
                {
                    if (occlusionManager != null && occlusionManager.TryAcquireEnvironmentDepthCpuImage(out var cpuImage))
                    {
                        using (cpuImage)
                        {
                            if (cpuImage.valid)
                            {
                                Assert.IsTrue(cpuImage.planeCount == 1);
                                var plane = cpuImage.GetPlane(0);
                                var dataLength = plane.data.Length;
                                var pixelStride = plane.pixelStride;
                                var rowStride = plane.rowStride;

                                var centerRowIndex = dataLength / rowStride / 2;
                                var centerPixelIndex = rowStride / pixelStride / 2;
                                var centerPixelData = plane.data.GetSubArray(centerRowIndex * rowStride + centerPixelIndex * pixelStride, pixelStride);
                                DepthDistance = convertPixelDataToDistanceInMeters(centerPixelData.ToArray(), cpuImage.format);

                                // Calculate normalized depth value (0 to 1)
                                float normalizedDepth = Mathf.Clamp01(DepthDistance / m_MaxEnvironmentDistance);

                                // Determine color range (reversed from before)
                                // string colorRange = "Unknown";
                                // if (normalizedDepth < 0.2f) colorRange = "Blue (Closest)";
                                // else if (normalizedDepth < 0.4f) colorRange = "Cyan";
                                // else if (normalizedDepth < 0.6f) colorRange = "Green";
                                // else if (normalizedDepth < 0.8f) colorRange = "Yellow";
                                // else colorRange = "Red (Farthest)";

                                // Debug.Log($"Depth: {DepthDistance:F2}m ({DepthDistanceCentimeters:F1}cm)" +
                                //         $"\nNormalized depth: {normalizedDepth:F2}" +
                                //         $"\nColor range: {colorRange}" +
                                //         $"\nPercentage of max: {(normalizedDepth * 100):F1}%");
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error retrieving depth: {e.Message}");
                }

                yield return new WaitForSeconds(0.1f);
            }
        }
        /// <summary>
        /// Log the given text to the screen if the image info UI is set. Otherwise, log the string to debug.
        /// </summary>
        /// <param name="text">The text string to log.</param>
        void LogText(string text)
        {
            if (m_ImageInfo != null)
            {
                m_ImageInfo.text = text;
            }
            else
            {
                Debug.Log(text);
            }
        }

        /// <summary>
        /// Update the raw image with the current configurations.
        /// </summary>
        void UpdateRawImage()
        {
            Debug.Assert(m_RawImage != null, "no raw image");

            // Determine the raw imge rectSize preserving the texture aspect ratio, matching the screen orientation,
            // and keeping a minimum dimension size.
            float minDimension = 480.0f;
            float maxDimension = Mathf.Round(minDimension * m_TextureAspectRatio);
            if (minDimension > maxDimension)
            {
                (minDimension, maxDimension) = (maxDimension, minDimension);
            }
            Vector2 rectSize;
            switch (m_CurrentScreenOrientation)
            {
                case ScreenOrientation.LandscapeRight:
                case ScreenOrientation.LandscapeLeft:
                    rectSize = new Vector2(maxDimension, minDimension);
                    break;
                case ScreenOrientation.PortraitUpsideDown:
                case ScreenOrientation.Portrait:
                default:
                    rectSize = new Vector2(minDimension, maxDimension);
                    break;
            }

            // Determine the raw image material and maxDistance material parameter based on the display mode.
            float maxDistance;
            Material material;
            switch (m_DisplayMode)
            {
                case DisplayMode.HumanStencil:
                    material = m_StencilMaterial;
                    maxDistance = m_MaxHumanDistance;
                    break;
                case DisplayMode.HumanDepth:
                    material = m_DepthMaterial;
                    maxDistance = m_MaxHumanDistance;
                    break;
                case DisplayMode.EnvironmentDepthRaw:
                case DisplayMode.EnvironmentDepthSmooth:
                default:
                    material = m_DepthMaterial;
                    maxDistance = m_MaxEnvironmentDistance;
                    break;
            }

            // Update the raw image dimensions and the raw image material parameters.
            m_RawImage.rectTransform.sizeDelta = rectSize;
            material.SetFloat(k_MaxDistanceId, maxDistance);
            material.SetMatrix(k_DisplayRotationPerFrameId, m_DisplayRotationMatrix);
            m_RawImage.material = material;
        }

        /// <summary>
        /// Callback when the depth mode dropdown UI has a value change.
        /// </summary>
        /// <param name="dropdown">The dropdown UI that changed.</param>
        public void OnDepthModeDropdownValueChanged(Dropdown dropdown)
        {
            // Update the display mode from the dropdown value.
            m_DisplayMode = (DisplayMode)dropdown.value;

            // Update the raw image following the mode change.
            UpdateRawImage();
        }

        public float convertPixelDataToDistanceInMeters(byte[] data, XRCpuImage.Format format)
        {
            switch (format)
            {
                case XRCpuImage.Format.DepthUint16:
                    return BitConverter.ToUInt16(data, 0) / 1000f;
                case XRCpuImage.Format.DepthFloat32:
                    return BitConverter.ToSingle(data, 0);
                default:
                    throw new Exception($"Format not supported: {format}");
            }
        }

        // Add this method to your DisplayDepthImage class to get color mapping for depth values
        private string GetDepthColorMapping()
        {
            float maxDist = m_MaxEnvironmentDistance; // 0.7m in your case
            StringBuilder mapping = new StringBuilder();
            mapping.AppendLine("Depth Color Mapping (max distance = 0.7m):");
            mapping.AppendLine("Blue   -> 0.0m to 0.14m (closest)");
            mapping.AppendLine("Cyan   -> 0.14m to 0.28m");
            mapping.AppendLine("Green  -> 0.28m to 0.42m");
            mapping.AppendLine("Yellow -> 0.42m to 0.56m");
            mapping.AppendLine("Red    -> 0.56m to 0.7m (farthest)");

            return mapping.ToString();
        }

    }
}
