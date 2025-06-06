using System.Collections;
using UnityEngine;

/// 車輪の坂道検出機能を提供するクラス
public class WheelSlopeDetector : MonoBehaviour
{
    // 設定
    float wheelRadius;
    Transform wheelTransform;

    // 坂道検出設定
    [Range(0.1f, 10f), Tooltip("坂道判定の角度閾値（度）")]
    [SerializeField] float slopeAngleThreshold = 1f;
    [Range(0.1f, 1f), Tooltip("坂道検出の更新間隔（秒）")]
    [SerializeField] float detectionInterval = 0.2f;
    [Range(1f, 5f), Tooltip("レイキャスト距離の倍率")]
    [SerializeField] float raycastDistanceMultiplier = 2f;

    // 状態
    bool onSlope = false;

    // 実行中のコルーチン
    Coroutine slopeDetectionCoroutine;

    // 初期化
    public void Initialize(float radius, Transform transform)
    {
        wheelRadius = radius;
        wheelTransform = transform;
        StartSlopeDetection();
    }

    // 坂道検出開始
    public void StartSlopeDetection()
    {
        StopSlopeDetection();
        slopeDetectionCoroutine = StartCoroutine(SlopeDetectionCoroutine());
    }

    // 坂道検出停止
    public void StopSlopeDetection()
    {
        if (slopeDetectionCoroutine != null)
        {
            StopCoroutine(slopeDetectionCoroutine);
            slopeDetectionCoroutine = null;
        }
    }

    // 坂道検出メインループ
    IEnumerator SlopeDetectionCoroutine()
    {
        while (enabled)
        {
            DetectSlope();
            yield return new WaitForSeconds(detectionInterval);
        }
    }

    // 坂道検出処理
    void DetectSlope()
    {
        if (wheelTransform == null) return;

        Vector3 rayOrigin = wheelTransform.position;
        Vector3 rayDirection = -Vector3.up;
        float rayDistance = wheelRadius * raycastDistanceMultiplier;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayDistance))
        {
            float slopeAngle = CalculateSlopeAngle(hit.normal);
            bool newSlopeState = slopeAngle > slopeAngleThreshold;
            
            // 状態が変化した場合のみ更新
            if (newSlopeState != onSlope)
            {
                onSlope = newSlopeState;
                OnSlopeStateChanged(onSlope, slopeAngle);
            }
        }
        else
        {
            // 地面が検出されない場合は平地とみなす
            if (onSlope)
            {
                onSlope = false;
                OnSlopeStateChanged(false, 0f);
            }
        }
    }

    // 坂道角度計算
    float CalculateSlopeAngle(Vector3 surfaceNormal)
    {
        return Vector3.Angle(surfaceNormal, Vector3.up);
    }

    // 坂道状態変化時のコールバック
    void OnSlopeStateChanged(bool isOnSlope, float angle)
    {
        // デバッグログ
        if (isOnSlope)
        {
            Debug.Log($"[{gameObject.name}] 坂道を検出: {angle:F1}度");
        }
        else
        {
            Debug.Log($"[{gameObject.name}] 平地を検出");
        }

        // 必要に応じて他のコンポーネントに通知
        BroadcastSlopeStateChange(isOnSlope, angle);
    }

    // 坂道状態変化を他のコンポーネントに通知
    void BroadcastSlopeStateChange(bool isOnSlope, float angle)
    {
        // 同じGameObjectの他のコンポーネントに通知
        SendMessage("OnSlopeDetected", new SlopeInfo { onSlope = isOnSlope, angle = angle }, SendMessageOptions.DontRequireReceiver);
    }

    // 現在の坂道情報取得
    public SlopeInfo GetCurrentSlopeInfo()
    {
        if (wheelTransform == null) return new SlopeInfo { onSlope = false, angle = 0f };

        Vector3 rayOrigin = wheelTransform.position;
        Vector3 rayDirection = -Vector3.up;
        float rayDistance = wheelRadius * raycastDistanceMultiplier;

        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, rayDistance))
        {
            float angle = CalculateSlopeAngle(hit.normal);
            return new SlopeInfo 
            { 
                onSlope = angle > slopeAngleThreshold,
                angle = angle,
                surfaceNormal = hit.normal,
                hitPoint = hit.point
            };
        }

        return new SlopeInfo { onSlope = false, angle = 0f };
    }

    // プロパティ
    public bool OnSlope => onSlope;
    public float SlopeAngleThreshold => slopeAngleThreshold;
    public bool IsDetectionActive => slopeDetectionCoroutine != null;

    // 設定変更メソッド
    public void UpdateSlopeAngleThreshold(float newThreshold)
    {
        slopeAngleThreshold = Mathf.Clamp(newThreshold, 0.1f, 10f);
    }

    public void UpdateDetectionInterval(float newInterval)
    {
        detectionInterval = Mathf.Clamp(newInterval, 0.1f, 1f);
    }

    public void UpdateRaycastDistanceMultiplier(float newMultiplier)
    {
        raycastDistanceMultiplier = Mathf.Clamp(newMultiplier, 1f, 5f);
    }

    // 破棄時の処理
    void OnDestroy()
    {
        StopSlopeDetection();
    }

    void OnDisable()
    {
        StopSlopeDetection();
    }

    // デバッグ表示（Scene Viewでレイキャストの可視化）
    void OnDrawGizmosSelected()
    {
        if (wheelTransform == null) return;

        Vector3 rayOrigin = wheelTransform.position;
        Vector3 rayDirection = -Vector3.up;
        float rayDistance = wheelRadius * raycastDistanceMultiplier;

        // レイキャストの可視化
        Gizmos.color = onSlope ? Color.red : Color.green;
        Gizmos.DrawRay(rayOrigin, rayDirection * rayDistance);

        // 車輪の半径も表示
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(rayOrigin, wheelRadius);
    }
}

/// 坂道情報を格納する構造体
[System.Serializable]
public struct SlopeInfo
{
    public bool onSlope;
    public float angle;
    public Vector3 surfaceNormal;
    public Vector3 hitPoint;
}