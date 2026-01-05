using System;
using Unity.Mathematics;
using UnityEngine;

namespace Felina.ARColoringBook.Base
{
    [Serializable]
    public struct ScanTarget
    {
        public string Name;
        public Vector2 Size; 
        public Transform Transform; 
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