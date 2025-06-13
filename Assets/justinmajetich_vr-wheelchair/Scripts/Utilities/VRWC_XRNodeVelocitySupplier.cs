using System.Collections;
using UnityEngine;
using UnityEngine.XR;

// 特定のXRNodeの現在の速度を提供します。このクラスは、XRコントローラーに速度入力を提供することで、ActionBasedControllerを補完することを目的としています。

public class VRWC_XRNodeVelocitySupplier : MonoBehaviour
{
    [SerializeField, Tooltip("速度を追跡するXRNode。これはLeftHandまたはRightHandである必要があります。")]
    XRNode trackedNode;

    Vector3 _velocity = Vector3.zero;

    // アタッチされたトランスフォームの最後に追跡された速度。読み取り専用。

    public Vector3 velocity { get => _velocity; }

    private void Start()
    {
        InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
        StartCoroutine(WaitForXRInitialization());
    }

    private IEnumerator WaitForXRInitialization()
    {
        yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
        // 速度取得処理
    }

    void Update()
    {
        InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
    }
}
