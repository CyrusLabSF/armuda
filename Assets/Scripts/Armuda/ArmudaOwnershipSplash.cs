// Copyright © 2026 CyFi Network Corporation. All Rights Reserved.

using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Armuda.Runtime
{
    /// <summary>
    /// Displays Armuda's ownership card before the start/profile interface is exposed.
    /// The card is created in code so it remains present across every packaged entry scene.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ArmudaOwnershipSplash : MonoBehaviour
    {
        public const string CopyrightNotice =
            "© 2026 CyFi Network Corporation. All Rights Reserved.";

        private const float FadeInSeconds = 0.3f;
        private const float HoldSeconds = 1.8f;
        private const float FadeOutSeconds = 0.55f;
        private const string CaptureArgument = "-armudaCaptureOwnershipSplash";

        private CanvasGroup _canvasGroup;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if ((Application.isBatchMode && !CommandLineContains(CaptureArgument)) ||
                FindAnyObjectByType<ArmudaOwnershipSplash>() != null)
            {
                return;
            }

            GameObject splashObject = new GameObject("Armuda Ownership Splash");
            DontDestroyOnLoad(splashObject);
            splashObject.AddComponent<ArmudaOwnershipSplash>();
        }

        private void Awake()
        {
            BuildSplashInterface();
        }

        private IEnumerator Start()
        {
            yield return FadeTo(1f, FadeInSeconds);

            string capturePath = ReadCommandLineValue(CaptureArgument);
            if (!string.IsNullOrWhiteSpace(capturePath))
            {
                capturePath = Path.GetFullPath(capturePath);
                string captureDirectory = Path.GetDirectoryName(capturePath);
                if (!string.IsNullOrWhiteSpace(captureDirectory))
                {
                    Directory.CreateDirectory(captureDirectory);
                }

                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(capturePath);
                float timeout = Time.realtimeSinceStartup + 5f;
                while (!File.Exists(capturePath) && Time.realtimeSinceStartup < timeout)
                {
                    yield return null;
                }

                bool captured = File.Exists(capturePath);
                Debug.Log(captured
                    ? $"[Armuda Ownership Splash] Capture saved to {capturePath}."
                    : $"[Armuda Ownership Splash] Capture failed: {capturePath}.");
                Application.Quit(captured ? 0 : 3);
                yield break;
            }

            yield return new WaitForSecondsRealtime(HoldSeconds);
            yield return FadeTo(0f, FadeOutSeconds);
            Destroy(gameObject);
        }

        private void BuildSplashInterface()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;

            GameObject backgroundObject = new GameObject("Black Background", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(transform, false);
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            StretchToParent(backgroundRect);
            Image background = backgroundObject.GetComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = true;

            TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (font == null)
            {
                throw new InvalidOperationException("Armuda's packaged ownership font could not be loaded.");
            }

            CreateText(
                "Created By",
                "CREATED BY",
                font,
                28,
                new Color(0.62f, 0.66f, 0.72f, 1f),
                new Vector2(0f, 76f),
                new Vector2(1500f, 56f),
                FontStyles.Bold);
            CreateText(
                "Company",
                "CyFi Network Corporation",
                font,
                58,
                Color.white,
                Vector2.zero,
                new Vector2(1700f, 100f),
                FontStyles.Bold);
            CreateText(
                "Copyright",
                CopyrightNotice,
                font,
                22,
                new Color(0.58f, 0.62f, 0.68f, 1f),
                new Vector2(0f, -92f),
                new Vector2(1700f, 52f),
                FontStyles.Normal);
        }

        private void CreateText(
            string objectName,
            string content,
            TMP_FontAsset font,
            int fontSize,
            Color color,
            Vector2 anchoredPosition,
            Vector2 size,
            FontStyles style)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = TextAlignmentOptions.Center;
            text.color = color;
            text.raycastTarget = false;
            text.enableAutoSizing = true;
            text.fontSizeMin = 16;
            text.fontSizeMax = fontSize;
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            float startingAlpha = _canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(startingAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _canvasGroup.alpha = targetAlpha;
        }

        private static void StretchToParent(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static bool CommandLineContains(string argument)
        {
            return Array.Exists(
                Environment.GetCommandLineArgs(),
                value => string.Equals(value, argument, StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadCommandLineValue(string argument)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], argument, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[index + 1];
                }
            }

            return string.Empty;
        }
    }
}
