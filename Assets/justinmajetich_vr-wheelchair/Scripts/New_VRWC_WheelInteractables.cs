using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// VR車椅子の車輪操作システム
public class New_VRWC_WheelInteractable : XRBaseInteractable
{
    // 物理演算関連
    Rigidbody m_Rigidbody;
    float wheelRadius;

    // 設定パラメータ
    bool onSlope = false;
    [SerializeField] bool hapticsEnabled = true;
    [Range(0, 0.5f), Tooltip("車輪からの距離閾値")]
    [SerializeField] float deselectionThreshold = 0.25f;
    [Range(1f, 50f), Tooltip("ブレーキ力の強度")]
    [SerializeField] float brakeForce = 25f;
    [Range(0.01f, 0.2f), Tooltip("静止判定の速度閾値")]
    [SerializeField] float stationaryThreshold = 0.05f;

    // 動的オブジェクト
    GameObject grabPoint;
    IXRSelectInteractor currentInteractor;

    // デバッグ用UI
    public Text label1;
    public Text label2;

    // 初期化
    private void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();

        if (m_Rigidbody == null)
        {
            Debug.LogError($"[{gameObject.name}] Rigidbodyコンポーネントが見つかりません。");
            return;
        }

        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            wheelRadius = sphereCollider.radius;
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] SphereColliderが見つかりません。デフォルト半径0.5を使用します。");
            wheelRadius = 0.5f;
        }

        StartCoroutine(CheckForSlope());
    }

    // 選択開始時の処理
    protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
    {
        base.OnSelectEntered(eventArgs);

        IXRSelectInteractor interactor = eventArgs.interactorObject;
        currentInteractor = interactor;

        CleanupGrabPoint();
        StartCoroutine(DelayedGrabSetup(interactor));
    }

    // 選択終了時の処理
    protected override void OnSelectExited(SelectExitEventArgs eventArgs)
    {
        base.OnSelectExited(eventArgs);
        CleanupGrabPoint();
        currentInteractor = null;
    }

    // 遅延グラブセットアップ
    IEnumerator DelayedGrabSetup(IXRSelectInteractor interactor)
    {
        yield return null;

        if (interactionManager != null)
        {
            // 修正: IXRSelectInteractable インターフェースにキャスト
            interactionManager.CancelInteractableSelection(this as IXRSelectInteractable);
        }

        SpawnGrabPoint(interactor);
        StartCoroutine(BrakeAssist(interactor));
        StartCoroutine(MonitorDetachDistance(interactor));

        if (hapticsEnabled)
        {
            StartCoroutine(SendHapticFeedback(interactor));
        }
    }

    // 動的グラブポイント生成
    void SpawnGrabPoint(IXRSelectInteractor interactor)
    {
        CleanupGrabPoint();

        grabPoint = new GameObject($"{transform.name}_GrabPoint", typeof(XRGrabInteractable), typeof(Rigidbody), typeof(FixedJoint));

        Transform interactorTransform = (interactor as MonoBehaviour)?.transform;
        if (interactorTransform != null)
        {
            grabPoint.transform.position = interactorTransform.position;
        }

        Rigidbody grabPointRb = grabPoint.GetComponent<Rigidbody>();
        if (grabPointRb != null)
        {
            grabPointRb.mass = 0.1f;
            grabPointRb.useGravity = false;
        }

        FixedJoint joint = grabPoint.GetComponent<FixedJoint>();
        if (joint != null && m_Rigidbody != null)
        {
            joint.connectedBody = m_Rigidbody;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;
        }

        XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null && interactionManager != null)
        {
            interactionManager.SelectEnter(interactor, grabInteractable);
        }
    }

    // グラブポイントのクリーンアップ
    void CleanupGrabPoint()
    {
        if (grabPoint != null)
        {
            XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null && interactionManager != null)
            {
                // 修正: IXRSelectInteractable インターフェースにキャスト
                interactionManager.CancelInteractableSelection(grabInteractable as IXRSelectInteractable);
            }

            DestroyImmediate(grabPoint);
            grabPoint = null;
        }
    }

    // ブレーキアシスト機能
    IEnumerator BrakeAssist(IXRSelectInteractor interactor)
    {
        New_VRWC_XRNodeVelocitySupplier interactorVelocity = (interactor as MonoBehaviour)?.GetComponent<New_VRWC_XRNodeVelocitySupplier>();

        if (interactorVelocity == null)
        {
            Debug.LogWarning($"[{gameObject.name}] VelocitySupplierが見つかりません。ブレーキアシストを無効化します。");
            yield break;
        }

        bool wasStationary = false;

        while (grabPoint != null && currentInteractor == interactor)
        {
            bool isCurrentlyStationary = Mathf.Abs(interactorVelocity.velocity.z) < stationaryThreshold;

            if (isCurrentlyStationary && !wasStationary && m_Rigidbody != null)
            {
                Vector3 currentAngularVelocity = m_Rigidbody.angularVelocity;
                if (currentAngularVelocity.magnitude > 0.1f)
                {
                    m_Rigidbody.AddTorque(-currentAngularVelocity.normalized * brakeForce);
                }
            }

            wasStationary = isCurrentlyStationary;
            yield return new WaitForFixedUpdate();
        }
    }

    // 距離監視による自動選択解除
    IEnumerator MonitorDetachDistance(IXRSelectInteractor interactor)
    {
        float checkInterval = 0.05f;

        Transform interactorTransform = (interactor as MonoBehaviour)?.transform;
        if (interactorTransform == null)
        {
            yield break;
        }

        while (grabPoint != null && currentInteractor == interactor)
        {
            float sqrDistance = (transform.position - interactorTransform.position).sqrMagnitude;
            float thresholdSqr = (wheelRadius + deselectionThreshold) * (wheelRadius + deselectionThreshold);

            if (sqrDistance >= thresholdSqr)
            {
                if (interactionManager != null)
                {
                    interactionManager.CancelInteractorSelection(interactor);
                }
                yield break;
            }

            yield return new WaitForSeconds(checkInterval);
        }
    }

    // 触覚フィードバック
    IEnumerator SendHapticFeedback(IXRSelectInteractor interactor)
    {
        float runInterval = 0.1f;

        XRBaseInputInteractor controllerInteractor = interactor as XRBaseInputInteractor;

        if (controllerInteractor == null || m_Rigidbody == null)
        {
            yield break;
        }

        Vector3 lastAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);

        while (grabPoint != null && currentInteractor == interactor)
        {
            Vector3 currentAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);
            Vector3 angularAcceleration = (currentAngularVelocity - lastAngularVelocity) / runInterval;

            if (currentAngularVelocity.magnitude > 0.1f && Vector3.Dot(currentAngularVelocity.normalized, angularAcceleration.normalized) < 0f)
            {
                float impulseAmplitude = Mathf.Abs(angularAcceleration.x);

                if (impulseAmplitude > 1.5f)
                {
                    float remappedImpulseAmplitude = Remap(impulseAmplitude, 1.5f, 40f, 0f, 1f);
                    controllerInteractor.SendHapticImpulse(remappedImpulseAmplitude, runInterval * 2f);
                }
            }

            lastAngularVelocity = currentAngularVelocity;
            yield return new WaitForSeconds(runInterval);
        }
    }

    // 値の範囲変換
    float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

    // 坂道検出
    IEnumerator CheckForSlope()
    {
        while (true)
        {
            if (Physics.Raycast(transform.position, -Vector3.up, out RaycastHit hit, wheelRadius * 2f))
            {
                onSlope = Vector3.Angle(hit.normal, Vector3.up) > 1f;
            }
            else
            {
                onSlope = false;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    // クリーンアップ処理
    protected override void OnDestroy()
    {
        base.OnDestroy();
        CleanupGrabPoint();
        StopAllCoroutines();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CleanupGrabPoint();
        currentInteractor = null;
    }
}