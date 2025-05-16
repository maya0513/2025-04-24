using UnityEngine;
using UnityEngine.InputSystem; // Unityの新しいInput Systemを利用するために必要
using UnityEngine.XR.Interaction.Toolkit; // XRControllerコンポーネントの型を参照するために必要 [14]

public class WheelchairLocomotion : MonoBehaviour
{


    public Rigidbody playerRigidbody;

    public Transform leftController;

    public Transform rightController;

    [Header("Input Actions")]

    public InputActionProperty leftGripAction; // 左手グリップボタンの状態を取得するためのアクション

    public InputActionProperty rightGripAction; // 右手グリップボタンの状態を取得するためのアクション



    public float moveForceMultiplier = 10f;

    public float turnTorqueMultiplier = 5f;

    public float maxMoveSpeed = 2f;
    // public float wheelRadius = 0.3f; // タイヤの半径。移動量計算や回転アニメーションで使用可能

    // 内部で使用する変数
    private Vector3 prevLeftControllerPos_Local;  // 前フレームの左コントローラーのローカル座標
    private Vector3 prevRightControllerPos_Local; // 前フレームの右コントローラーのローカル座標
    private bool isLeftGripPressed = false;      // 左グリップが押されているか
    private bool isRightGripPressed = false;     // 右グリップが押されているか

    void OnEnable()
    {
        // 必要なコンポーネントやアクションが設定されているか確認
        if (playerRigidbody == null || leftController == null || rightController == null || leftGripAction.action == null || rightGripAction.action == null)
        {
            Debug.LogError("WheelchairLocomotion: 必要なコンポーネントまたはInput ActionがInspectorで設定されていません。スクリプトを無効にします。");
            enabled = false; // スクリプトを無効化
            return;
        }

        // 左手グリップアクションのイベント購読
        leftGripAction.action.Enable(); // アクションを有効化
        leftGripAction.action.performed += OnLeftGripPerformed; // ボタンが押された時のイベント
        leftGripAction.action.canceled += OnLeftGripCanceled;   // ボタンが離された時のイベント

        // 右手グリップアクションのイベント購読
        rightGripAction.action.Enable(); // アクションを有効化
        rightGripAction.action.performed += OnRightGripPerformed; // ボタンが押された時のイベント
        rightGripAction.action.canceled += OnRightGripCanceled;   // ボタンが離された時のイベント

        // スクリプト有効化時にコントローラーの初期ローカル位置を記録
        prevLeftControllerPos_Local = leftController.transform.localPosition;
        prevRightControllerPos_Local = rightController.transform.localPosition;
    }

    void OnDisable()
    {
        // 左手グリップアクションのイベント購読解除
        if (leftGripAction.action != null)
        {
            leftGripAction.action.performed -= OnLeftGripPerformed;
            leftGripAction.action.canceled -= OnLeftGripCanceled;
            leftGripAction.action.Disable(); // アクションを無効化
        }

        // 右手グリップアクションのイベント購読解除
        if (rightGripAction.action != null)
        {
            rightGripAction.action.performed -= OnRightGripPerformed;
            rightGripAction.action.canceled -= OnRightGripCanceled;
            rightGripAction.action.Disable(); // アクションを無効化
        }
    }

    // 左手グリップが押された時に呼び出されるメソッド
    private void OnLeftGripPerformed(InputAction.CallbackContext context)
    {
        isLeftGripPressed = true;
        // グリップ開始時のコントローラーローカル位置を記録
        prevLeftControllerPos_Local = leftController.transform.localPosition;
    }

    // 左手グリップが離された時に呼び出されるメソッド
    private void OnLeftGripCanceled(InputAction.CallbackContext context)
    {
        isLeftGripPressed = false;
    }

    // 右手グリップが押された時に呼び出されるメソッド
    private void OnRightGripPerformed(InputAction.CallbackContext context)
    {
        isRightGripPressed = true;
        // グリップ開始時のコントローラーローカル位置を記録
        prevRightControllerPos_Local = rightController.transform.localPosition;
    }

    // 右手グリップが離された時に呼び出されるメソッド
    private void OnRightGripCanceled(InputAction.CallbackContext context)
    {
        isRightGripPressed = false;
    }

    void FixedUpdate()
    {
        Vector3 leftControllerDelta_Local = Vector3.zero;
        Vector3 rightControllerDelta_Local = Vector3.zero;

        // 左手コントローラーの移動量（ローカル座標系）を取得
        if (isLeftGripPressed)
        {
            Vector3 currentLeftPos_Local = leftController.transform.localPosition;
            leftControllerDelta_Local = currentLeftPos_Local - prevLeftControllerPos_Local;
            prevLeftControllerPos_Local = currentLeftPos_Local; // 次のフレームのために現在位置を保存
        }
        else
        {
            // グリップが離されている間も、不意の大きな差分を防ぐために位置を更新し続ける
            prevLeftControllerPos_Local = leftController.transform.localPosition;
        }

        // 右手コントローラーの移動量（ローカル座標系）を取得
        if (isRightGripPressed)
        {
            Vector3 currentRightPos_Local = rightController.transform.localPosition;
            rightControllerDelta_Local = currentRightPos_Local - prevRightControllerPos_Local;
            prevRightControllerPos_Local = currentRightPos_Local; // 次のフレームのために現在位置を保存
        }
        else
        {
            prevRightControllerPos_Local = rightController.transform.localPosition;
        }

        // タイヤを漕ぐ動作を検出
        // コントローラーのローカルZ軸（前後）の動きを「押し出す力」として利用
        // 手を前に押し出すとプラス、後ろに引くとマイナスになるように調整
        float leftWheelPush = -leftControllerDelta_Local.z;
        float rightWheelPush = -rightControllerDelta_Local.z;
        // Y軸（上下）の動きも考慮に入れる場合は、以下のように加算することも可能
        // leftWheelPush += leftControllerDelta_Local.y * 0.5f; // 例: 上下動の寄与度を半分にする
        // rightWheelPush += rightControllerDelta_Local.y * 0.5f;

        // デバッグ用にコンソールに出力（テスト時に便利）
        // Debug.Log($"Left Push: {leftWheelPush:F3}, Right Push: {rightWheelPush:F3}, Left Delta: {leftControllerDelta_Local}, Right Delta: {rightControllerDelta_Local}");

        // 計算された押し出す力に基づいて、車椅子を動かす
        ApplyLocomotion(leftWheelPush, rightWheelPush);
    }

    void ApplyLocomotion(float leftPush, float rightPush)
    {
        // 1. 前進・後退力の計算
        // 左右の押し出す力の平均を取り、全体の推進力とする
        float averagePush = (leftPush + rightPush) * 0.5f;
        Vector3 moveDirection = playerRigidbody.transform.forward * averagePush * moveForceMultiplier;

        // 最高速度制限 (オプション)
        // 現在の速度が最高速度未満の場合、または後退しようとしている場合に力を加える
        if (playerRigidbody.linearVelocity.magnitude < maxMoveSpeed || averagePush < 0)
        {
            playerRigidbody.AddForce(moveDirection, ForceMode.Force);
            // ForceMode.Force は質量に応じて継続的な力を加える [27]
        }

        // 2. 旋回トルクの計算
        // 左右の押し出す力の差を取り、旋回力とする
        // 例: 右手で強く押す (rightPush > leftPush) と、車体は左に旋回する
        float turnDifference = (rightPush - leftPush) * 0.5f; // 0.5を掛けて感度を調整
        Vector3 turnAxis = playerRigidbody.transform.up; // Y軸周りに回転
        float turnAmount = turnDifference * turnTorqueMultiplier;

        playerRigidbody.AddTorque(turnAxis * turnAmount, ForceMode.Force);
        // ForceMode.Force は質量に応じて継続的なトルクを加える [26]
    }
}