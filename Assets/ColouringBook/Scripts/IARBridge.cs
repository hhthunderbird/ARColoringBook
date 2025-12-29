using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace Felina.ARColoringBook
{
    // A library-agnostic structure to hold tracking data
    [Serializable]
    public struct ScanTarget
    {
        public string Name;
        public Vector2 Size; // Physical dimensions (meters)
        public bool IsTracking;
        public Transform Transform; // Reference to the actual game object
        public float Score;
    }

    public struct RenderTextureSettings
    {
        public RenderTextureFormat Format;
        public int Width;
        public int Height;
        public bool UseMipMap;
        public bool AutoGenerateMips;
        public FilterMode FilterMode;
    }

    public interface IARBridge
    {
        event Action<ScanTarget> OnTargetAdded;

        void SetTargetRenderTexture( RenderTexture targetRT );
        void GetCameraFeedRT();

        Camera GetARCamera();

        RenderTextureSettings RenderTextureSettings { get; }
        ARTrackedImageManager ARTrackedImageManager { get; }

        //TODO: Remove dependency on ARFoundation types
        ARCameraBackground GetARCameraBackground();
    }
}