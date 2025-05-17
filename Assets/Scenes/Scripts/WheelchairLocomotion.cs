using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(Rigidbody))]
public class WheelchairLocomotion : MonoBehaviour
{
    [Header("References")]
    public Rigidbody playerRigidbody;         // 車いす本体
    public Transform leftController;          // 左手コントローラ（Transform）
    public Transform rightController;         // 右手コントローラ（Transform）

    [Header("Input Actions")]
    public InputActionProperty leftGripAction;
    public InputActionProperty rightGripAction;

    [Header("Tuning")]
    public float moveForceMultiplier = 60f;   // 前進力の倍率
    public float turnTorqueMultiplier = 25f;  // 旋回トルク倍率
    public float maxMoveSpeed = 2f;           // 最高速度[m/s]

    /* 内部変数 */
    Vector3 prevLeftPosLocal, prevRightPosLocal;
    bool leftGripHeld, rightGripHeld;

    // Inspector で Rigidbody を入れ忘れた場合は自動取得
    void Reset() => playerRigidbody = GetComponent<Rigidbody>();

    void OnEnable()
    {
        if (playerRigidbody == null) playerRigidbody = GetComponent<Rigidbody>();

        leftGripAction.action.Enable();
        rightGripAction.action.Enable();

        leftGripAction.action.performed += _ => leftGripHeld = true;
        leftGripAction.action.canceled += _ => leftGripHeld = false;

        rightGripAction.action.performed += _ => rightGripHeld = true;
        rightGripAction.action.canceled += _ => rightGripHeld = false;

        prevLeftPosLocal = leftController.localPosition;
        prevRightPosLocal = rightController.localPosition;
    }

    void OnDisable()
    {
        leftGripAction.action.performed -= _ => leftGripHeld = true;
        leftGripAction.action.canceled -= _ => leftGripHeld = false;
        rightGripAction.action.performed -= _ => rightGripHeld = true;
        rightGripAction.action.canceled -= _ => rightGripHeld = false;

        leftGripAction.action.Disable();
        rightGripAction.action.Disable();
    }

    void FixedUpdate()
    {
        Vector3 leftDelta = Vector3.zero;
        Vector3 rightDelta = Vector3.zero;

        /* 左手の移動量 */
        if (leftGripHeld)
        {
            var now = leftController.localPosition;
            leftDelta = now - prevLeftPosLocal;
            prevLeftPosLocal = now;
        }
        else prevLeftPosLocal = leftController.localPosition;

        /* 右手の移動量 */
        if (rightGripHeld)
        {
            var now = rightController.localPosition;
            rightDelta = now - prevRightPosLocal;
            prevRightPosLocal = now;
        }
        else prevRightPosLocal = rightController.localPosition;

        float leftPush = -leftDelta.z;   // 前に押すと＋
        float rightPush = -rightDelta.z;

        ApplyLocomotion(leftPush, rightPush);
    }

    void ApplyLocomotion(float leftPush, float rightPush)
    {
        /* 1. 前進／後退 */
        float avgPush = (leftPush + rightPush) * 0.5f;

        if (avgPush != 0 && (playerRigidbody.linearVelocity.magnitude < maxMoveSpeed || avgPush < 0))
        {
            Vector3 force = playerRigidbody.transform.forward * avgPush * moveForceMultiplier;
            playerRigidbody.AddForce(force, ForceMode.Force);
        }

        /* 2. 旋回 */
        float turnAmount = (rightPush - leftPush) * 0.5f * turnTorqueMultiplier;
        if (turnAmount != 0)
        {
            playerRigidbody.AddTorque(Vector3.up * turnAmount, ForceMode.Force);
        }
    }
}