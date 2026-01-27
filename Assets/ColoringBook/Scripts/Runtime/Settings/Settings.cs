using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Linq;
using UnityEditor;

namespace Felina.ARColoringBook
{
    public enum RenderPipelineMode
    {
        AutoDetect,
        BuiltIn,
        URP
    }

    [CreateAssetMenu(fileName = "Settings", menuName = "ColoringBook/Settings")]
    public class Settings : ScriptableObject
    {
        private static Settings _instance;
        public static Settings Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null)
                    LoadFromAssetDatabase();
#endif
                if (_instance == null)
                    Debug.LogError("[Settings] CRITICAL: Not loaded! Settings must be in Preloaded Assets.");
                return _instance;
            }
        }

        [Header("Quality Scoring Weights")]
        [Range(0f, 1f)] public float WEIGHT_ANGLE = 0.6f;
        [Range(0f, 1f)] public float WEIGHT_CENTER = 0.4f;

        [Header("Distance Constraints (Meters)")]
        public float MIN_SCAN_DIST = 0.2f;
        public float MAX_SCAN_DIST = 1.0f;
        [Range(0f, 1f)] public float DIST_PENALTY = 0.5f;

        [Header("Thresholds")]
        [Range(0f, 1f)]
        [SerializeField] private float _captureThreshold = 0.75f;
        public float CAPTURE_THRESHOLD => _captureThreshold;

        [Header("Motion Constraints")]
        [SerializeField] private float _maxMoveSpeed = 0.05f;
        public float MAX_MOVE_SPEED => _maxMoveSpeed;

        [SerializeField] private float _maxRotateSpeed = 5.0f;
        public float MAX_ROTATE_SPEED => _maxRotateSpeed;

        [Header("Render Pipeline")]
        [Tooltip("Auto-detect: Automatically detect the render pipeline\nBuilt-in: Force Built-in Render Pipeline\nURP: Force Universal Render Pipeline")]
        [SerializeField] private RenderPipelineMode _renderPipelineMode = RenderPipelineMode.AutoDetect;
        public RenderPipelineMode RenderPipelineMode => _renderPipelineMode;

        private bool _isURP = false;
        public bool IsURP => _isURP;

        private bool _urpCompatible = false;
        public bool IsURPCompatible => _urpCompatible;

        [Header("Solver Parameters")]
        [SerializeField] private float _inlierThreshold = 4f;
        public float INLIER_THRESHOLD => _inlierThreshold;

        [SerializeField] private int _maxIterations = 200;
        public int MAX_ITERATIONS => _maxIterations;

        [SerializeField] private bool _refineWithLM = true;
        public bool REFINE_WITH_LM => _refineWithLM;

        private void OnEnable() => _instance = this;

#if UNITY_EDITOR
        private void OnValidate()
        {
            EditorApplication.delayCall += () => RegisterPreloadedAsset(this);
        }

        public static void LoadFromAssetDatabase()
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Settings");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<Settings>(path);
            }
        }

        public static void RegisterPreloadedAsset(Settings settingsAsset)
        {
            if (settingsAsset == null) return;

            var preloaded = UnityEditor.PlayerSettings.GetPreloadedAssets().ToList();
            if (!preloaded.Contains(settingsAsset))
            {
                preloaded.Add(settingsAsset);
                UnityEditor.PlayerSettings.SetPreloadedAssets(preloaded.ToArray());
                Debug.Log($"[GameSettings] Auto-registered '{settingsAsset.name}' to Preloaded Assets!");
            }
        }
#endif
    }
}