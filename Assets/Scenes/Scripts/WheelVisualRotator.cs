using UnityEngine;

/// <summary>
/// プレイヤー（車椅子本体）の移動速度を参照して、
/// 見た目用ホイールメッシュを回転させるだけのスクリプト。
/// 物理挙動には一切影響を与えない。
/// </summary>
[DisallowMultipleComponent]
public sealed class WheelVisualRotator : MonoBehaviour
{
    // ────────── Inspector で設定する項目 ──────────
    [Header("References")]
    [SerializeField] private Rigidbody parentRigidbody;      // 車椅子本体
    [SerializeField] private Transform wheelMeshTransform;   // 回転させるメッシュ

    [Header("Wheel Settings")]
    [Min(0.001f)]
    [SerializeField] private float wheelRadius = 0.3f;        // タイヤ半径[m]
    [SerializeField] private Vector3 localRotationAxis = Vector3.right; // 回転軸

    [Header("Options")]
    [Tooltip("後退時も正方向に回転させたい場合は ON")]
    [SerializeField] private bool ignoreDirection = false;

    // ────────── Unity Callback ──────────
    private void Reset()
    {
        // 自動で参照を補完（手動設定が望ましいが保険として）
        parentRigidbody = GetComponentInParent<Rigidbody>();
        wheelMeshTransform = transform;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (wheelRadius < 0.001f) wheelRadius = 0.001f;
    }
#endif

    // 見た目の回転なので LateUpdate で十分（描画直前）
    private void LateUpdate()
    {
        if (!IsValid()) return;

        // 1. ワールド速度
        Vector3 worldVelocity = parentRigidbody.linearVelocity;

        // 2. 本体ローカル速度
        Vector3 localVelocity = parentRigidbody.transform.InverseTransformDirection(worldVelocity);
        float forwardSpeed = ignoreDirection ? Mathf.Abs(localVelocity.z) : localVelocity.z;

        // 3. 角速度 (rad/s)
        float angularVel = forwardSpeed / wheelRadius;

        // 4. フレーム内回転量 (deg)
        float deltaAngleDeg = angularVel * Mathf.Rad2Deg * Time.deltaTime;

        // 5. 回転適用
        wheelMeshTransform.Rotate(localRotationAxis, deltaAngleDeg, Space.Self);
    }

    // ────────── Helper ──────────
    private bool IsValid()
    {
        if (parentRigidbody == null || wheelMeshTransform == null)
        {
            // １度だけ警告を出して無効化
            Debug.LogWarning($"[{nameof(WheelVisualRotator)}] 必須参照が設定されていません。スクリプトを無効化します。", this);
            enabled = false;
            return false;
        }
        return true;
    }
}