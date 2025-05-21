// ファイル名: WheelchairPhysicsController.cs
using UnityEngine;


public class WheelchairPhysicsController : MonoBehaviour
{
    [Header("Wheel Colliders")]

    public WheelCollider leftWheelCollider;

    public WheelCollider rightWheelCollider;

    [Header("Movement Parameters")]

    public float maxMotorTorque = 100f;

    public float maxBrakeTorque = 200f;



    public Vector3 centerOfMassOffset = new Vector3(0f, -0.2f, 0.05f); // Yを低く、Zをわずかに前方に

    // 内部状態変数
    private float currentLeftMotorInput = 0f;
    private float currentRightMotorInput = 0f;
    private float currentLeftBrakeInput = 0f;
    private float currentRightBrakeInput = 0f;

    private Rigidbody wheelchairRigidbody;

    void Start()
    {
        wheelchairRigidbody = GetComponent<Rigidbody>();
        if (wheelchairRigidbody == null)
        {
            Debug.LogError("WheelchairPhysicsController: Rigidbodyが見つかりません。", this.gameObject);
            this.enabled = false;
            return;
        }

        // 質量中心を手動で設定
        wheelchairRigidbody.centerOfMass = centerOfMassOffset;

        // WheelColliderの基本的な検証
        if (leftWheelCollider == null || rightWheelCollider == null)
        {
            Debug.LogError("WheelchairPhysicsController: WheelColliderが割り当てられていません。", this.gameObject);
            this.enabled = false;
        }
    }

    // 外部からモーター入力を設定するためのメソッド
    // leftInput, rightInput: -1 (後退) から 1 (前進)
    public void SetMotorInput(float leftInput, float rightInput)
    {
        currentLeftMotorInput = Mathf.Clamp(leftInput, -1f, 1f);
        currentRightMotorInput = Mathf.Clamp(rightInput, -1f, 1f);
    }

    // 外部からブレーキ入力を設定するためのメソッド
    // leftInput, rightInput: 0 (ブレーキなし) から 1 (最大ブレーキ)
    public void SetBrakeInput(float leftInput, float rightInput)
    {
        currentLeftBrakeInput = Mathf.Clamp01(leftInput);
        currentRightBrakeInput = Mathf.Clamp01(rightInput);
    }

    // FixedUpdateは物理演算の更新と同期して呼び出されます
    void FixedUpdate()
    {
        if (leftWheelCollider == null || rightWheelCollider == null || wheelchairRigidbody == null) return;

        // モータートルクの適用
        // ブレーキが適用されている場合はモータートルクをゼロにするか、あるいはより複雑なロジックを実装することも可能
        float actualLeftMotorTorque = (currentLeftBrakeInput > 0.01f) ? 0f : currentLeftMotorInput * maxMotorTorque;
        float actualRightMotorTorque = (currentRightBrakeInput > 0.01f) ? 0f : currentRightMotorInput * maxMotorTorque;

        leftWheelCollider.motorTorque = actualLeftMotorTorque;
        rightWheelCollider.motorTorque = actualRightMotorTorque;

        // ブレーキトルクの適用
        // モーターが逆方向に駆動されている場合、ブレーキはより効果的に働く（あるいは、ここでは単純に設定されたブレーキ値を適用）
        leftWheelCollider.brakeTorque = currentLeftBrakeInput * maxBrakeTorque;
        rightWheelCollider.brakeTorque = currentRightBrakeInput * maxBrakeTorque;
    }

    // Inspectorで質量中心のオフセットを変更したときに、リアルタイムで適用するためのGizmo
    void OnDrawGizmosSelected()
    {
        if (wheelchairRigidbody == null)
        {
            wheelchairRigidbody = GetComponent<Rigidbody>();
            if (wheelchairRigidbody == null) return;
        }
        // 質量中心をワールド空間で表示
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.TransformPoint(centerOfMassOffset), 0.1f);

        // エディタ実行中でなくても質量中心を更新（エディタ上での調整のため）
        if (!Application.isPlaying)
        {
            wheelchairRigidbody.centerOfMass = centerOfMassOffset;
        }
    }
}