using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// VR車椅子の車輪操作システムのメインクラス
public class VRWheelInteractable : XRBaseInteractable
{
    // 設定パラメータ
    [SerializeField] bool hapticsEnabled = true;
    [Range(0, 0.5f), Tooltip("車輪からの距離閾値")]
    [SerializeField] float deselectionThreshold = 0.25f;
    [Range(1f, 50f), Tooltip("ブレーキ力の強度")]
    [SerializeField] float brakeForce = 25f;
    [Range(0.01f, 0.2f), Tooltip("静止判定の速度閾値")]
    [SerializeField] float stationaryThreshold = 0.05f;

    // コンポーネント
    Rigidbody m_Rigidbody;
    float wheelRadius;

    // 機能別マネージャー
    WheelGrabPointManager grabPointManager;
    WheelBrakeAssist brakeAssist;
    WheelHapticFeedback hapticFeedback;
    WheelDistanceMonitor distanceMonitor;
    WheelSlopeDetector slopeDetector;

    // 現在のインタラクター
    IXRSelectInteractor currentInteractor;

    // デバッグ用UI
    public Text label1;
    public Text label2;

    // 初期化
    private void Start()
    {
        InitializeComponents();
        InitializeManagers();
    }

    // コンポーネント初期化
    void InitializeComponents()
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
    }

    // マネージャー初期化
    void InitializeManagers()
    {
        grabPointManager = gameObject.GetComponent<WheelGrabPointManager>();
        if (grabPointManager == null)
            grabPointManager = gameObject.AddComponent<WheelGrabPointManager>();

        brakeAssist = gameObject.GetComponent<WheelBrakeAssist>();
        if (brakeAssist == null)
            brakeAssist = gameObject.AddComponent<WheelBrakeAssist>();

        hapticFeedback = gameObject.GetComponent<WheelHapticFeedback>();
        if (hapticFeedback == null)
            hapticFeedback = gameObject.AddComponent<WheelHapticFeedback>();

        distanceMonitor = gameObject.GetComponent<WheelDistanceMonitor>();
        if (distanceMonitor == null)
            distanceMonitor = gameObject.AddComponent<WheelDistanceMonitor>();

        slopeDetector = gameObject.GetComponent<WheelSlopeDetector>();
        if (slopeDetector == null)
            slopeDetector = gameObject.AddComponent<WheelSlopeDetector>();

        // マネージャーの初期化
        grabPointManager.Initialize(m_Rigidbody, interactionManager);
        brakeAssist.Initialize(m_Rigidbody, stationaryThreshold, brakeForce);
        hapticFeedback.Initialize(m_Rigidbody, transform);
        distanceMonitor.Initialize(wheelRadius, deselectionThreshold, interactionManager);
        slopeDetector.Initialize(wheelRadius, transform);
    }

    // 選択開始時の処理
    protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
    {
        base.OnSelectEntered(eventArgs);

        IXRSelectInteractor interactor = eventArgs.interactorObject;
        currentInteractor = interactor;

        // 前回のセッションをクリーンアップ
        CleanupCurrentSession();

        // 遅延セットアップを開始（元のコードと同じタイミング）
        StartCoroutine(DelayedGrabSetup(interactor));
    }

    // 選択終了時の処理
    protected override void OnSelectExited(SelectExitEventArgs eventArgs)
    {
        base.OnSelectExited(eventArgs);
        CleanupCurrentSession();
        currentInteractor = null;
    }

    // 遅延グラブセットアップ（元のコードと同じ処理フロー）
    IEnumerator DelayedGrabSetup(IXRSelectInteractor interactor)
    {
        yield return null;

        // 元のインタラクションをキャンセル
        if (interactionManager != null)
        {
            interactionManager.CancelInteractableSelection(this as IXRSelectInteractable);
        }

        // グラブポイント生成（即座にインタラクション開始）
        grabPointManager.SpawnGrabPointImmediate(interactor, transform);

        // 各機能を開始
        brakeAssist.StartBrakeAssist(interactor, grabPointManager);
        distanceMonitor.StartDistanceMonitoring(interactor, grabPointManager, transform);

        if (hapticsEnabled)
        {
            hapticFeedback.StartHapticFeedback(interactor, grabPointManager);
        }
    }

    // 現在のセッションクリーンアップ
    void CleanupCurrentSession()
    {
        grabPointManager?.CleanupGrabPoint();
        brakeAssist?.StopBrakeAssist();
        hapticFeedback?.StopHapticFeedback();
        distanceMonitor?.StopDistanceMonitoring();
    }

    // プロパティ
    public bool OnSlope => slopeDetector != null ? slopeDetector.OnSlope : false;
    public IXRSelectInteractor CurrentInteractor => currentInteractor;

    // クリーンアップ処理
    protected override void OnDestroy()
    {
        base.OnDestroy();
        CleanupCurrentSession();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        CleanupCurrentSession();
        currentInteractor = null;
    }
}