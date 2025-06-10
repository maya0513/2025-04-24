using UnityEngine;

// ターゲットトランスフォームとの一定のオフセットを維持するために、トランスフォームの位置と回転を変更します。
// 階層内で兄弟である 2 つのオブジェクトの位置/回転を同期するのに役立ちます

public class VRWC_FollowTransform : MonoBehaviour
{
    [Tooltip("追従するリジッドボディの変換")]
    public Transform target;
    Vector3 offset;

    void Start()
    {
        offset = transform.localPosition - target.localPosition;
    }

    void Update()
    {
        Vector3 rotatedOffset = target.localRotation * offset;
        transform.localPosition = target.localPosition + rotatedOffset;

        transform.rotation = target.rotation;
    }
}
