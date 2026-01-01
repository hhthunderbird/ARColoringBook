using System;
using Unity.Mathematics;
using UnityEngine;

namespace Felina.ARColoringBook
{
    // A library-agnostic structure to hold tracking data
    [Serializable]
    public struct ScanTarget
    {
        public string Name;
        public Vector2 Size; // Physical dimensions (meters)
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
        event Action<float4x4> OnDisplayMatrixUpdated;

        void SetTargetRenderTexture( RenderTexture targetRT );
        void UpdateCameraRT();

        Camera GetARCamera();

        RenderTexture MasterCameraFeed { get; }

        string GetImageName( Guid guid );
    }
}