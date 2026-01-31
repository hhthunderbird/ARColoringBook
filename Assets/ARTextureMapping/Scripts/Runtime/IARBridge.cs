using System;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARTextureMapping.Base
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

        Camera GetARCamera();

        XRCameraIntrinsics? GetCameraIntrinsics();

        XRCpuImage? GetXRCpuImage();
    }
}