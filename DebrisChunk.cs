using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using UnityEngine;
using UnityEngine.Rendering;

namespace Main
{
    [RegisterTypeInIl2Cpp]
    public class DebrisChunk : MonoBehaviour
    {
        // Oldest-first registry so we can recycle under the cap.
        private static readonly LinkedList<DebrisChunk> Live = new LinkedList<DebrisChunk>();
        private LinkedListNode<DebrisChunk> _node;

        private float _age;
        private float _lifetime;
        private bool _dying;

        // --- shrink fallback fields (pre-existing) ---
        private float _initialScaleMax = 1f;

        // --- alpha-fade fields ---
        private const float FadeDuration = 0.6f;
        private float _fadeAge;
        private bool _useAlphaFade;
        private Material _fadeMat;
        private string _fadeProp;
        private Color _startColor;

        public DebrisChunk(IntPtr ptr) : base(ptr) { }

        public void Init(float lifetime)
        {
            _lifetime = lifetime;
            _node = Live.AddLast(this);
            // The Chunk Limit is a hard cap that applies in EVERY mode (including Chaos): when over
            // the limit the oldest chunk is removed immediately. In Chaos chunks still never despawn
            // on a timer — they're only evicted to stay under the cap.
            EnforceCap();
        }

        private static void EnforceCap()
        {
            int cap = Preferences.MaxConcurrentDebris.Value;
            // Drop nodes whose chunk was destroyed externally (e.g. scene/map change);
            // a dead front node would make the loop break early and defeat the cap.
            while (Live.First != null && Live.First.Value == null) Live.RemoveFirst();
            while (Live.Count > cap)
            {
                var oldest = Live.First?.Value;
                if (oldest == null) { Live.RemoveFirst(); continue; }
                oldest.Kill();
            }
        }

        private void Update()
        {
            // Persist mode: no timed despawn at all — chunks live until the scene changes.
            if (Preferences.PersistUntilSceneChange.Value && !_dying) return;

            _age += Time.deltaTime;
            if (!_dying && _age >= _lifetime) BeginFade();

            if (_dying)
            {
                _fadeAge += Time.deltaTime;

                if (_useAlphaFade)
                {
                    float t = _fadeAge / FadeDuration;
                    Color c = _startColor;
                    c.a = Mathf.Lerp(_startColor.a, 0f, t);
                    _fadeMat.SetColor(_fadeProp, c);
                    if (t >= 1f) Kill();
                }
                else
                {
                    // Shrink fallback: scale down over ~0.5s then destroy.
                    Vector3 s = transform.localScale - Vector3.one * (Time.deltaTime / 0.5f) * _initialScaleMax;
                    if (Mathf.Max(s.x, Mathf.Max(s.y, s.z)) <= 0f) { Kill(); return; }
                    transform.localScale = s;
                }
            }
        }

        private void BeginFade()
        {
            _dying = true;
            _fadeAge = 0f;

            // --- attempt alpha-fade setup ---
            var r = GetComponent<MeshRenderer>();
            if (r != null)
            {
                // Instance the material so this chunk owns its own copy while fading.
                Material mat = r.material;

                string prop = null;
                if (mat.HasProperty("_BaseColor")) prop = "_BaseColor";
                else if (mat.HasProperty("_Color"))  prop = "_Color";

                if (prop != null)
                {
                    // Best-effort: set up transparent blending. Wrap in try/catch because
                    // some IL2CPP-proxied shaders throw on unknown property writes.
                    try
                    {
                        mat.SetFloat("_Surface", 1f);
                        mat.SetFloat("_Mode",    3f);
                        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                        mat.SetInt("_ZWrite",   0);
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        mat.EnableKeyword("_ALPHABLEND_ON");
                        mat.DisableKeyword("_ALPHATEST_ON");
                        mat.renderQueue = (int)RenderQueue.Transparent;
                    }
                    catch (Exception ex)
                    {
                        Main.Logger.Warning($"Alpha-fade shader setup failed, falling back to shrink: {ex.Message}");
                        goto useShrink;
                    }

                    _useAlphaFade = true;
                    _fadeMat      = mat;
                    _fadeProp     = prop;
                    _startColor   = mat.GetColor(prop);
                    return;
                }
            }

            useShrink:
            // No renderer, no usable color property, or shader setup threw — use shrink.
            _useAlphaFade = false;
            Vector3 s = transform.localScale;
            _initialScaleMax = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
        }

        public void Kill()
        {
            if (_node != null) { Live.Remove(_node); _node = null; }
            if (this != null && gameObject != null) UnityEngine.Object.Destroy(gameObject);
        }

        // Unity destroys chunks directly on scene/map change WITHOUT calling Kill(),
        // so remove our node here too — otherwise the static registry leaks dead nodes
        // and the MaxConcurrentDebris cap stops working after the first scene change.
        private void OnDestroy()
        {
            if (_node != null) { Live.Remove(_node); _node = null; }
        }
    }
}
