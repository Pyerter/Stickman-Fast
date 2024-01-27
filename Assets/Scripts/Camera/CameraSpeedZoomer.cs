using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class CameraSpeedZoomer : CinemachineExtension
{
    protected CinemachineVirtualCamera _cam;
    public CinemachineVirtualCamera Cam { get { if (_cam == null) _cam = GetComponent<CinemachineVirtualCamera>(); return _cam; } }

    [SerializeField] private float originalOrthoSize = 20f;
    protected bool initialized = false;
    protected Vector2 currentVelocity;

    [SerializeField][Range(0, 1000)] protected float orthoSizeBaseSpeed = 20f;
    [SerializeField][Range(1, 10)] protected float orthoSizeSpeedScale = 3f;
    [SerializeField] [Range(1, 100)] protected float maxOrthoSize = 20f;
    [SerializeField][Range(0, 0.4f)] protected float cameraLerpSpeed = 0.1f;
    protected float speedMultiplier;
    public float SpeedMultiplier { get { return speedMultiplier; } }
    
    protected override void OnEnable()
    {
        base.OnEnable();
        if (!initialized)
        {
            originalOrthoSize = Cam.m_Lens.OrthographicSize;
            initialized = true;
        }
    }

    public void UpdateVelocity(Vector2 velocity)
    {
        currentVelocity = velocity;
        speedMultiplier = Mathf.Log(Mathf.Abs(currentVelocity.x), orthoSizeBaseSpeed) * orthoSizeSpeedScale;
    }

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (originalOrthoSize == 0)
        {
            originalOrthoSize = Cam.m_Lens.OrthographicSize;
            Debug.Log("Ortho size is 0, skipping camera scale");
            return;
        }
        float newSize = speedMultiplier * originalOrthoSize;
        newSize = Mathf.Lerp(Cam.m_Lens.OrthographicSize, newSize, cameraLerpSpeed);
        if (newSize < originalOrthoSize)
            newSize = originalOrthoSize;
        if (newSize > maxOrthoSize)
            newSize = maxOrthoSize;
        if (!float.IsNaN(newSize))
        {
            Cam.m_Lens.OrthographicSize = newSize;
        }
    }
}
