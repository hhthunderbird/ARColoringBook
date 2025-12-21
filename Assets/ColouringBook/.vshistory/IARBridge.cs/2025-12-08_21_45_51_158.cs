using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;

namespace Felina.ARColoringBook
{
    // A library-agnostic structure to hold tracking data
    public struct ScanTarget
    {
        public string Name;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector2 Size; // Physical dimensions (meters)
        public bool IsTracking;
        public Transform Transform; // Reference to the actual game object
        public TrackableId TrackableId;
        public float Score;
    }

    public struct RenderTextureSettings
    {
        public RenderTextureFormat Format;
        public int Width;
        public int Height;
        public bool useMipMap;
        public bool autoGenerateMips;
        public FilterMode filterMode;
    }

    public interface IARBridge
    {
        // Event that fires when ANY image target moves or is found
        event Action<ScanTarget> OnTargetAdded;

        // The Manager calls this every frame to get the raw video feed
        // The implementation must Blit/Copy the camera image into the provided RenderTexture
        //void FillCameraTexture( RenderTexture destination );

        RenderTexture GetCameraFeedRT();

        // We need the Camera to calculate Screen Points (WorldToScreenPoint)
        Camera GetARCamera();

        RenderTextureSettings RenderTextureSettings { get; }
    }
}