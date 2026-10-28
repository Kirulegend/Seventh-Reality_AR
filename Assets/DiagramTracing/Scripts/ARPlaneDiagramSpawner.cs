using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace RutherfordSim.Bio.Tracing
{
    public class ARPlaneDiagramSpawner : MonoBehaviour
    {
        [SerializeField] GameObject _tracingBoardPrefab, _placementInstructionUI;
        [SerializeField] ARRaycastManager _raycastManager;
        [SerializeField] ARPlaneManager _planeManager;
        [SerializeField] Camera _arCamera;
        [SerializeField] bool _spawnOnlyOnce = true, _hidePlanesAfterSpawn;
        [SerializeField] float _heightOffset = 0.005f, _yawRotationOffset = 0f;

        public UnityEvent<ARTracingBoard> onBoardSpawned;

        GameObject _spawnedBoardInstance;
        static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public GameObject SpawnedBoard => _spawnedBoardInstance;
        public bool HasSpawned => _spawnedBoardInstance != null;

        void Awake() => EnsureComponents();

        void EnsureComponents()
        {
            if ( !_arCamera ) _arCamera = Camera.main;
            if ( !_raycastManager ) _raycastManager = FindFirstObjectByType<ARRaycastManager>();
            if ( !_planeManager ) _planeManager = FindFirstObjectByType<ARPlaneManager>();
        }

        IEnumerator Start()
        {
            EnsureComponents();
            while ( true )
            {
                if ( !_spawnOnlyOnce || !_spawnedBoardInstance )
                {
                    if ( TryGetTapPosition(out var screenPos) && !IsPointerOverUI(screenPos) && _raycastManager )
                    {
                        if ( _raycastManager.Raycast(screenPos, s_Hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds) )
                        {
                            var hit = s_Hits[0];
                            SpawnBoardOnPlane(hit.pose.position, hit.pose.rotation, hit.pose.up);
                        }
                    }
                }
                yield return null;
            }
        }

        public void SpawnBoardOnPlane(Vector3 hitPosition, Quaternion hitRotation, Vector3 planeUp)
        {
            if ( !_tracingBoardPrefab ) return;
            if ( _spawnedBoardInstance ) { Destroy(_spawnedBoardInstance); _spawnedBoardInstance = null; }

            Vector3 spawnPosition = hitPosition + (planeUp * _heightOffset);
            Quaternion spawnRotation = hitRotation;
            if ( _arCamera )
            {
                Vector3 awayFromCamera = spawnPosition - _arCamera.transform.position;
                Vector3 projectedForward = Vector3.ProjectOnPlane(awayFromCamera, planeUp);
                if ( projectedForward.sqrMagnitude > 0.0001f ) spawnRotation = Quaternion.LookRotation(projectedForward.normalized, planeUp);
            }

            if ( Mathf.Abs(_yawRotationOffset) > 0.001f ) spawnRotation *= Quaternion.AngleAxis(_yawRotationOffset, planeUp);

            _spawnedBoardInstance = Instantiate(_tracingBoardPrefab, spawnPosition, spawnRotation);

            var board = _spawnedBoardInstance.GetComponent<ARTracingBoard>();
            if ( board )
            {
                board.EnsureWorldCamera();
                onBoardSpawned?.Invoke(board);
            }

            var book = _spawnedBoardInstance.GetComponent<ARTracingBook>();
            if ( book ) book.EnsureWorldCamera();

            if ( _placementInstructionUI ) _placementInstructionUI.SetActive(false);
            if ( _hidePlanesAfterSpawn && _planeManager ) SetPlaneVisualsEnabled(false);
        }

        public void Reposition()
        {
            if ( _spawnedBoardInstance ) { Destroy(_spawnedBoardInstance); _spawnedBoardInstance = null; }
            if ( _placementInstructionUI ) _placementInstructionUI.SetActive(true);
            if ( _hidePlanesAfterSpawn && _planeManager ) SetPlaneVisualsEnabled(true);
        }

        void SetPlaneVisualsEnabled(bool enabled)
        {
            if ( !_planeManager ) return;
            foreach ( var plane in _planeManager.trackables )
                if ( plane ) plane.gameObject.SetActive(enabled);
        }

        bool TryGetTapPosition(out Vector2 touchPosition)
        {
            touchPosition = Vector2.zero;
            if ( Touchscreen.current != null )
            {
                var touches = Touchscreen.current.touches;
                if ( touches.Count > 0 && touches[0].phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began )
                {
                    touchPosition = touches[0].position.ReadValue();
                    return true;
                }
            }

            if ( Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame )
            {
                touchPosition = Mouse.current.position.ReadValue();
                return true;
            }

            return false;
        }

        bool IsPointerOverUI(Vector2 screenPosition)
        {
            if ( !EventSystem.current ) return false;
            var eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach ( var r in results )
            {
                if ( r.gameObject )
                {
                    var canvas = r.gameObject.GetComponentInParent<Canvas>();
                    if ( canvas && canvas.renderMode != RenderMode.WorldSpace ) return true;
                }
            }
            return false;
        }
    }
}
