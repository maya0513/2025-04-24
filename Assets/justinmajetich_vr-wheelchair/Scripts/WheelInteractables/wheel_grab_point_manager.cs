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
        if (interactorTransform != null && grabPoint != null)
        {
            grabPoint.transform.position = interactorTransform.position;
        }
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