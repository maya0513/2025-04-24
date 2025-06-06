using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// VR車椅子の車輪操作システムのメインクラス
public class VRWheelInteractable : XRBaseInteractable
{
    // --- 設定パラメータ ---
    // インスペクターから設定可能な項目

    // 触覚フィードバック（ハプティクス）を有効にするかどうか
    [SerializeField] bool hapticsEnabled = true;
    // コントローラーが車輪からこの距離以上離れたら、掴んでいる状態を自動的に解除する
    [Range(0, 0.5f), Tooltip("車輪からの距離閾値")]
    [SerializeField] float deselectionThreshold = 0.25f;
    // 車輪の回転を止めるブレーキ力の強さ
    [Range(1f, 50f), Tooltip("ブレーキ力の強度")]
    [SerializeField] float brakeForce = 25f;
    // 車輪が「静止している」と見なされる速度の閾値。この値以下になると静止と判定される
    [Range(0.01f, 0.2f), Tooltip("静止判定の速度閾値")]
    [SerializeField] float stationaryThreshold = 0.05f;

    // --- コンポーネント参照 ---
    // このスクリプトが動作するために必要なUnityコンポーネント

    // 車輪の物理挙動を制御するためのRigidbody
    Rigidbody m_Rigidbody;
    // 車輪の半径。当たり判定や距離計算に使用
    float wheelRadius;

    // --- 機能別マネージャー ---
    // 各機能を専門に担当するクラスのインスタンス

    // 掴むポイント（グラブポイント）の生成と管理を担当
    WheelGrabPointManager grabPointManager;
    // ブレーキアシスト機能（手を離した時にブレーキをかけるなど）を担当
    WheelBrakeAssist brakeAssist;
    // 触覚フィードバックの生成と管理を担当
    WheelHapticFeedback hapticFeedback;
    // コントローラーと車輪の距離を監視し、離れすぎたら選択を解除する機能を担当
    WheelDistanceMonitor distanceMonitor;
    // 車椅子が坂道にいるかどうかを検出する機能を担当
    WheelSlopeDetector slopeDetector;

    // --- 内部状態 ---
    // 現在のインタラクション状態を保持する変数

    // 現在この車輪を掴んでいるインタラクター（VRコントローラー）
    IXRSelectInteractor currentInteractor;

    // --- デバッグ用UI ---
    // デバッグ情報を表示するためのUI要素

    public Text label1;
    public Text label2;

    // --- 初期化処理 ---

    // ゲーム開始時に一度だけ呼び出されるUnityの標準メソッド
    private void Start()
    {
        // 必要なコンポーネントを初期化
        InitializeComponents();
        // 各機能マネージャーを初期化
        InitializeManagers();
    }

    // このオブジェクトに必要なコンポーネントを取得し、初期設定を行う
    void InitializeComponents()
    {
        // 自分自身（このスクリプトがアタッチされているGameObject）からRigidbodyコンポーネントを取得
        m_Rigidbody = GetComponent<Rigidbody>();

        // Rigidbodyが見つからない場合は、エラーメッセージをログに出力して処理を中断
        if (m_Rigidbody == null)
        {
            Debug.LogError($"[{gameObject.name}] Rigidbodyコンポーネントが見つかりません。");
            return;
        }

        // SphereColliderコンポーネントを取得
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        if (sphereCollider != null)
        {
            // SphereColliderがあれば、その半径を車輪の半径として使用
            wheelRadius = sphereCollider.radius;
        }
        else
        {
            // SphereColliderが見つからない場合は、警告を出し、デフォルトの半径を使用
            Debug.LogWarning($"[{gameObject.name}] SphereColliderが見つかりません。デフォルト半径0.5を使用します。");
            wheelRadius = 0.5f;
        }
    }

    // 各機能マネージャーのインスタンスを生成または取得し、初期化する
    void InitializeManagers()
    {
        // 各マネージャーコンポーネントをGameObjectから取得。もし存在しなければ、新しく追加する。
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

        // 各マネージャーに、動作に必要な情報（Rigidbodyなど）を渡して初期化処理を呼び出す
        grabPointManager.Initialize(m_Rigidbody, interactionManager);
        brakeAssist.Initialize(m_Rigidbody, stationaryThreshold, brakeForce);
        hapticFeedback.Initialize(m_Rigidbody, transform);
        distanceMonitor.Initialize(wheelRadius, deselectionThreshold, interactionManager);
        slopeDetector.Initialize(wheelRadius, transform);
    }

    // --- インタラクションイベント処理 ---

    // VRコントローラーがこのオブジェクトを選択（掴んだ）したときに呼び出される
    protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
    {
        // 親クラスのOnSelectEntered処理を呼び出す（XR Interaction Toolkitの標準的な処理）
        base.OnSelectEntered(eventArgs);

        // どのインタラクター（コントローラー）が掴んだかを取得
        IXRSelectInteractor interactor = eventArgs.interactorObject;
        // 現在のインタラクターとして保持
        currentInteractor = interactor;

        // 前回の操作が残っている場合に備えて、関連する処理を一旦クリーンアップする
        CleanupCurrentSession();

        // 掴んだ後のセットアップ処理をコルーチンとして開始する（1フレーム遅延させるため）
        StartCoroutine(DelayedGrabSetup(interactor));
    }

    // VRコントローラーがこのオブジェクトの選択を解除（離した）したときに呼び出される
    protected override void OnSelectExited(SelectExitEventArgs eventArgs)
    {
        // 親クラスのOnSelectExited処理を呼び出す
        base.OnSelectExited(eventArgs);
        // 掴んでいる状態に関連するすべての処理を停止・クリーンアップする
        CleanupCurrentSession();
        // 現在のインタラクター情報をリセット
        currentInteractor = null;
    }

    // 掴んだ直後のセットアップを1フレーム遅らせて実行するコルーチン
    IEnumerator DelayedGrabSetup(IXRSelectInteractor interactor)
    {
        // 1フレーム待機する。これにより、XR Interaction Toolkitの標準的な選択処理が完了するのを待つ。
        yield return null;

        // XR Interaction Toolkitの標準の選択処理をキャンセルする。
        // これにより、オブジェクトがコントローラーに追従するデフォルトの挙動を防ぎ、
        // このスクリプト独自のカスタムな掴み処理に切り替えることができる。
        if (interactionManager != null)
        {
            interactionManager.CancelInteractableSelection(this as IXRSelectInteractable);
        }

        // カスタムの掴み処理を開始。コントローラーの位置に追従する「グラブポイント」を生成する。
        grabPointManager.SpawnGrabPointImmediate(interactor, transform);

        // 各機能マネージャーの処理を開始する
        brakeAssist.StartBrakeAssist(interactor, grabPointManager);
        distanceMonitor.StartDistanceMonitoring(interactor, grabPointManager, transform);

        // ハプティクスが有効な場合、触覚フィードバックの処理を開始する
        if (hapticsEnabled)
        {
            hapticFeedback.StartHapticFeedback(interactor, grabPointManager);
        }
    }

    // --- クリーンアップ処理 ---

    // 現在のインタラクション（掴んでいる状態）に関連するすべての処理を停止・リセットする
    void CleanupCurrentSession()
    {
        // 各マネージャーの停止/クリーンアップ処理を呼び出す
        // `?.` (null条件演算子) を使うことで、マネージャーがnullの場合でもエラーにならずに安全に呼び出せる
        grabPointManager?.CleanupGrabPoint();
        brakeAssist?.StopBrakeAssist();
        hapticFeedback?.StopHapticFeedback();
        distanceMonitor?.StopDistanceMonitoring();
    }

    // --- プロパティ (外部から値を取得するための窓口) ---

    // 車椅子が坂道にいるかどうかを返す
    public bool OnSlope => slopeDetector != null ? slopeDetector.OnSlope : false;
    // 現在この車輪を操作しているインタラクターを返す
    public IXRSelectInteractor CurrentInteractor => currentInteractor;

    // --- Unityライフサイクルメソッド ---

    // このオブジェクトが破棄される時に呼び出される
    protected override void OnDestroy()
    {
        base.OnDestroy();
        // 意図しない処理が残らないように、クリーンアップ処理を実行
        CleanupCurrentSession();
    }

    // このコンポーネントが無効になった時に呼び出される
    protected override void OnDisable()
    {
        base.OnDisable();
        // 同様に、クリーンアップ処理を実行して状態をリセット
        CleanupCurrentSession();
        currentInteractor = null;
    }
}