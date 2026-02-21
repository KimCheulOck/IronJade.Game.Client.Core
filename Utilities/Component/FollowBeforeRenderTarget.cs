using UnityEngine;

/// <summary>
/// 특정 Transform을 따라다니는 컴포넌트.
/// - Enable 시 즉시 업데이트 옵션
/// - 부드럽게(Smooth) / 즉각(Instant) 모드
/// - World / Local 좌표 모드
/// 렌더 직전(onBeforeRender)에 동기화하여 CinemachineBrain이 카메라를 옮긴 뒤 같은 프레임에 따라감.
/// </summary>
public class FollowBeforeRenderTarget : MonoBehaviour
{
    public enum FollowMode
    {
        Instant,
        Smooth
    }

    public enum SpaceMode
    {
        World,
        Local
    }

    [Header("대상")]
    [Tooltip("따라갈 Transform (비어 있으면 비활성화)")]
    [SerializeField]
    private Transform target;

    [Header("동작")]
    [Tooltip("켜질 때 한 번 즉시 목표 위치/회전으로 맞춤")]
    [SerializeField]
    private bool updateImmediatelyOnEnable = true;

    [Tooltip("즉각 따라가기 vs 부드럽게 따라가기")]
    [SerializeField]
    private FollowMode followMode = FollowMode.Instant;

    [Tooltip("World: 월드 좌표 기준 / Local: 로컬(부모 기준) 좌표 기준")]
    [SerializeField]
    private SpaceMode spaceMode = SpaceMode.World;

    [Header("추적 대상 (체크한 것만 적용)")]
    [SerializeField]
    private bool followPosition = true;
    [SerializeField]
    private bool followRotation = true;

    [Header("Smooth 설정 (FollowMode.Smooth일 때)")]
    [Tooltip("위치 보간 속도 (큰 값일수록 빨리 따라감)")]
    [SerializeField]
    private float positionSmoothSpeed = 10f;
    [Tooltip("회전 보간 속도 (큰 값일수록 빨리 따라감)")]
    [SerializeField]
    private float rotationSmoothSpeed = 10f;

    private Vector3 velocity;

    private void OnEnable()
    {
        Application.onBeforeRender += OnBeforeRender;
        if (updateImmediatelyOnEnable && target != null)
            ApplyToTargetInstant();
    }

    private void OnDisable()
    {
        Application.onBeforeRender -= OnBeforeRender;
    }

    /// <summary>
    /// 렌더 직전 호출 — CinemachineBrain 등이 카메라를 갱신한 뒤이므로 같은 프레임에 맞춤.
    /// </summary>
    private void OnBeforeRender()
    {
        if (target == null) return;

        if (followMode == FollowMode.Instant)
            ApplyToTargetInstant();
        else
            ApplyToTargetSmooth();
    }

    /// <summary>
    /// 목표와 동일한 위치/회전으로 즉시 이동 (한 프레임만)
    /// </summary>
    private void ApplyToTargetInstant()
    {
        if (spaceMode == SpaceMode.World)
        {
            if (followPosition) transform.position = target.position;
            if (followRotation) transform.rotation = target.rotation;
        }
        else
        {
            if (followPosition) transform.localPosition = target.localPosition;
            if (followRotation) transform.localRotation = target.localRotation;
        }
    }

    private void ApplyToTargetSmooth()
    {
        float dt = Time.deltaTime;

        if (spaceMode == SpaceMode.World)
        {
            if (followPosition)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    target.position,
                    ref velocity,
                    1f / Mathf.Max(0.001f, positionSmoothSpeed),
                    Mathf.Infinity,
                    dt
                );
            }
            if (followRotation)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    target.rotation,
                    1f - Mathf.Exp(-rotationSmoothSpeed * dt)
                );
            }
        }
        else
        {
            if (followPosition)
            {
                transform.localPosition = Vector3.SmoothDamp(
                    transform.localPosition,
                    target.localPosition,
                    ref velocity,
                    1f / Mathf.Max(0.001f, positionSmoothSpeed),
                    Mathf.Infinity,
                    dt
                );
            }
            if (followRotation)
            {
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    target.localRotation,
                    1f - Mathf.Exp(-rotationSmoothSpeed * dt)
                );
            }
        }
    }

    /// <summary>
    /// 따라갈 대상 설정
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }
}
