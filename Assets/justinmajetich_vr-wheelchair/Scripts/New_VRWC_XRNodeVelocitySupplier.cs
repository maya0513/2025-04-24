using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// 【最新OpenXR/Input System対応版】VRコントローラー速度情報提供クラス
/// 
/// 最新のInput System Actions およびOpenXR 1.8+に対応した速度追跡システム
/// 
/// 主要な機能:
/// - Input System Actionsベースの高精度速度追跡
/// - OpenXR Device Layoutサポート
/// - フォールバック機能付きの安定した速度取得
/// - パフォーマンス最適化された更新処理
/// 
/// 最新版での改善点:
/// - Input System Actionsの使用（InputDevices API廃止対応）
/// - OpenXR標準デバイスレイアウトサポート
/// - より高精度な速度計算
/// - エラーハンドリングとフォールバック機能の強化
/// - 設定可能な更新頻度とフィルタリング
/// </summary>
public class New_VRWC_XRNodeVelocitySupplier : MonoBehaviour
{
    [Header("追跡設定")]
    [SerializeField, Tooltip("速度を追跡するXRNode。LeftHandまたはRightHandを指定してください")]
    private XRNode trackedNode = XRNode.RightHand;

    [SerializeField, Tooltip("Input System Actionsを使用する（推奨）")]
    private bool useInputSystemActions = true;

    [Header("フィルタリング設定")]
    [SerializeField, Tooltip("速度の平滑化を有効にする")]
    private bool enableSmoothing = true;

    [Range(0.1f, 1.0f), Tooltip("平滑化の強度（低いほど滑らか、高いほど応答性が良い）")]
    [SerializeField] private float smoothingFactor = 0.3f;

    [Range(0.01f, 0.1f), Tooltip("速度更新の間隔（秒）")]
    [SerializeField] private float updateInterval = 0.02f; // 50Hz

    [Header("Input System Actions（推奨設定）")]
    [SerializeField, Tooltip("左手の位置入力アクション")]
    private InputActionProperty leftHandPositionAction;

    [SerializeField, Tooltip("右手の位置入力アクション")]
    private InputActionProperty rightHandPositionAction;

    [SerializeField, Tooltip("左手の速度入力アクション")]
    private InputActionProperty leftHandVelocityAction;

    [SerializeField, Tooltip("右手の速度入力アクション")]
    private InputActionProperty rightHandVelocityAction;

    [Header("デバッグ")]
    [SerializeField, Tooltip("デバッグ情報を表示する")]
    private bool showDebugInfo = false;

    // 内部状態管理
    private Vector3 _velocity = Vector3.zero;
    private Vector3 _smoothedVelocity = Vector3.zero;
    private Vector3 _lastPosition = Vector3.zero;
    private float _lastUpdateTime = 0f;
    
    // Input System関連
    private InputAction currentPositionAction;
    private InputAction currentVelocityAction;

    /// <summary>
    /// 追跡されたデバイスの最新の速度（読み取り専用）
    /// 
    /// 外部クラスからこのプロパティを参照することで、リアルタイムの手の速度を取得できます。
    /// 車輪操作の判定やブレーキアシストの制御に使用されます。
    /// 
    /// 最新版では平滑化フィルタリングにより、より安定した値を提供します。
    /// </summary>
    public Vector3 velocity => enableSmoothing ? _smoothedVelocity : _velocity;

    /// <summary>
    /// 生の速度値（フィルタリング前）
    /// デバッグや高精度が必要な場合に使用
    /// </summary>
    public Vector3 rawVelocity => _velocity;

    /// <summary>
    /// 追跡中のデバイスが有効かどうか
    /// </summary>
    public bool isDeviceValid { get; private set; } = false;

    /// <summary>
    /// 初期化処理（最新Input System対応）
    /// </summary>
    private void Start()
    {
        InitializeInputActions();
        InitializeTracking();
        
        if (showDebugInfo)
        {
            Debug.Log($"New_VRWC_XRNodeVelocitySupplier: 初期化完了 - 追跡ノード: {trackedNode}, Input System使用: {useInputSystemActions}");
        }
    }

    /// <summary>
    /// Input System Actionsの初期化
    /// </summary>
    private void InitializeInputActions()
    {
        if (!useInputSystemActions) return;

        try
        {
            // 追跡するノードに応じてアクションを設定
            if (trackedNode == XRNode.LeftHand)
            {
                currentPositionAction = leftHandPositionAction.action;
                currentVelocityAction = leftHandVelocityAction.action;
            }
            else if (trackedNode == XRNode.RightHand)
            {
                currentPositionAction = rightHandPositionAction.action;
                currentVelocityAction = rightHandVelocityAction.action;
            }

            // アクションの有効化
            if (currentPositionAction != null)
            {
                currentPositionAction.Enable();
            }
            
            if (currentVelocityAction != null)
            {
                currentVelocityAction.Enable();
            }

            if (showDebugInfo)
            {
                Debug.Log($"New_VRWC_XRNodeVelocitySupplier: Input System Actions初期化完了");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"New_VRWC_XRNodeVelocitySupplier: Input System Actions初期化失敗: {e.Message}");
            useInputSystemActions = false; // フォールバックモードに切り替え
        }
    }

    /// <summary>
    /// 追跡システムの初期化
    /// </summary>
    private void InitializeTracking()
    {
        // 初期位置の取得
        _lastPosition = GetCurrentPosition();
        _lastUpdateTime = Time.time;
    }

    /// <summary>
    /// 毎フレーム実行される速度更新処理（最新版）
    /// 
    /// 設定された更新間隔に基づいて速度を計算し、
    /// Input System ActionsまたはフォールバックのInputDevices APIを使用します。
    /// </summary>
    void Update()
    {
        // 更新間隔の制御
        if (Time.time - _lastUpdateTime < updateInterval)
            return;

        UpdateVelocity();
        _lastUpdateTime = Time.time;
    }

    /// <summary>
    /// 速度の更新処理
    /// </summary>
    private void UpdateVelocity()
    {
        Vector3 newVelocity = Vector3.zero;
        isDeviceValid = false;

        if (useInputSystemActions && currentVelocityAction != null)
        {
            // === Input System Actionsから直接速度を取得（推奨方法） ===
            try
            {
                newVelocity = currentVelocityAction.ReadValue<Vector3>();
                isDeviceValid = true;

                if (showDebugInfo)
                {
                    Debug.Log($"Input System速度: {newVelocity}");
                }
            }
            catch (System.Exception e)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"Input System速度取得失敗: {e.Message}");
                }
                // フォールバック処理へ
            }
        }

        // フォールバック: 位置から速度を計算
        if (!isDeviceValid)
        {
            newVelocity = CalculateVelocityFromPosition();
        }

        // フォールバック: 従来のInputDevices API
        if (!isDeviceValid && !useInputSystemActions)
        {
            newVelocity = GetVelocityFromInputDevices();
        }

        // 速度の更新
        _velocity = newVelocity;

        // 平滑化処理
        if (enableSmoothing)
        {
            _smoothedVelocity = Vector3.Lerp(_smoothedVelocity, _velocity, smoothingFactor);
        }
        else
        {
            _smoothedVelocity = _velocity;
        }
    }

    /// <summary>
    /// 位置の変化から速度を計算（高精度フォールバック）
    /// </summary>
    private Vector3 CalculateVelocityFromPosition()
    {
        Vector3 currentPosition = GetCurrentPosition();
        float deltaTime = Time.time - _lastUpdateTime;

        if (deltaTime > 0)
        {
            Vector3 calculatedVelocity = (currentPosition - _lastPosition) / deltaTime;
            _lastPosition = currentPosition;
            isDeviceValid = true;
            return calculatedVelocity;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// 現在の位置を取得
    /// </summary>
    private Vector3 GetCurrentPosition()
    {
        // Input System Actionsから位置を取得
        if (useInputSystemActions && currentPositionAction != null)
        {
            try
            {
                return currentPositionAction.ReadValue<Vector3>();
            }
            catch (System.Exception e)
            {
                if (showDebugInfo)
                {
                    Debug.LogWarning($"Input System位置取得失敗: {e.Message}");
                }
            }
        }

        // フォールバック: InputDevices API
        var device = InputDevices.GetDeviceAtXRNode(trackedNode);
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 position))
        {
            return position;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// 従来のInputDevices APIから速度を取得（フォールバック）
    /// </summary>
    private Vector3 GetVelocityFromInputDevices()
    {
        var device = InputDevices.GetDeviceAtXRNode(trackedNode);
        if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceVelocity, out Vector3 deviceVelocity))
        {
            isDeviceValid = true;
            return deviceVelocity;
        }

        return Vector3.zero;
    }

    /// <summary>
    /// コンポーネントが無効化される時の処理
    /// </summary>
    private void OnDisable()
    {
        // Input System Actionsの無効化
        if (currentPositionAction != null && currentPositionAction.enabled)
        {
            currentPositionAction.Disable();
        }

        if (currentVelocityAction != null && currentVelocityAction.enabled)
        {
            currentVelocityAction.Disable();
        }
    }

    /// <summary>
    /// デバッグ情報の表示
    /// </summary>
    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"追跡ノード: {trackedNode}");
        GUILayout.Label($"デバイス有効: {isDeviceValid}");
        GUILayout.Label($"生速度: {_velocity}");
        GUILayout.Label($"平滑化速度: {_smoothedVelocity}");
        GUILayout.Label($"Input System使用: {useInputSystemActions}");
        GUILayout.EndArea();
    }

    /// <summary>
    /// 追跡ノードを動的に変更
    /// </summary>
    public void SetTrackedNode(XRNode newNode)
    {
        if (trackedNode != newNode)
        {
            trackedNode = newNode;
            InitializeInputActions();
            InitializeTracking();
            
            if (showDebugInfo)
            {
                Debug.Log($"New_VRWC_XRNodeVelocitySupplier: 追跡ノード変更: {newNode}");
            }
        }
    }

    /// <summary>
    /// 平滑化設定の動的変更
    /// </summary>
    public void SetSmoothingSettings(bool enabled, float factor)
    {
        enableSmoothing = enabled;
        smoothingFactor = Mathf.Clamp01(factor);
    }
}