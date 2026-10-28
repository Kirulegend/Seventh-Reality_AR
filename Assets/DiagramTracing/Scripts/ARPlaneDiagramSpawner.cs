using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RutherfordSim.Bio.Tracing
{
    /// <summary>
    /// Spawns the modular Diagram Tracing Board onto a detected real-world AR Plane.
    /// Once placed, the board stays rigidly locked in World Space, so moving the mobile device
    /// does not move the board with the camera.
    /// </summary>
    public class ARPlaneDiagramSpawner : MonoBehaviour
    {
        [Header("Prefab to Spawn")]
        [Tooltip("The modular ARTracingBoard prefab to place on the plane.")]
        [SerializeField] private GameObject _tracingBoardPrefab;

        [Header("AR Foundation References")]
        [SerializeField] private ARRaycastManager _raycastManager;
        [SerializeField] private ARPlaneManager _planeManager;
        [SerializeField] private Camera _arCamera;

        [Header("Placement Settings")]
        [Tooltip("Whether to allow only one board instance placed in the scene.")]
        [SerializeField] private bool _spawnOnlyOnce = true;

        [Tooltip("Slight vertical offset above the detected plane to prevent z-fighting with the plane mesh.")]
        [SerializeField] private float _heightOffset = 0.005f;

        [Tooltip("Whether to hide detected plane meshes after the board is placed for a clean view.")]
        [SerializeField] private bool _hidePlanesAfterSpawn = false;

        [Header("Placement Orientation")]
        [Tooltip("Additional rotation offset around the plane normal in degrees (0 = bottom of board closest to player).")]
        [SerializeField] private float _yawRotationOffset = 0f;

        [Header("UI Instructions (Optional)")]
        [SerializeField] private GameObject _placementInstructionUI;

        [Header("Events")]
        public UnityEvent<ARTracingBoard> onBoardSpawned;

        private GameObject _spawnedBoardInstance;
        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public GameObject SpawnedBoard => _spawnedBoardInstance;
        public bool HasSpawned => _spawnedBoardInstance != null;

        private void Awake()
        {
            if (_arCamera == null)
                _arCamera = Camera.main;

            if (_raycastManager == null)
                _raycastManager = FindFirstObjectByType<ARRaycastManager>();

            if (_planeManager == null)
                _planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        private void Update()
        {
            // If already spawned and one-instance lock is enabled, do not handle placement input
            // (this ensures user finger tracing strokes on the board are not intercepted by the spawner)
            if (_spawnOnlyOnce && _spawnedBoardInstance != null)
                return;

            Vector2 screenPos;
            if (!TryGetTapPosition(out screenPos))
                return;

            // Ignore taps over UI elements (e.g. coaching/menus/HUD)
            if (IsPointerOverUI(screenPos))
                return;

            // Raycast against real-world AR planes
            if (_raycastManager == null)
            {
                Debug.LogWarning("[ARPlaneDiagramSpawner] ARRaycastManager not found in scene.", this);
                return;
            }

            if (_raycastManager.Raycast(screenPos, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
            {
                var hit = s_Hits[0];
                SpawnBoardOnPlane(hit.pose.position, hit.pose.rotation, hit.pose.up);
            }
        }

        /// <summary>
        /// Spawns the board anchored firmly in 3D World Space at the given plane position and orientation.
        /// </summary>
        public void SpawnBoardOnPlane(Vector3 hitPosition, Quaternion hitRotation, Vector3 planeUp)
        {
            if (_tracingBoardPrefab == null)
            {
                Debug.LogError("[ARPlaneDiagramSpawner] Tracing Board Prefab is not assigned!", this);
                return;
            }

            // If an instance already exists and we are repositioning, destroy the previous one
            if (_spawnedBoardInstance != null)
            {
                Destroy(_spawnedBoardInstance);
                _spawnedBoardInstance = null;
            }

            Vector3 spawnPosition = hitPosition + (planeUp * _heightOffset);

            // Orient the board so that it rests flat on the plane with:
            // - Bottom of the board (toolbar/progress) facing towards the player's side
            // - Top of the board (title/header) pointing away from the player into the scene
            Quaternion spawnRotation = hitRotation;
            if (_arCamera != null)
            {
                Vector3 awayFromCamera = spawnPosition - _arCamera.transform.position;
                Vector3 projectedForward = Vector3.ProjectOnPlane(awayFromCamera, planeUp);
                if (projectedForward.sqrMagnitude > 0.0001f)
                {
                    spawnRotation = Quaternion.LookRotation(projectedForward.normalized, planeUp);
                }
            }

            if (Mathf.Abs(_yawRotationOffset) > 0.001f)
            {
                spawnRotation *= Quaternion.AngleAxis(_yawRotationOffset, planeUp);
            }

            // Instantiate in WORLD SPACE (do not parent to camera or origin)
            _spawnedBoardInstance = Instantiate(_tracingBoardPrefab, spawnPosition, spawnRotation);

            // Ensure the board's WorldSpace Canvas is bound to the AR camera
            var board = _spawnedBoardInstance.GetComponent<ARTracingBoard>();
            if (board != null)
            {
                board.EnsureWorldCamera();
                onBoardSpawned?.Invoke(board);
            }

            var book = _spawnedBoardInstance.GetComponent<ARTracingBook>();
            if (book != null)
            {
                book.EnsureWorldCamera();
            }

            // Hide placement instruction UI once placed
            if (_placementInstructionUI != null)
                _placementInstructionUI.SetActive(false);

            // Optionally disable plane visualization after placement
            if (_hidePlanesAfterSpawn && _planeManager != null)
            {
                SetPlaneVisualsEnabled(false);
            }

            Debug.Log($"[ARPlaneDiagramSpawner] Diagram board spawned at {spawnPosition}. Locked in World Space.");
        }

        /// <summary>
        /// Clears the current board and allows placing it again on another plane.
        /// </summary>
        public void Reposition()
        {
            if (_spawnedBoardInstance != null)
            {
                Destroy(_spawnedBoardInstance);
                _spawnedBoardInstance = null;
            }

            if (_placementInstructionUI != null)
                _placementInstructionUI.SetActive(true);

            if (_hidePlanesAfterSpawn && _planeManager != null)
            {
                SetPlaneVisualsEnabled(true);
            }
        }

        private void SetPlaneVisualsEnabled(bool enabled)
        {
            if (_planeManager == null) return;
            foreach (var plane in _planeManager.trackables)
            {
                plane.gameObject.SetActive(enabled);
            }
        }

        private bool TryGetTapPosition(out Vector2 touchPosition)
        {
            touchPosition = Vector2.zero;

            // Touchscreen support (Mobile device)
            if (Touchscreen.current != null)
            {
                var touches = Touchscreen.current.touches;
                if (touches.Count > 0)
                {
                    var touch = touches[0];
                    if (touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began)
                    {
                        touchPosition = touch.position.ReadValue();
                        return true;
                    }
                }
            }

            // Mouse / Editor simulation fallback
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                touchPosition = Mouse.current.position.ReadValue();
                return true;
            }

            return false;
        }

        private bool IsPointerOverUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            // If raycast hit a Screen-Space Canvas UI element, it's UI.
            // Ignore World-Space hits so touching a world object/plane does not count as UI block.
            foreach (var r in results)
            {
                if (r.gameObject != null)
                {
                    var canvas = r.gameObject.GetComponentInParent<Canvas>();
                    if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
                        return true;
                }
            }

            return false;
        }
    }
}
