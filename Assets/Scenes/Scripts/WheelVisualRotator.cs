using UnityEngine;

public class WheelVisualRotator : MonoBehaviour
{


    public Rigidbody parentRigidbody; // 車椅子本体（親）のRigidbody

    public Transform wheelMeshTransform; // 回転させる車輪のメッシュのTransform



    public float wheelRadius = 0.3f; // タイヤの半径 (メートル)

    public Vector3 localRotationAxis = Vector3.right; // 車輪の回転軸 (モデルのローカル座標系)

    void Update()
    {
        // 必要な参照が設定されていない場合や、半径が不正な場合は処理を中断
        if (parentRigidbody == null || wheelMeshTransform == null || wheelRadius <= 0f)
        {
            if (parentRigidbody == null) Debug.LogWarning("Parent Rigidbodyが設定されていません。", this);
            if (wheelMeshTransform == null) Debug.LogWarning("Wheel Mesh Transformが設定されていません。", this);
            if (wheelRadius <= 0f) Debug.LogWarning("Wheel Radiusが0以下です。", this);
            return;
        }

        // 1. Rigidbodyのワールド空間での速度を取得
        Vector3 worldVelocity = parentRigidbody.linearVelocity;
        // Vector3 worldVelocity = parentRigidbody.velocity; // 旧形式
        // Unity 2022以降ではlinearVelocityを推奨
        // Vector3 worldVelocity = parentRigidbody.linearVelocity;

        // 2. Rigidbodyのローカル座標系での前方速度を計算
        //    (車椅子がどの方向を向いていても、その前進方向の速度成分を取り出す)
        Vector3 localVelocity = parentRigidbody.transform.InverseTransformDirection(worldVelocity);
        float forwardSpeed = localVelocity.z; // RigidbodyのローカルZ軸が前方であると仮定

        // 3. 車輪の角速度を計算 (ラジアン/秒)
        //    角速度 = 並進速度 / 半径
        float angularVelocity_rad_per_sec = forwardSpeed / wheelRadius;

        // 4. このフレームでの回転角度を計算 (度)
        //    回転角度 = 角速度 * 時間 * (ラジアンから度への変換係数)
        float rotationAngle_deg_this_frame = angularVelocity_rad_per_sec * Mathf.Rad2Deg * Time.deltaTime;

        // 5. 車輪メッシュをローカル軸周りに回転させる
        //    wheelMeshTransform.Rotate(軸,角度,どの座標系で回転するか)
        //    Space.Self を指定することで、wheelMeshTransform自身のローカル軸で回転します。
        wheelMeshTransform.Rotate(localRotationAxis, rotationAngle_deg_this_frame, Space.Self);
    }
}