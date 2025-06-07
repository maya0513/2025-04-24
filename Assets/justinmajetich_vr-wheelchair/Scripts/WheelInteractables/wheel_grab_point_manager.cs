using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// 動的グラブポイントの生成と管理を行うクラス
public class WheelGrabPointManager : MonoBehaviour
{
    // 設定
    Rigidbody wheelRigidbody;
    XRInteractionManager interactionManager;

    // 動的オブジェクト
    GameObject grabPoint;

    // 初期化
    public void Initialize(Rigidbody rigidbody, XRInteractionManager manager)
    {
        wheelRigidbody = rigidbody;
        interactionManager = manager;
    }

    // 動的グラブポイント生成（即座にインタラクション開始）
    public void SpawnGrabPointImmediate(IXRSelectInteractor interactor, Transform wheelTransform)
    {
        // 既存のグラブポイントをクリーンアップ
        CleanupGrabPoint();

        // グラブポイント作成
        grabPoint = CreateGrabPointObject(wheelTransform);

        // インタラクターの位置にセットアップ
        SetupGrabPointPosition(interactor);

        // 物理設定
        SetupGrabPointPhysics();

        // ジョイント設定
        SetupGrabPointJoint();

        // 即座にインタラクション開始
        SetupGrabPointInteraction(interactor);
    }

    // 動的グラブポイント生成（従来の遅延版）
    public void SpawnGrabPoint(IXRSelectInteractor interactor, Transform wheelTransform)
    {
        // 既存のグラブポイントをクリーンアップ
        CleanupGrabPoint();

        // グラブポイント作成
        grabPoint = CreateGrabPointObject(wheelTransform);

        // インタラクターの位置にセットアップ
        SetupGrabPointPosition(interactor);

        // 物理設定
        SetupGrabPointPhysics();

        // ジョイント設定
        SetupGrabPointJoint();

        // インタラクション設定
        StartCoroutine(DelayedInteractionSetup(interactor));
    }

    // グラブポイントオブジェクト作成
    GameObject CreateGrabPointObject(Transform wheelTransform)
    {
        return new GameObject(
            $"{wheelTransform.name}_GrabPoint",
            typeof(XRGrabInteractable),
            typeof(Rigidbody),
            typeof(FixedJoint)
        );
    }

    // グラブポイント位置設定
    void SetupGrabPointPosition(IXRSelectInteractor interactor)
    {
        Transform interactorTransform = (interactor as MonoBehaviour)?.transform;
        if (interactorTransform != null && grabPoint != null && wheelRigidbody != null)
        {
            // インタラクターの位置を車輪の表面に投影
            Vector3 wheelCenter = wheelRigidbody.transform.position;
            Vector3 interactorPosition = interactorTransform.position;

            // 車輪の中心からインタラクターへのベクトル
            Vector3 direction = (interactorPosition - wheelCenter).normalized;

            // 車輪の半径を取得（SphereColliderから）
            float wheelRadius = GetWheelRadius();

            // 車輪の表面上の点を計算
            Vector3 surfacePoint = wheelCenter + direction * wheelRadius;

            grabPoint.transform.position = surfacePoint;

            // グラブポイントの向きを車輪に合わせる
            grabPoint.transform.rotation = wheelRigidbody.transform.rotation;
        }
    }

    // 車輪の半径を取得
    float GetWheelRadius()
    {
        if (wheelRigidbody == null) return 0.5f;

        SphereCollider sphereCollider = wheelRigidbody.GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            return sphereCollider.radius * wheelRigidbody.transform.localScale.x;
        }

        // フォールバック値
        return 0.5f;
    }

    // グラブポイント物理設定
    void SetupGrabPointPhysics()
    {
        if (grabPoint == null) return;

        Rigidbody grabPointRb = grabPoint.GetComponent<Rigidbody>();
        if (grabPointRb != null)
        {
            grabPointRb.mass = 0.1f;
            grabPointRb.useGravity = false;
            grabPointRb.isKinematic = false;
            // 新形式のドラッグ設定
            grabPointRb.linearDamping = 0f;
            grabPointRb.angularDamping = 0f;
        }

        // XRGrabInteractableの設定
        XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            // 移動タイプを設定（重要：これが逆回転の原因）
            grabInteractable.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            // 速度追跡設定
            grabInteractable.velocityDamping = 1f;
            grabInteractable.velocityScale = 1f;
            grabInteractable.angularVelocityDamping = 1f;
            grabInteractable.angularVelocityScale = 1f;
            // 投げる動作を無効化
            grabInteractable.throwOnDetach = false;
            grabInteractable.forceGravityOnDetach = false;
        }
    }

    // グラブポイントジョイント設定
    void SetupGrabPointJoint()
    {
        if (grabPoint == null || wheelRigidbody == null) return;

        FixedJoint joint = grabPoint.GetComponent<FixedJoint>();
        if (joint != null)
        {
            joint.connectedBody = wheelRigidbody;
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            joint.enableCollision = false;
            joint.enablePreprocessing = false;

            // 重要：アンカーの自動設定を無効化
            joint.autoConfigureConnectedAnchor = false;

            // アンカーポイントを明示的に設定
            Vector3 localAnchor = wheelRigidbody.transform.InverseTransformPoint(grabPoint.transform.position);
            joint.connectedAnchor = localAnchor;
            joint.anchor = Vector3.zero;
        }
    }

    // 即座にグラブポイントインタラクション設定
    void SetupGrabPointInteraction(IXRSelectInteractor interactor)
    {
        if (interactionManager != null && grabPoint != null)
        {
            XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                interactionManager.SelectEnter(interactor, grabInteractable);
            }
        }
    }

    // 遅延インタラクション設定
    IEnumerator DelayedInteractionSetup(IXRSelectInteractor interactor)
    {
        yield return null;

        if (interactionManager != null && grabPoint != null)
        {
            // 元のインタラクションをキャンセル
            var wheelInteractable = GetComponent<IXRSelectInteractable>();
            if (wheelInteractable != null)
            {
                interactionManager.CancelInteractableSelection(wheelInteractable);
            }

            // グラブポイントとのインタラクションを開始
            XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                interactionManager.SelectEnter(interactor, grabInteractable);
            }
        }
    }

    // グラブポイントのクリーンアップ
    public void CleanupGrabPoint()
    {
        if (grabPoint != null)
        {
            // インタラクションを終了
            CancelGrabPointInteraction();

            // オブジェクトを破棄
            DestroyImmediate(grabPoint);
            grabPoint = null;
        }
    }

    // グラブポイントインタラクション終了
    void CancelGrabPointInteraction()
    {
        if (grabPoint == null || interactionManager == null) return;

        XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null)
        {
            interactionManager.CancelInteractableSelection(grabInteractable as IXRSelectInteractable);
        }
    }

    // プロパティ
    public bool HasActiveGrabPoint => grabPoint != null;
    public GameObject CurrentGrabPoint => grabPoint;

    // 破棄時の処理
    void OnDestroy()
    {
        CleanupGrabPoint();
    }

    void OnDisable()
    {
        CleanupGrabPoint();
    }
}