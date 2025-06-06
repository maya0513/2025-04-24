using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// 車輪とインタラクターの距離を監視し、自動選択解除を行うクラス
public class WheelDistanceMonitor : MonoBehaviour
{
    // 設定
    float wheelRadius;
    float deselectionThreshold;
    XRInteractionManager interactionManager;

    // 監視設定
    [Range(0.01f, 0.1f)]
    float checkInterval = 0.05f;

    // 実行中のコルーチン
    Coroutine distanceMonitorCoroutine;

    // 初期化
    public void Initialize(float radius, float threshold, XRInteractionManager manager)
    {
        wheelRadius = radius;
        deselectionThreshold = threshold;
        interactionManager = manager;
    }

    // 距離監視開始
    public void StartDistanceMonitoring(IXRSelectInteractor interactor, WheelGrabPointManager grabPointManager, Transform wheelTransform)
    {
        StopDistanceMonitoring();
        distanceMonitorCoroutine = StartCoroutine(DistanceMonitorCoroutine(interactor, grabPointManager, wheelTransform));
    }

    // 距離監視停止
    public void StopDistanceMonitoring()
    {
        if (distanceMonitorCoroutine != null)
        {
            StopCoroutine(distanceMonitorCoroutine);
            distanceMonitorCoroutine = null;
        }
    }

    // 距離監視メインループ
    IEnumerator DistanceMonitorCoroutine(IXRSelectInteractor interactor, WheelGrabPointManager grabPointManager, Transform wheelTransform)
    {
        Transform interactorTransform = GetInteractorTransform(interactor);
        
        if (interactorTransform == null)
        {
            Debug.LogWarning($"[{gameObject.name}] インタラクターのTransformが取得できません。距離監視を停止します。");
            yield break;
        }

        float thresholdSqr = CalculateThresholdSquared();

        while (grabPointManager.HasActiveGrabPoint)
        {
            // 距離チェック
            if (IsDistanceExceeded(wheelTransform, interactorTransform, thresholdSqr))
            {
                // 自動選択解除
                CancelInteractorSelection(interactor);
                yield break;
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    // インタラクターのTransform取得
    Transform GetInteractorTransform(IXRSelectInteractor interactor)
    {
        MonoBehaviour interactorMono = interactor as MonoBehaviour;
        return interactorMono?.transform;
    }

    // 閾値の二乗値計算
    float CalculateThresholdSquared()
    {
        float totalThreshold = wheelRadius + deselectionThreshold;
        return totalThreshold * totalThreshold;
    }

    // 距離が閾値を超えているかチェック
    bool IsDistanceExceeded(Transform wheelTransform, Transform interactorTransform, float thresholdSqr)
    {
        Vector3 distance = wheelTransform.position - interactorTransform.position;
        float sqrDistance = distance.sqrMagnitude;
        return sqrDistance >= thresholdSqr;
    }

    // インタラクター選択解除
    void CancelInteractorSelection(IXRSelectInteractor interactor)
    {
        if (interactionManager != null)
        {
            interactionManager.CancelInteractorSelection(interactor);
        }
    }

    // プロパティ
    public bool IsActive => distanceMonitorCoroutine != null;
    public float CurrentThreshold => wheelRadius + deselectionThreshold;

    // 設定変更メソッド
    public void UpdateDeselectionThreshold(float newThreshold)
    {
        deselectionThreshold = Mathf.Max(0f, newThreshold);
    }

    public void UpdateCheckInterval(float newInterval)
    {
        checkInterval = Mathf.Clamp(newInterval, 0.01f, 0.1f);
    }

    // デバッグ用距離計算メソッド
    public float GetCurrentDistance(Transform wheelTransform, IXRSelectInteractor interactor)
    {
        Transform interactorTransform = GetInteractorTransform(interactor);
        if (wheelTransform == null || interactorTransform == null) return float.MaxValue;

        return Vector3.Distance(wheelTransform.position, interactorTransform.position);
    }

    // 破棄時の処理
    void OnDestroy()
    {
        StopDistanceMonitoring();
    }

    void OnDisable()
    {
        StopDistanceMonitoring();
    }

    // デバッグ表示（Scene Viewで距離の可視化）
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        // 車輪の位置と閾値範囲を可視化
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wheelRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, wheelRadius + deselectionThreshold);
    }
}