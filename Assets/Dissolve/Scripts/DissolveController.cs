using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DissolveEffect
{
    [DisallowMultipleComponent]
    public class DissolveController : MonoBehaviour
    {
        public static event Action<DissolveController, bool> OnAnyDissolveStateChanged;
        public event Action<bool> OnDissolveStateChanged;

        [SerializeField] float _duration = 2.0f, _initialAmount = 0.0f, _uiFadeSpeedMultiplier = 3.0f;
        [SerializeField] AnimationCurve _dissolveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] bool _dissolveOnStart, _startFullyDissolved, _disableCollidersWhenDissolved, _includeInactiveChildren = true;
        [SerializeField] bool _dissolveUI = true, _disableCanvasesWhenDissolved = true;
        [SerializeField] Canvas[] _excludedCanvases;
        [SerializeField] CanvasGroup[] _excludedCanvasGroups;
        [SerializeField] Renderer[] _excludedRenderers;
        [SerializeField] [ColorUsage(true, true)] Color _customEdgeColor = new Color(0f, 0.8f, 1f, 1f), _customCoreColor = new Color(2f, 2.5f, 3f, 1f);
        [SerializeField] Color _customAshColor = new Color(0.04f, 0.06f, 0.09f, 1f);
        [SerializeField] bool _overrideColorsOnStart;

        Renderer[] _cachedRenderers;
        Collider[] _cachedColliders;
        Canvas[] _cachedCanvases;
        CanvasGroup[] _cachedCanvasGroups;
        Graphic[] _cachedDissolveGraphics;
        Material[] _clonedGraphicMaterials;
        MaterialPropertyBlock _propertyBlock;
        Coroutine _activeRoutine;
        float _currentAmount;
        bool _isDissolvingOut;

        static readonly int _dissolvePropId = Shader.PropertyToID("_DissolveAmount");
        static readonly int _edgeColorPropId = Shader.PropertyToID("_EdgeColor");
        static readonly int _coreColorPropId = Shader.PropertyToID("_CoreColor");
        static readonly int _ashColorPropId = Shader.PropertyToID("_AshColor");

        public float CurrentAmount => _currentAmount;
        public bool IsDissolved => _currentAmount >= 0.999f;
        public bool IsVisible => _currentAmount <= 0.001f;

        void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            CollectRenderers();
            float startVal = _startFullyDissolved ? 1.0f : Mathf.Clamp01(_initialAmount);
            _currentAmount = startVal;
            ApplyDissolveToRenderers(startVal);
        }

        IEnumerator Start()
        {
            if ( _startFullyDissolved )
            {
                _currentAmount = 1.0f;
                ApplyDissolveToRenderers(1.0f);
                UpdateColliders(false);
                EnableCanvases(false);
            }
            else
            {
                _currentAmount = Mathf.Clamp01(_initialAmount);
                ApplyDissolveToRenderers(_currentAmount);
            }

            if ( _overrideColorsOnStart )
            {
                SetEdgeColors(_customEdgeColor, _customCoreColor);
                SetAshColor(_customAshColor);
            }

            if ( _dissolveOnStart )
            {
                yield return null;
                if ( _startFullyDissolved ) DissolveIn();
                else DissolveOut();
            }
        }

        void OnDestroy()
        {
            if ( !Application.isPlaying || _clonedGraphicMaterials == null ) return;
            foreach ( var m in _clonedGraphicMaterials )
                if ( m ) Destroy(m);
        }

        public void CollectRenderers()
        {
            var allRenderers = GetComponentsInChildren<Renderer>(_includeInactiveChildren);
            var validRenderers = new List<Renderer>();
            if ( allRenderers != null )
            {
                foreach ( var r in allRenderers )
                    if ( r && !IsExcluded(r) ) validRenderers.Add(r);
            }
            _cachedRenderers = validRenderers.ToArray();

            var allColliders = GetComponentsInChildren<Collider>(_includeInactiveChildren);
            var validColliders = new List<Collider>();
            if ( allColliders != null )
            {
                foreach ( var col in allColliders )
                    if ( col && !IsExcluded(col) ) validColliders.Add(col);
            }
            _cachedColliders = validColliders.ToArray();

            if ( _dissolveUI )
            {
                if ( Application.isPlaying && _clonedGraphicMaterials != null )
                {
                    foreach ( var m in _clonedGraphicMaterials )
                        if ( m ) Destroy(m);
                }

                var allCanvases = GetComponentsInChildren<Canvas>(_includeInactiveChildren);
                var validCanvases = new List<Canvas>();
                var groupList = new List<CanvasGroup>();
                if ( allCanvases != null )
                {
                    foreach ( var c in allCanvases )
                    {
                        if ( !c || IsExcluded(c) )
                        {
                            if ( c )
                            {
                                var cg = c.GetComponent<CanvasGroup>();
                                if ( cg )
                                {
                                    cg.alpha = 1.0f;
                                    cg.blocksRaycasts = true;
                                    cg.interactable = true;
                                }
                                c.enabled = true;
                            }
                            continue;
                        }
                        validCanvases.Add(c);
                        var g = c.GetComponent<CanvasGroup>();
                        if ( !g ) g = c.gameObject.AddComponent<CanvasGroup>();
                        if ( !groupList.Contains(g) ) groupList.Add(g);
                    }
                }
                _cachedCanvases = validCanvases.ToArray();

                var allGroups = GetComponentsInChildren<CanvasGroup>(_includeInactiveChildren);
                if ( allGroups != null )
                {
                    foreach ( var g in allGroups )
                    {
                        if ( !g || IsExcluded(g) )
                        {
                            if ( g )
                            {
                                g.alpha = 1.0f;
                                g.blocksRaycasts = true;
                                g.interactable = true;
                            }
                            continue;
                        }
                        if ( !groupList.Contains(g) ) groupList.Add(g);
                    }
                }
                _cachedCanvasGroups = groupList.ToArray();

                var graphics = GetComponentsInChildren<Graphic>(_includeInactiveChildren);
                var dissolveGraphics = new List<Graphic>();
                var clonedMats = new List<Material>();
                if ( graphics != null )
                {
                    foreach ( var g in graphics )
                    {
                        if ( !g || IsExcluded(g) || !g.material || !g.material.HasProperty(_dissolvePropId) ) continue;
                        var mat = Application.isPlaying ? Instantiate(g.material) : g.material;
                        if ( Application.isPlaying ) g.material = mat;
                        dissolveGraphics.Add(g);
                        clonedMats.Add(mat);
                    }
                }
                _cachedDissolveGraphics = dissolveGraphics.ToArray();
                _clonedGraphicMaterials = clonedMats.ToArray();
            }
        }

        bool IsExcluded(Component comp)
        {
            if ( !comp ) return true;
            if ( comp.GetComponentInParent<DissolveIgnore>() != null ) return true;
            if ( _excludedCanvases != null )
            {
                for ( int i = 0; i < _excludedCanvases.Length; i++ )
                {
                    if ( !_excludedCanvases[i] ) continue;
                    if ( comp.transform == _excludedCanvases[i].transform || comp.transform.IsChildOf(_excludedCanvases[i].transform) ) return true;
                }
            }
            if ( _excludedCanvasGroups != null )
            {
                for ( int i = 0; i < _excludedCanvasGroups.Length; i++ )
                {
                    if ( !_excludedCanvasGroups[i] ) continue;
                    if ( comp.transform == _excludedCanvasGroups[i].transform || comp.transform.IsChildOf(_excludedCanvasGroups[i].transform) ) return true;
                }
            }
            if ( _excludedRenderers != null )
            {
                for ( int i = 0; i < _excludedRenderers.Length; i++ )
                {
                    if ( !_excludedRenderers[i] ) continue;
                    if ( comp.transform == _excludedRenderers[i].transform || comp.transform.IsChildOf(_excludedRenderers[i].transform) ) return true;
                }
            }
            return false;
        }

        public void DissolveOut() => StartDissolveRoutine(1.0f);

        public void DissolveIn() => StartDissolveRoutine(0.0f);

        public void ToggleDissolve()
        {
            if ( _isDissolvingOut || _currentAmount >= 0.5f ) DissolveIn();
            else DissolveOut();
        }

        public void SetDissolve(float targetAmount)
        {
            if ( _activeRoutine != null )
            {
                StopCoroutine(_activeRoutine);
                _activeRoutine = null;
            }

            _currentAmount = Mathf.Clamp01(targetAmount);
            if ( _currentAmount < 0.999f )
            {
                UpdateColliders(true);
                EnableCanvases(true);
            }
            ApplyDissolveToRenderers(_currentAmount);
            if ( _currentAmount >= 0.999f )
            {
                UpdateColliders(false);
                EnableCanvases(false);
            }
        }

        public void SetEdgeColors(Color edgeColor, Color coreColor)
        {
            if ( _cachedRenderers != null && _propertyBlock != null )
            {
                foreach ( var r in _cachedRenderers )
                {
                    if ( !r ) continue;
                    r.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor(_edgeColorPropId, edgeColor);
                    _propertyBlock.SetColor(_coreColorPropId, coreColor);
                    r.SetPropertyBlock(_propertyBlock);
                }
            }

            if ( _clonedGraphicMaterials != null )
            {
                foreach ( var m in _clonedGraphicMaterials )
                {
                    if ( !m ) continue;
                    if ( m.HasProperty(_edgeColorPropId) ) m.SetColor(_edgeColorPropId, edgeColor);
                    if ( m.HasProperty(_coreColorPropId) ) m.SetColor(_coreColorPropId, coreColor);
                }
            }
        }

        public void SetAshColor(Color ashColor)
        {
            if ( _cachedRenderers != null && _propertyBlock != null )
            {
                foreach ( var r in _cachedRenderers )
                {
                    if ( !r ) continue;
                    r.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor(_ashColorPropId, ashColor);
                    r.SetPropertyBlock(_propertyBlock);
                }
            }

            if ( _clonedGraphicMaterials != null )
            {
                foreach ( var m in _clonedGraphicMaterials )
                {
                    if ( !m ) continue;
                    if ( m.HasProperty(_ashColorPropId) ) m.SetColor(_ashColorPropId, ashColor);
                }
            }
        }

        void StartDissolveRoutine(float target)
        {
            if ( _activeRoutine != null ) StopCoroutine(_activeRoutine);
            _activeRoutine = StartCoroutine(AnimateDissolve(target));
        }

        IEnumerator AnimateDissolve(float target)
        {
            _isDissolvingOut = target > _currentAmount;
            if ( !_isDissolvingOut )
            {
                UpdateColliders(true);
                EnableCanvases(true);
            }

            float startValue = _currentAmount;
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, _duration);

            while ( elapsed < duration )
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float curveValue = _dissolveCurve != null ? _dissolveCurve.Evaluate(t) : t;
                _currentAmount = Mathf.Lerp(startValue, target, curveValue);
                ApplyDissolveToRenderers(_currentAmount);
                yield return null;
            }

            _currentAmount = target;
            ApplyDissolveToRenderers(_currentAmount);

            if ( _isDissolvingOut )
            {
                UpdateColliders(false);
                EnableCanvases(false);
            }

            _activeRoutine = null;
            bool isFullyDissolved = _currentAmount >= 0.999f;
            OnDissolveStateChanged?.Invoke(!isFullyDissolved);
            OnAnyDissolveStateChanged?.Invoke(this, !isFullyDissolved);
        }

        void ApplyDissolveToRenderers(float amount)
        {
            if ( _cachedRenderers != null && _propertyBlock != null )
            {
                foreach ( var r in _cachedRenderers )
                {
                    if ( !r ) continue;
                    r.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetFloat(_dissolvePropId, amount);
                    r.SetPropertyBlock(_propertyBlock);
                }
            }

            if ( _dissolveUI )
            {
                float uiProgress = Mathf.Clamp01(amount * _uiFadeSpeedMultiplier);
                if ( _cachedCanvasGroups != null )
                {
                    float uiAlpha = Mathf.Clamp01(1.0f - uiProgress);
                    bool interactable = uiProgress < 0.05f;
                    foreach ( var cg in _cachedCanvasGroups )
                    {
                        if ( !cg ) continue;
                        cg.alpha = uiAlpha;
                        cg.blocksRaycasts = interactable;
                        cg.interactable = interactable;
                    }
                }

                if ( _clonedGraphicMaterials != null )
                {
                    foreach ( var m in _clonedGraphicMaterials )
                    {
                        if ( !m ) continue;
                        m.SetFloat(_dissolvePropId, uiProgress);
                    }
                }
            }
        }

        void EnableCanvases(bool state)
        {
            if ( !_dissolveUI || !_disableCanvasesWhenDissolved || _cachedCanvases == null ) return;
            foreach ( var c in _cachedCanvases )
                if ( c ) c.enabled = state;
        }

        void UpdateColliders(bool state)
        {
            if ( !_disableCollidersWhenDissolved || _cachedColliders == null ) return;
            foreach ( var c in _cachedColliders )
                if ( c ) c.enabled = state;
        }
    }
}
