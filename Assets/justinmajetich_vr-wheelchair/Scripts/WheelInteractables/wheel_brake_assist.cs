using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// 車輪のブレーキアシスト機能を提供するクラス
public class WheelBrakeAssist : MonoBehaviour
{
    // 設定
    Rigidbody wheelRigidbody;
    float stationaryThreshold;
    float brakeForce;

    // 実行中のコルーチン
    Coroutine brakeAssistCoroutine;

    // 初期化
    public void Initialize(Rigidbody rigidbody, float threshold, float force)
    {
        wheelRigidbody = rigidbody;
        stationaryThreshold = threshold;
        brakeForce = force;
    }

    // ブレーキアシスト開始
    public void StartBrakeAssist(IXRSelectInteractor interactor, WheelGrabPointManager grabPointManager)
    {
        StopBrakeAssist();
        brakeAssistCoroutine = StartCoroutine(BrakeAssistCoroutine(interactor, grabPointManager));
    }

    // ブレーキアシスト停止
    public void StopBrakeAssist()
    {
        if (brakeAssistCoroutine != null)
        {
            StopCoroutine(brakeAssistCoroutine);
            brakeAssistCoroutine = null;
        }
    }

    // ブレーキアシストメインループ
    IEnumerator BrakeAssistCoroutine(IXRSelectInteractor interactor, WheelGrabPointManager grabPointManager)
    {
        // Velocity Supplierを取得
        New_VRWC_XRNodeVelocitySupplier interactorVelocity = GetInteractorVelocitySupplier(interactor);

        if (interactorVelocity == null)
        {
            Debug.LogWarning($"[{gameObject.name}] VelocitySupplierが見つかりません。ブレーキアシストを無効化します。");
            yield break;
        }

        bool wasStationary = false;

        while (grabPointManager.HasActiveGrabPoint)
        {
            // 現在の静止状態をチェック
            bool isCurrentlyStationary = IsInteractorStationary(interactorVelocity);

            // 静止状態に変化した時にブレーキを適用
            if (isCurrentlyStationary && !wasStationary)
            {
                ApplyBrake();
            }

            wasStationary = isCurrentlyStationary;
            yield return new WaitForFixedUpdate();
        }
    }

    // インタラクターのVelocity Supplier取得
    New_VRWC_XRNodeVelocitySupplier GetInteractorVelocitySupplier(IXRSelectInteractor interactor)
    {
        MonoBehaviour interactorMono = interactor as MonoBehaviour;
        return interactorMono?.GetComponent<New_VRWC_XRNodeVelocitySupplier>();
    }

    // インタラクターが静止しているかチェック
    bool IsInteractorStationary(New_VRWC_XRNodeVelocitySupplier velocitySupplier)
    {
        if (velocitySupplier == null) return false;
        return Mathf.Abs(velocitySupplier.velocity.z) < stationaryThreshold;
    }

    // ブレーキ適用
    void ApplyBrake()
    {
        if (wheelRigidbody == null) return;

        Vector3 currentAngularVelocity = wheelRigidbody.angularVelocity;
        
        // 回転している場合のみブレーキを適用
        if (currentAngularVelocity.magnitude > 0.1f)
        {
            Vector3 brakeForceVector = -currentAngularVelocity.normalized * brakeForce;
            wheelRigidbody.AddTorque(brakeForceVector);
        }
    }

    // プロパティ
    public bool IsActive => brakeAssistCoroutine != null;

    // パラメータ更新メソッド
    public void UpdateStationaryThreshold(float newThreshold)
    {
        stationaryThreshold = newThreshold;
    }

    public void UpdateBrakeForce(float newForce)
    {
        brakeForce = newForce;
    }

    // 破棄時の処理
    void OnDestroy()
    {
        StopBrakeAssist();
    }

    void OnDisable()
    {
        StopBrakeAssist();
    }
}