using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// 車輪の触覚フィードバック機能を提供するクラス
public class WheelHapticFeedback : MonoBehaviour
{
    // 設定
    Rigidbody wheelRigidbody;
    Transform wheelTransform;

    // フィードバック設定
    [Range(0.05f, 0.2f)]
    float feedbackInterval = 0.1f;
    [Range(1f, 50f)]
    float minImpulseThreshold = 1.5f;
    [Range(10f, 100f)]
    float maxImpulseValue = 40f;

    // 実行中のコルーチン
    Coroutine hapticFeedbackCoroutine;

    // 初期化
    public void Initialize(Rigidbody rigidbody, Transform transform)
    {
        wheelRigidbody = rigidbody;
        wheelTransform = transform;

        // Rigidbodyの新形式プロパティを確認（必要に応じて設定）
        if (wheelRigidbody != null)
        {
            // 車輪の物理設定を最適化
            wheelRigidbody.linearDamping = Mathf.Max(0f, wheelRigidbody.linearDamping);
            wheelRigidbody.angularDamping = Mathf.Max(0f, wheelRigidbody.angularDamping);
        }
    }

    // 触覚フィードバック開始
    public void StartHapticFeedback(IXRSelectInteractor interactor, WheelGrabPointManager grabPointManager)
    {
        StopHapticFeedback();
        hapticFeedbackCoroutine = StartCoroutine(HapticFeedbackCoroutine(interactor, grabPointManager));
    }

    // 触覚フィードバック停止
    public void StopHapticFeedback()
    {
        if (hapticFeedbackCoroutine != null)
        {
            StopCoroutine(hapticFeedbackCoroutine);
            hapticFeedbackCoroutine = null;
        }
    }

    // 触覚フィードバックメインループ
    IEnumerator HapticFeedbackCoroutine(IXRSelectInteractor interactor, WheelGrabPointManager grabPointManager)
    {
        // コントローラーインタラクター取得
        XRBaseInputInteractor controllerInteractor = GetControllerInteractor(interactor);

        if (controllerInteractor == null || wheelRigidbody == null)
        {
            Debug.LogWarning($"[{gameObject.name}] 触覚フィードバック用のコンポーネントが見つかりません。");
            yield break;
        }

        Vector3 lastAngularVelocity = GetLocalAngularVelocity();

        while (grabPointManager.HasActiveGrabPoint)
        {
            Vector3 currentAngularVelocity = GetLocalAngularVelocity();
            Vector3 angularAcceleration = CalculateAngularAcceleration(currentAngularVelocity, lastAngularVelocity);

            // 触覚フィードバック条件をチェック
            if (ShouldSendHapticFeedback(currentAngularVelocity, angularAcceleration))
            {
                float impulseAmplitude = CalculateImpulseAmplitude(angularAcceleration);
                SendHapticImpulse(controllerInteractor, impulseAmplitude);
            }

            lastAngularVelocity = currentAngularVelocity;
            yield return new WaitForSeconds(feedbackInterval);
        }
    }

    // コントローラーインタラクター取得
    XRBaseInputInteractor GetControllerInteractor(IXRSelectInteractor interactor)
    {
        return interactor as XRBaseInputInteractor;
    }

    // ローカル角速度取得
    Vector3 GetLocalAngularVelocity()
    {
        if (wheelRigidbody == null || wheelTransform == null) return Vector3.zero;

        Vector3 worldAngularVelocity = wheelRigidbody.angularVelocity;
        Vector3 localAngularVelocity = wheelTransform.InverseTransformDirection(worldAngularVelocity);

        // X軸の回転のみを使用
        return new Vector3(localAngularVelocity.x, 0f, 0f);
    }

    // 角加速度計算
    Vector3 CalculateAngularAcceleration(Vector3 currentVelocity, Vector3 lastVelocity)
    {
        return (currentVelocity - lastVelocity) / feedbackInterval;
    }

    // 触覚フィードバック送信条件チェック
    bool ShouldSendHapticFeedback(Vector3 currentVelocity, Vector3 acceleration)
    {
        // 十分な速度がある
        if (currentVelocity.magnitude <= 0.1f) return false;

        // 減速している（負の加速度）
        float velocityAccelerationDot = Vector3.Dot(currentVelocity.normalized, acceleration.normalized);
        return velocityAccelerationDot < 0f;
    }

    // インパルス振幅計算
    float CalculateImpulseAmplitude(Vector3 acceleration)
    {
        float impulseAmplitude = Mathf.Abs(acceleration.x);

        if (impulseAmplitude <= minImpulseThreshold) return 0f;

        return Remap(impulseAmplitude, minImpulseThreshold, maxImpulseValue, 0f, 1f);
    }

    // 触覚インパルス送信
    void SendHapticImpulse(XRBaseInputInteractor controllerInteractor, float amplitude)
    {
        if (amplitude <= 0f) return;

        float duration = feedbackInterval * 2f;
        controllerInteractor.SendHapticImpulse(amplitude, duration);
    }

    // 値の範囲変換
    float Remap(float value, float from1, float to1, float from2, float to2)
    {
        float normalizedValue = Mathf.Clamp01((value - from1) / (to1 - from1));
        return Mathf.Lerp(from2, to2, normalizedValue);
    }

    // プロパティ
    public bool IsActive => hapticFeedbackCoroutine != null;

    // 設定変更メソッド
    public void UpdateFeedbackInterval(float newInterval)
    {
        feedbackInterval = Mathf.Clamp(newInterval, 0.05f, 0.2f);
    }

    public void UpdateImpulseThreshold(float newThreshold)
    {
        minImpulseThreshold = Mathf.Clamp(newThreshold, 1f, 50f);
    }

    public void UpdateMaxImpulseValue(float newMaxValue)
    {
        maxImpulseValue = Mathf.Clamp(newMaxValue, 10f, 100f);
    }

    // 破棄時の処理
    void OnDestroy()
    {
        StopHapticFeedback();
    }

    void OnDisable()
    {
        StopHapticFeedback();
    }
}