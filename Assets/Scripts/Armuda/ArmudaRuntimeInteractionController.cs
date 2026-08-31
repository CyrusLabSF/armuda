using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Armuda.Runtime
{
    /// <summary>
    /// Desktop and mobile interaction bootstrap for packaged Armuda builds.
    /// The cursor remains visible and unlocked. Middle-mouse drag looks around,
    /// standard clicks continue through the EventSystem, and glyph HUDs use
    /// right click (or long press on touch devices).
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class ArmudaRuntimeInteractionController : MonoBehaviour
    {
        private const float MouseLookSensitivity = 0.13f;
        private const float TouchLookSensitivity = 0.08f;
        private const float TouchDragThreshold = 12f;
        private const float TouchLongPressSeconds = 0.55f;

        private Camera _camera;
        private float _pitch;
        private float _yaw;
        private bool _mouseLooking;
        private bool _touchActive;
        private bool _touchStartedOverUi;
        private bool _touchDragged;
        private float _touchStartedAt;
        private Vector2 _touchStartPosition;
        private Vector2 _lastTouchPosition;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindAnyObjectByType<ArmudaRuntimeInteractionController>() != null)
            {
                return;
            }

            GameObject controllerObject = new GameObject("Armuda Runtime Interaction");
            DontDestroyOnLoad(controllerObject);
            controllerObject.AddComponent<ArmudaRuntimeInteractionController>();
        }

        private void Awake()
        {
            KeepCursorFree();
            SceneManager.sceneLoaded += HandleSceneLoaded;
            ConfigureCurrentScene();
        }

        private IEnumerator Start()
        {
            // Allow scene objects and late-created canvases to finish their first frame.
            yield return null;
            ConfigureCurrentScene();

            int responsiveCanvasCount = CountResponsiveCanvases(out bool allCanvasesResponsive);
            bool runtimeReady = _camera != null && Cursor.lockState == CursorLockMode.None &&
                                Cursor.visible && allCanvasesResponsive;

            Debug.Log(
                $"[Armuda Runtime] Ready={runtimeReady}; Camera={(_camera != null ? _camera.name : "missing")}; " +
                $"ResponsiveCanvases={responsiveCanvasCount}; Cursor={Cursor.lockState}/{Cursor.visible}.");

            if (!CommandLineContains("-armudaSmokeTest"))
            {
                yield break;
            }

            yield return new WaitForSecondsRealtime(1f);
            Debug.Log(runtimeReady
                ? "[Armuda Smoke Test] PASSED"
                : "[Armuda Smoke Test] FAILED");
            Application.Quit(runtimeReady ? 0 : 2);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                KeepCursorFree();
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureCurrentScene();
        }

        private void ConfigureCurrentScene()
        {
            _camera = Camera.main;
            SynchronizeLookAngles();
            ConfigureResponsiveCanvases();
            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
        }

        private void Update()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
                SynchronizeLookAngles();
            }

            HandleMouse();
            HandleTouch();

            if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
            {
                ConfigureResponsiveCanvases();
                _lastScreenWidth = Screen.width;
                _lastScreenHeight = Screen.height;
            }
        }

        private void LateUpdate()
        {
            // No interaction mode is allowed to strand the packaged app with a
            // hidden or captured cursor.
            KeepCursorFree();
        }

        private void HandleMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _mouseLooking = true;
                SynchronizeLookAngles();
            }

            if (_mouseLooking && mouse.middleButton.isPressed)
            {
                ApplyLook(mouse.delta.ReadValue(), MouseLookSensitivity);
            }

            if (mouse.middleButton.wasReleasedThisFrame)
            {
                _mouseLooking = false;
            }

            if (mouse.leftButton.wasReleasedThisFrame && !PointerIsOverUi())
            {
                DispatchWorldPointer(mouse.position.ReadValue(), PointerEventData.InputButton.Left);
            }

            if (mouse.rightButton.wasReleasedThisFrame && !PointerIsOverUi())
            {
                DispatchWorldPointer(mouse.position.ReadValue(), PointerEventData.InputButton.Right);
            }
        }

        private void HandleTouch()
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return;
            }

            var touch = touchscreen.primaryTouch;
            Vector2 position = touch.position.ReadValue();

            if (touch.press.wasPressedThisFrame)
            {
                _touchActive = true;
                _touchDragged = false;
                _touchStartedAt = Time.unscaledTime;
                _touchStartPosition = position;
                _lastTouchPosition = position;
                _touchStartedOverUi = PointerIsOverUi(touch.touchId.ReadValue());
            }

            if (_touchActive && touch.press.isPressed && !_touchStartedOverUi)
            {
                Vector2 totalDelta = position - _touchStartPosition;
                if (!_touchDragged && totalDelta.sqrMagnitude >= TouchDragThreshold * TouchDragThreshold)
                {
                    _touchDragged = true;
                    SynchronizeLookAngles();
                }

                if (_touchDragged)
                {
                    ApplyLook(position - _lastTouchPosition, TouchLookSensitivity);
                }

                _lastTouchPosition = position;
            }

            if (_touchActive && touch.press.wasReleasedThisFrame)
            {
                if (!_touchStartedOverUi && !_touchDragged)
                {
                    float heldSeconds = Time.unscaledTime - _touchStartedAt;
                    PointerEventData.InputButton button = heldSeconds >= TouchLongPressSeconds
                        ? PointerEventData.InputButton.Right
                        : PointerEventData.InputButton.Left;
                    DispatchWorldPointer(position, button);
                }

                _touchActive = false;
            }
        }

        private void ApplyLook(Vector2 delta, float sensitivity)
        {
            if (_camera == null || delta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            _yaw += delta.x * sensitivity;
            _pitch = Mathf.Clamp(_pitch - delta.y * sensitivity, -85f, 85f);
            _camera.transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private void SynchronizeLookAngles()
        {
            if (_camera == null)
            {
                return;
            }

            Vector3 currentAngles = _camera.transform.eulerAngles;
            _pitch = NormalizeSignedAngle(currentAngles.x);
            _yaw = currentAngles.y;
        }

        private void DispatchWorldPointer(Vector2 screenPosition, PointerEventData.InputButton button)
        {
            if (_camera == null)
            {
                return;
            }

            Ray ray = _camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, _camera.farClipPlane, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Collide))
            {
                return;
            }

            ArmudaGlyphInteraction glyph = hit.collider.GetComponentInParent<ArmudaGlyphInteraction>();
            if (glyph != null)
            {
                if (button == PointerEventData.InputButton.Right)
                {
                    glyph.OpenHud();
                }
                else
                {
                    glyph.Select();
                }

                return;
            }

            // Preserve any existing IPointerClickHandler-based world interaction.
            if (EventSystem.current == null)
            {
                return;
            }

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                button = button,
                position = screenPosition,
                pointerPressRaycast = new RaycastResult
                {
                    gameObject = hit.collider.gameObject,
                    worldPosition = hit.point,
                    worldNormal = hit.normal,
                    distance = hit.distance
                }
            };

            ExecuteEvents.ExecuteHierarchy(hit.collider.gameObject, eventData, ExecuteEvents.pointerClickHandler);
        }

        private static bool PointerIsOverUi(int pointerId = -1)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }

        private static void ConfigureResponsiveCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null)
                {
                    scaler = canvas.gameObject.AddComponent<CanvasScaler>();
                }

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
            }

            Canvas.ForceUpdateCanvases();
        }

        private static int CountResponsiveCanvases(out bool allCanvasesResponsive)
        {
            int count = 0;
            allCanvasesResponsive = true;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            foreach (Canvas canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace)
                {
                    continue;
                }

                count++;
                CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
                if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    allCanvasesResponsive = false;
                }
            }

            return count;
        }

        private static bool CommandLineContains(string expectedArgument)
        {
            foreach (string argument in Environment.GetCommandLineArgs())
            {
                if (string.Equals(argument, expectedArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static float NormalizeSignedAngle(float angle)
        {
            return angle > 180f ? angle - 360f : angle;
        }

        private static void KeepCursorFree()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
