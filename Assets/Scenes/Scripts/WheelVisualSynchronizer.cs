// ファイル名: WheelVisualSynchronizer.cs
using UnityEngine;

public class WheelVisualSynchronizer : MonoBehaviour
{
   
    public WheelCollider wheelCollider;

   
    public Transform wheelVisualTransform;

    void Start()
    {
        // 基本的な検証
        if (wheelCollider == null)
        {
            Debug.LogError("WheelVisualSynchronizer: WheelColliderが割り当てられていません。", this.gameObject);
            this.enabled = false; // 設定されていない場合はスクリプトを無効にする
            return;
        }
        if (wheelVisualTransform == null)
        {
            Debug.LogError("WheelVisualSynchronizer: WheelVisualTransformが割り当てられていません。", this.gameObject);
            this.enabled = false; // 設定されていない場合はスクリプトを無効にする
            return;
        }
    }

    // LateUpdateは、すべてのUpdate関数が呼び出された後、
    // そしてFixedUpdateでの物理更新の後に呼び出されます。
    // これは、物理に基づいてビジュアルを更新するのに理想的な場所です。
    void LateUpdate()
    {
        if (wheelCollider == null |
| wheelVisualTransform == null) return;

        Vector3 worldPosition;
        Quaternion worldRotation;

        // WheelColliderの現在のワールドポーズ（位置と回転）を取得
        wheelCollider.GetWorldPose(out worldPosition, out worldRotation);

        // このポーズをビジュアルホイールのトランスフォームに適用
        wheelVisualTransform.position = worldPosition;
        wheelVisualTransform.rotation = worldRotation;
    }
}