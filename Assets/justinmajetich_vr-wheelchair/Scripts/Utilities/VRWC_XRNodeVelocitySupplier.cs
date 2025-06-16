using System.Collections;
using UnityEngine;
using UnityEngine.XR;

// 特定のXRNodeの現在の速度を提供します。このクラスは、XRコントローラーに速度入力を提供することで、ActionBasedControllerを補完することを目的としています。

namespace JustinMajetich.VRWheelchair
{
    public class VRWC_XRNodeVelocitySupplier : MonoBehaviour
{
        [SerializeField, Tooltip("速度を追跡するXRNode。これはLeftHandまたはRightHandである必要があります。")]
        XRNode trackedNode;

        [Header("Initialization Settings")]
        [SerializeField] private float initializationTimeout = 10f;

        Vector3 _velocity = Vector3.zero;
        bool _isInitialized = false;

        // アタッチされたトランスフォームの最後に追跡された速度。読み取り専用。

        public Vector3 velocity { get => _velocity; }
        
        // XRシステムの初期化が完了しているかどうかを示すフラグ。読み取り専用。
        public bool IsInitialized { get => _isInitialized; }

        private void Start()
        {
            StartCoroutine(WaitForXRInitialization());
        }

        private IEnumerator WaitForXRInitialization()
        {
            // XR初期化マネージャーの完了待機
            yield return new WaitUntil(() => VRWC_XRInitializationManager.IsXRFullyInitialized);
            
            // デバイス有効性の再確認とタイムアウト
            float startTime = Time.time;
            yield return new WaitUntil(() => 
            {
                if (Time.time - startTime > initializationTimeout)
                {
                    Debug.LogError($"XR Device for {trackedNode} failed to initialize within timeout of {initializationTimeout}s");
                    return true;
                }
                return InputDevices.GetDeviceAtXRNode(trackedNode).isValid;
            });
            
            if (Time.time - startTime > initializationTimeout)
            {
                Debug.LogError($"Timeout exceeded for {trackedNode} initialization - proceeding without velocity tracking");
                yield break;
            }
            
            // 初期化完了後に速度取得を開始
            InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
            
            // 初期化完了フラグを設定
            _isInitialized = true;
            Debug.Log($"VRWC_XRNodeVelocitySupplier for {trackedNode} initialized successfully");
        }

        void Update()
        {
            if (_isInitialized)
            {
                InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
            }
        }
    }
}
