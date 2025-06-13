using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// XRBaseInteractableを継承したVRWC_WheelInteractableは、動的グラブポイント、ブレーキング、自動選択解除を処理する独自の動作を提供します。

public class VRWC_WheelInteractable : XRBaseInteractable
{
    Rigidbody m_Rigidbody;

    float wheelRadius;

    bool onSlope = false;
    [SerializeField] bool hapticsEnabled = true;

    [Range(0, 0.5f), Tooltip("インタラクションマネージャーが選択をキャンセルするホイールコライダーからの距離。")]
    [SerializeField] float deselectionThreshold = 0.25f;

    GameObject grabPoint;

    public Text label1;
    public Text label2;


    private void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        wheelRadius = GetComponent<SphereCollider>().radius;

        // 斜面チェックは最適化された間隔でコルーチンで実行される
        StartCoroutine(CheckForSlope());
    }

    protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
    {
        base.OnSelectEntered(eventArgs);

        // 短い遅延後にブレーキアシストを開始
        StartCoroutine(DelayedBrakeAssist(eventArgs.interactorObject));

        // XRI v3ではinteractorObjectを使用
        IXRSelectInteractor interactor = eventArgs.interactorObject;

        // このホイールオブジェクトとの選択を強制的にキャンセル
        interactionManager.CancelInteractableSelection(this as IXRSelectInteractable);

        SpawnGrabPoint(interactor);

        StartCoroutine(BrakeAssist(interactor));
        StartCoroutine(MonitorDetachDistance(interactor));

        if (hapticsEnabled)
        {
            StartCoroutine(SendHapticFeedback(interactor));
        }
    }

    private IEnumerator DelayedBrakeAssist(IXRSelectInteractor interactor)
    {
        yield return new WaitForSeconds(0.1f); // XRシステム安定化待ち
        StartCoroutine(BrakeAssist(interactor));
    }

    // ホイールのリジッドボディとの物理インタラクションを仲介するグラブポイントを生成します。この「グラブ
    // ポイント」にはXRGrabInteractableコンポーネントとリジッドボディが含まれています。Fixed Jointを使用してホイールに結合されます。
    // <param name="interactor">選択を行っているインタラクター。</param>
    void SpawnGrabPoint(IXRSelectInteractor interactor)
    {
        // アクティブなグラブポイントがある場合、選択をキャンセル
        if (grabPoint)
        {
            var grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
            if (grabInteractable != null)
            {
                interactionManager.CancelInteractableSelection(grabInteractable as IXRSelectInteractable);
            }
        }

        // インタラクターの位置に新しいグラブポイントをインスタンス化
        grabPoint = new GameObject($"{transform.name}'s grabPoint", typeof(VRWC_GrabPoint), typeof(Rigidbody), typeof(FixedJoint));

        grabPoint.transform.position = interactor.transform.position;

        // Fixed Jointを使用してグラブポイントをこのホイールに取り付け
        grabPoint.GetComponent<FixedJoint>().connectedBody = GetComponent<Rigidbody>();

        // 現在のインタラクターと新しいグラブポイント間の選択を強制
        var grabPointInteractable = grabPoint.GetComponent<XRGrabInteractable>();
        if (grabPointInteractable != null)
        {
            // XRI v3ではSelectEnterを使用
            interactionManager.SelectEnter(interactor, grabPointInteractable as IXRSelectInteractable);
        }
    }

    IEnumerator BrakeAssist(IXRSelectInteractor interactor)
    {
        VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();

        if (interactorVelocity == null)
        {
            yield break; // nullの場合は早期終了
        }

        while (grabPoint)
        {
            // インタラクターの前後の動きがほぼゼロの場合、ブレーキをかけていると判断
            if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
            {
                m_Rigidbody.AddTorque(-m_Rigidbody.angularVelocity.normalized * 25f);

                SpawnGrabPoint(interactor);
            }

            yield return new WaitForFixedUpdate();
        }
    }

    IEnumerator MonitorDetachDistance(IXRSelectInteractor interactor)
    {
        // このホイールにアクティブなグラブポイントがある間
        while (grabPoint)
        {
            // インタラクターがホイールから閾値距離を超えて離れた場合、強制的に選択解除
            if (Vector3.Distance(transform.position, interactor.transform.position) >= wheelRadius + deselectionThreshold)
            {
                interactionManager.CancelInteractorSelection(interactor);
            }

            yield return null;
        }
    }

    IEnumerator SendHapticFeedback(IXRSelectInteractor interactor)
    {
        // コルーチンの反復間隔（秒）
        float runInterval = 0.1f;

        // インタラクターからInputDeviceを取得
        InputDevice device = GetInputDeviceFromInteractor(interactor);

        Vector3 lastAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);

        while (grabPoint)
        {
            Vector3 currentAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);
            Vector3 angularAcceleration = (currentAngularVelocity - lastAngularVelocity) / runInterval;

            // 現在の速度と加速度が垂直または反対の方向を持つ場合、ホイールは減速している
            if (Vector3.Dot(currentAngularVelocity.normalized, angularAcceleration.normalized) < 0f)
            {
                float impulseAmplitude = Mathf.Abs(angularAcceleration.x);

                if (impulseAmplitude > 1.5f)
                {
                    float remappedImpulseAmplitude = Remap(impulseAmplitude, 1.5f, 40f, 0f, 1f);

                    // InputDevice.SendHapticImpulseを使用してハプティクスを送信
                    if (device.isValid)
                    {
                        HapticCapabilities capabilities;
                        if (device.TryGetHapticCapabilities(out capabilities))
                        {
                            if (capabilities.supportsImpulse)
                            {
                                uint channel = 0;
                                device.SendHapticImpulse(channel, remappedImpulseAmplitude, runInterval * 2f);
                            }
                        }
                    }
                }
            }

            lastAngularVelocity = currentAngularVelocity;
            yield return new WaitForSeconds(runInterval);
        }
    }

    // インタラクターから適切なInputDeviceを取得するヘルパーメソッド
    InputDevice GetInputDeviceFromInteractor(IXRSelectInteractor interactor)
    {
        // インタラクターのTransformの名前や位置から左右を判別
        string interactorName = interactor.transform.name.ToLower();

        InputDeviceCharacteristics desiredCharacteristics = InputDeviceCharacteristics.HeldInHand | InputDeviceCharacteristics.Controller;

        if (interactorName.Contains("left"))
        {
            desiredCharacteristics |= InputDeviceCharacteristics.Left;
        }
        else if (interactorName.Contains("right"))
        {
            desiredCharacteristics |= InputDeviceCharacteristics.Right;
        }
        else
        {
            // 名前から判別できない場合は、X座標の位置から推測
            // 通常、左手は負のX座標、右手は正のX座標にある
            if (interactor.transform.localPosition.x < 0)
            {
                desiredCharacteristics |= InputDeviceCharacteristics.Left;
            }
            else
            {
                desiredCharacteristics |= InputDeviceCharacteristics.Right;
            }
        }

        // 指定された特性を持つデバイスを検索
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(desiredCharacteristics, devices);

        if (devices.Count > 0)
        {
            return devices[0];
        }

        // デバイスが見つからない場合は無効なデバイスを返す
        return new InputDevice();
    }

    // float値をある範囲から別の範囲に再マッピングするユーティリティメソッドです。
    float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;

        //float normal = Mathf.InverseLerp(aLow, aHigh, value);
        //float bValue = Mathf.Lerp(bLow, bHigh, normal);
    }

    IEnumerator CheckForSlope()
    {
        while (true)
        {
            if (Physics.Raycast(transform.position, -Vector3.up, out RaycastHit hit))
            {
                onSlope = hit.normal != Vector3.up;
            }

            yield return new WaitForSeconds(0.1f);
        }
    }
}