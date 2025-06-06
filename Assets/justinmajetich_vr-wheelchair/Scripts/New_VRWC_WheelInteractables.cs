// usingディレクティブ：このスクリプトで使用する機能が含まれる名前空間をインポートします。
using System.Collections; // コルーチン (IEnumerator) を使用するために必要です。
using UnityEngine; // Unityの基本的な機能（GameObject, Rigidbody, Transformなど）を使用するために必要です。
using UnityEngine.UI; // UI要素（今回はデバッグ用のText）を操作するために必要です。
using UnityEngine.XR.Interaction.Toolkit; // XR Interaction Toolkitの基本的な機能を使用するために必要です。
using UnityEngine.XR.Interaction.Toolkit.Interactables; // Interactable（操作される側のオブジェクト）関連のクラスを使用するために必要です。
using UnityEngine.XR.Interaction.Toolkit.Interactors; // Interactor（操作する側、XRコントローラーなど）関連のクラスを使用するために必要です。

// VR車椅子の車輪のインタラクションを管理するクラスです。
// XRBaseInteractableを継承することで、XR Interaction Toolkitのイベント（選択、非選択など）をフックして、
// 独自のインタラクション処理を実装できます。
public class New_VRWC_WheelInteractable : XRBaseInteractable
{
    // --- メンバー変数の定義 ---

    // ■ 物理演算関連
    // Rigidbodyコンポーネントへの参照。オブジェクトの物理的な動き（回転、移動）を制御します。
    Rigidbody m_Rigidbody;
    // 車輪の半径。SphereColliderから取得し、コントローラーとの距離判定などに使用します。
    float wheelRadius;

    // ■ UnityエディタのInspectorから設定するパラメータ
    // 現在、車椅子が坂道にいるかどうかを示すフラグ。CheckForSlopeコルーチンによって更新されます。
    bool onSlope = false;
    // 触覚フィードバック（ハプティクス）を有効にするかどうかを設定します。
    [SerializeField] bool hapticsEnabled = true;
    // コントローラーが車輪からこの距離以上離れたら、自動的に掴むのをやめる（選択解除する）ための閾値です。
    [Range(0, 0.5f), Tooltip("車輪からの距離閾値")]
    [SerializeField] float deselectionThreshold = 0.25f;
    // コントローラーの動きが止まった時に、車輪の回転を止めるためにかけるブレーキ力の強さです。
    [Range(1f, 50f), Tooltip("ブレーキ力の強度")]
    [SerializeField] float brakeForce = 25f;
    // コントローラーが「静止」していると判定するための速度の閾値です。この値より速度が遅いと静止とみなします。
    [Range(0.01f, 0.2f), Tooltip("静止判定の速度閾値")]
    [SerializeField] float stationaryThreshold = 0.05f;

    // ■ 実行中に動的に変化するオブジェクト
    // コントローラーが実際に掴むための、動的に生成されるアンカーポイントとなるGameObjectです。
    GameObject grabPoint;
    // 現在この車輪を掴んでいるインタラクター（XRコントローラー）を保持します。
    IXRSelectInteractor currentInteractor;

    // ■ デバッグ用UI
    // デバッグ情報を画面に表示するためのUIテキストへの参照です。
    public Text label1;
    public Text label2;

    // --- Unityのライフサイクルメソッド ---

    // ゲーム開始時に一度だけ呼び出される初期化処理です。
    private void Start()
    {
        // このスクリプトがアタッチされているGameObjectからRigidbodyコンポーネントを取得します。
        m_Rigidbody = GetComponent<Rigidbody>();

        // Rigidbodyが見つからない場合は、処理に必須なためエラーログを出力し、それ以降の処理を中断します。
        if (m_Rigidbody == null)
        {
            Debug.LogError($"[{gameObject.name}] Rigidbodyコンポーネントが見つかりません。");
            return; // 処理を中断
        }

        // SphereColliderコンポーネントを取得します。
        SphereCollider sphereCollider = GetComponent<SphereCollider>();
        // SphereColliderが見つかれば、その半径をwheelRadiusとして使用します。
        if (sphereCollider != null)
        {
            wheelRadius = sphereCollider.radius;
        }
        else
        {
            // 見つからない場合は警告ログを出し、デフォルト値で処理を続行します。
            Debug.LogWarning($"[{gameObject.name}] SphereColliderが見つかりません。デフォルト半径0.5を使用します。");
            wheelRadius = 0.5f;
        }

        // 坂道を検出するためのコルーチン（定期実行処理）を開始します。
        StartCoroutine(CheckForSlope());
    }

    // --- XR Interaction Toolkitのイベントメソッド（オーバーライド） ---

    // インタラクター（コントローラー）によってこのオブジェクトが選択された（掴まれた）瞬間に呼び出されます。
    protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
    {
        // 基底クラスのOnSelectEnteredを呼び出し、基本的な処理を実行させます。
        base.OnSelectEntered(eventArgs);

        // どのインタラクターが選択したかを取得し、保持します。
        IXRSelectInteractor interactor = eventArgs.interactorObject;
        currentInteractor = interactor;

        // もし前回のgrabPointが残っていた場合に備えて、クリーンアップ処理を実行します。
        CleanupGrabPoint();
        // 掴んだ後のセットアップ処理をコルーチンで開始します。
        StartCoroutine(DelayedGrabSetup(interactor));
    }

    // インタラクターによってこのオブジェクトの選択が解除された（離された）瞬間に呼び出されます。
    protected override void OnSelectExited(SelectExitEventArgs eventArgs)
    {
        // 基底クラスのOnSelectExitedを呼び出します。
        base.OnSelectExited(eventArgs);
        // 動的に生成したgrabPointを破棄します。
        CleanupGrabPoint();
        // 掴んでいるインタラクターの参照をリセットします。
        currentInteractor = null;
    }

    // --- コルーチン（非同期・時間差処理） ---

    // 掴んだ直後のセットアップを1フレーム遅らせて実行するコルーチンです。
    // OnSelectEnteredの直後だとXR Interaction Toolkitの内部処理と競合する場合があるため、1フレーム待機して安定させます。
    IEnumerator DelayedGrabSetup(IXRSelectInteractor interactor)
    {
        // 1フレーム待機します。
        yield return null;

        // interactionManagerが利用可能な状態か確認します。
        if (interactionManager != null)
        {
            // このオブジェクト（車輪自身）に対する直接の選択状態を一度キャンセルします。
            // これにより、標準の「オブジェクトが手に追従する」動作を防ぎ、
            // これから生成する`grabPoint`を介した、より自然な車輪操作のロジックに移行します。
            // 修正: メソッドの引数としてIXRSelectInteractableインターフェースへのキャストが必要です。
            interactionManager.CancelInteractableSelection(this as IXRSelectInteractable);
        }

        // コントローラーが掴むためのアンカーポイント（grabPoint）を生成します。
        SpawnGrabPoint(interactor);
        // ブレーキアシスト機能のコルーチンを開始します。
        StartCoroutine(BrakeAssist(interactor));
        // コントローラーと車輪の距離を監視し、離れすぎたら手を離す処理のコルーチンを開始します。
        StartCoroutine(MonitorDetachDistance(interactor));

        // 触覚フィードバックが有効に設定されている場合
        if (hapticsEnabled)
        {
            // 触覚フィードバックを送信するコルーチンを開始します。
            StartCoroutine(SendHapticFeedback(interactor));
        }
    }

    // コントローラーが掴むためのアンカーポイント（grabPoint）を動的に生成するメソッドです。
    void SpawnGrabPoint(IXRSelectInteractor interactor)
    {
        // 既存のgrabPointがあれば、まず破棄します。
        CleanupGrabPoint();

        // 新しい空のGameObjectを生成し、操作に必要なコンポーネントを追加します。
        // XRGrabInteractable: これによりコントローラーがこのオブジェクトを掴めるようになります。
        // Rigidbody: 物理演算（特にJoint）のために必要です。
        // FixedJoint: このgrabPointを車輪本体に物理的に固定するために使用します。
        grabPoint = new GameObject($"{transform.name}_GrabPoint", typeof(XRGrabInteractable), typeof(Rigidbody), typeof(FixedJoint));

        // インタラクター（コントローラー）のTransform（位置・回転情報）を取得します。
        Transform interactorTransform = (interactor as MonoBehaviour)?.transform;
        // grabPointをコントローラーの現在の位置に生成します。
        if (interactorTransform != null)
        {
            grabPoint.transform.position = interactorTransform.position;
        }

        // grabPointに追加したRigidbodyのプロパティを設定します。
        Rigidbody grabPointRb = grabPoint.GetComponent<Rigidbody>();
        if (grabPointRb != null)
        {
            grabPointRb.mass = 0.1f; // 軽い質量に設定します。
            grabPointRb.useGravity = false; // 重力の影響を受けないようにします。
        }

        // grabPointに追加したFixedJointのプロパティを設定します。
        FixedJoint joint = grabPoint.GetComponent<FixedJoint>();
        if (joint != null && m_Rigidbody != null)
        {
            // Jointを車輪本体のRigidbodyに接続します。これにより、grabPointが動くと車輪も追従します。
            joint.connectedBody = m_Rigidbody;
            // Jointが意図せず壊れないように、breakForceとbreakTorqueを無限大に設定します。
            joint.breakForce = Mathf.Infinity;
            joint.breakTorque = Mathf.Infinity;
            // Jointで繋がったオブジェクト同士が衝突しないように設定します。
            joint.enableCollision = false;
            // 物理演算の安定性を高めるための前処理を無効化します。
            joint.enablePreprocessing = false;
        }

        // grabPointに追加したXRGrabInteractableを取得します。
        XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
        if (grabInteractable != null && interactionManager != null)
        {
            // interactionManagerを介して、インタラクター（コントローラー）にこの新しいgrabPointをプログラム的に選択（掴む）させます。
            // これで、ユーザーは車輪本体ではなく、このアンカーポイントを掴んで操作している状態になります。
            interactionManager.SelectEnter(interactor, grabInteractable);
        }
    }

    // 動的に生成されたgrabPointを破棄し、関連リソースを解放するメソッドです。
    void CleanupGrabPoint()
    {
        // grabPointが既に存在する場合のみ処理を実行します。
        if (grabPoint != null)
        {
            XRGrabInteractable grabInteractable = grabPoint.GetComponent<XRGrabInteractable>();
            // grabPointがまだ選択されている状態であれば、その選択をプログラム的にキャンセルします。
            if (grabInteractable != null && interactionManager != null)
            {
                // 修正: メソッドの引数としてIXRSelectInteractableインターフェースへのキャストが必要です。
                interactionManager.CancelInteractableSelection(grabInteractable as IXRSelectInteractable);
            }

            // grabPointのGameObjectをシーンから即座に破棄します。
            // Destroy()はフレームの終わりに実行されますが、DestroyImmediate()は即時実行のため、確実なクリーンアップが可能です。
            DestroyImmediate(grabPoint);
            // 参照をnullにして、ガベージコレクション（メモリ解放）の対象にします。
            grabPoint = null;
        }
    }

    // ブレーキアシスト機能を提供するコルーチンです。
    // コントローラーの動きが止まったと判定された場合に、車輪に能動的にブレーキをかけます。
    IEnumerator BrakeAssist(IXRSelectInteractor interactor)
    {
        // インタラクターから速度を取得するためのカスタムコンポーネントを取得します。
        New_VRWC_XRNodeVelocitySupplier interactorVelocity = (interactor as MonoBehaviour)?.GetComponent<New_VRWC_XRNodeVelocitySupplier>();

        // 速度供給コンポーネントが見つからない場合は警告を出し、この機能を無効化します。
        if (interactorVelocity == null)
        {
            Debug.LogWarning($"[{gameObject.name}] VelocitySupplierが見つかりません。ブレーキアシストを無効化します。");
            yield break; // コルーチンを終了します。
        }

        // 前のフレームで静止していたかどうかを記録するフラグです。
        bool wasStationary = false;

        // grabPointが存在し、かつ現在のインタラクターがこのコルーチンを開始したインタラクターである間、ループを続けます。
        while (grabPoint != null && currentInteractor == interactor)
        {
            // 現在のコントローラーのZ軸方向（前後方向）の速度の絶対値が閾値より小さいか判定し、静止状態かを判断します。
            bool isCurrentlyStationary = Mathf.Abs(interactorVelocity.velocity.z) < stationaryThreshold;

            // 「動いていた状態」から「静止した状態」に切り替わった瞬間にブレーキをかけます。
            if (isCurrentlyStationary && !wasStationary && m_Rigidbody != null)
            {
                // 車輪の現在の角速度（回転の速さ）を取得します。
                Vector3 currentAngularVelocity = m_Rigidbody.angularVelocity;
                // 車輪がわずかでも回転している場合（慣性で回り続けている場合など）
                if (currentAngularVelocity.magnitude > 0.1f)
                {
                    // 現在の回転と逆方向のトルク（回転力）を加えて、回転を止めます。
                    m_Rigidbody.AddTorque(-currentAngularVelocity.normalized * brakeForce);
                }
            }

            // 現在の静止状態を次のフレームのために保存します。
            wasStationary = isCurrentlyStationary;
            // 次の物理演算フレーム（FixedUpdate）まで待機します。物理演算はFixedUpdateで更新されるため、それに同期させます。
            yield return new WaitForFixedUpdate();
        }
    }

    // コントローラーと車輪の距離を監視し、離れすぎたら自動で選択を解除するコルーチンです。
    IEnumerator MonitorDetachDistance(IXRSelectInteractor interactor)
    {
        // 距離をチェックする間隔（秒）です。毎フレーム行うと負荷が高いため、間隔を空けます。
        float checkInterval = 0.05f;

        // インタラクターのTransformを取得します。
        Transform interactorTransform = (interactor as MonoBehaviour)?.transform;
        if (interactorTransform == null)
        {
            // Transformが取得できなければ処理を中断します。
            yield break;
        }

        // grabPointが存在し、現在のインタラクターがこのコルーチンを開始したものである間、ループします。
        while (grabPoint != null && currentInteractor == interactor)
        {
            // 車輪の中心とコントローラーの間の距離の二乗を計算します。
            // (sqrMagnitudeは平方根の計算を省略するため、magnitudeより高速で、比較用途に適しています)
            float sqrDistance = (transform.position - interactorTransform.position).sqrMagnitude;
            // 閾値も二乗して、二乗同士で比較します。
            float thresholdSqr = (wheelRadius + deselectionThreshold) * (wheelRadius + deselectionThreshold);

            // 距離が閾値を超えた場合
            if (sqrDistance >= thresholdSqr)
            {
                // interactionManagerを介して、このインタラクターの選択を強制的にキャンセルします（手を離させます）。
                if (interactionManager != null)
                {
                    interactionManager.CancelInteractorSelection(interactor);
                }
                // ループを抜けてコルーチンを終了します。
                yield break;
            }

            // 指定された時間だけ待機してから次のチェックを行います。
            yield return new WaitForSeconds(checkInterval);
        }
    }

    // 触覚フィードバック（コントローラーの振動）を送信するコルーチンです。
    IEnumerator SendHapticFeedback(IXRSelectInteractor interactor)
    {
        // フィードバック処理を実行する間隔（秒）です。
        float runInterval = 0.1f;

        // インタラクターを、振動機能にアクセスできるXRBaseInputInteractorにキャストします。
        XRBaseInputInteractor controllerInteractor = interactor as XRBaseInputInteractor;

        // キャストに失敗した場合や、Rigidbodyがない場合は、振動させられないので処理を中断します。
        if (controllerInteractor == null || m_Rigidbody == null)
        {
            yield break;
        }

        // 前フレームの車輪のローカル角速度（X軸周りの回転）を記録しておきます。
        Vector3 lastAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);

        // grabPointが存在し、現在のインタラクターがこのコルーチンを開始したものである間、ループします。
        while (grabPoint != null && currentInteractor == interactor)
        {
            // 現在の車輪のローカル角速度（X軸周りの回転）を取得します。
            Vector3 currentAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);
            // 角加速度（角速度の変化率）を計算します。
            Vector3 angularAcceleration = (currentAngularVelocity - lastAngularVelocity) / runInterval;

            // 車輪が回転しており、かつ角速度と角加速度の方向が逆（＝減速している）の場合
            if (currentAngularVelocity.magnitude > 0.1f && Vector3.Dot(currentAngularVelocity.normalized, angularAcceleration.normalized) < 0f)
            {
                // 角加速度の絶対値を振動の強さの元とします。急な減速ほど強い値になります。
                float impulseAmplitude = Mathf.Abs(angularAcceleration.x);

                // 減速の度合いが一定値より大きい場合（強い抵抗がかかったとみなせる場合）
                if (impulseAmplitude > 1.5f)
                {
                    // 角加速度の値を0～1の範囲にマッピングし直し、振動の強さとして使用します。
                    float remappedImpulseAmplitude = Remap(impulseAmplitude, 1.5f, 40f, 0f, 1f);
                    // コントローラーに振動命令を送信します。（強さ, 持続時間）
                    controllerInteractor.SendHapticImpulse(remappedImpulseAmplitude, runInterval * 2f);
                }
            }

            // 現在の角速度を次のフレームのために保存します。
            lastAngularVelocity = currentAngularVelocity;
            // 指定された時間だけ待機します。
            yield return new WaitForSeconds(runInterval);
        }
    }

    // --- ヘルパーメソッド ---

    // ある数値の範囲を、別の数値の範囲に変換（マッピング）する汎用メソッドです。
    // 例: Remap(5, 0, 10, 0, 1) は、0～10の範囲の5を、0～1の範囲に変換して0.5を返します。
    float Remap(float value, float from1, float to1, float from2, float to2)
    {
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
    }

    // 坂道にいるかどうかを定期的にチェックするコルーチンです。
    IEnumerator CheckForSlope()
    {
        // 無限ループで、ゲーム実行中ずっとチェックを続けます。
        while (true)
        {
            // 車輪の中心から真下にRay（光線）を飛ばし、地面との接触情報を取得します。
            if (Physics.Raycast(transform.position, -Vector3.up, out RaycastHit hit, wheelRadius * 2f))
            {
                // 接触した地面の法線ベクトルと、世界の真上方向のベクトルとの角度を計算します。
                // 角度が1度より大きい場合、平坦ではないとみなし、坂道と判定します。
                onSlope = Vector3.Angle(hit.normal, Vector3.up) > 1f;
            }
            else
            {
                // Rayが何にも当たらなかった場合（空中にいる場合など）は、坂道ではないと判定します。
                onSlope = false;
            }

            // 0.2秒待ってから次のチェックを行います。負荷軽減のためです。
            yield return new WaitForSeconds(0.2f);
        }
    }

    // --- Unityの破棄・無効化メソッド ---

    // このGameObjectが破棄される時に呼び出されます。
    protected override void OnDestroy()
    {
        base.OnDestroy(); // 基底クラスのOnDestroy処理を呼び出します。
        CleanupGrabPoint(); // grabPointがシーンに残り続けないようにクリーンアップします。
        StopAllCoroutines(); // このスクリプトで実行中の全てのコルーチンを停止し、メモリリークを防ぎます。
    }

    // このコンポーネントが無効になった時（Inspectorでチェックを外した時など）に呼び出されます。
    protected override void OnDisable()
    {
        base.OnDisable(); // 基底クラスのOnDisable処理を呼び出します。
        // 選択中にオブジェクトが無効化されると意図しない動作の原因になるため、
        // grabPointをクリーンアップし、インタラクターの参照をクリアして安全な状態にします。
        CleanupGrabPoint();
        currentInteractor = null;
    }
}