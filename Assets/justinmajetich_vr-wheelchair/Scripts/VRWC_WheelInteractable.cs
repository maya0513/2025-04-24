using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// 【VR車椅子 - 車輪相互作用システム】
/// このクラスはVR環境での車椅子の車輪操作を管理する核となるスクリプトです。
/// XRBaseInteractableを継承することで、VRコントローラーとの相互作用が可能になります。
/// 主な機能：
/// ・動的グラブポイント生成（ユーザーがつかんだ位置に応じて相互作用点を作成）
/// ・ブレーキアシスト（停止時の自動ブレーキ補助）
/// ・距離監視による自動選択解除（手が離れすぎた時の自動解除）
/// ・触覚フィードバック（車輪の動きに応じたコントローラー振動）
/// ・坂道検出（地面の傾斜を感知）

public class VRWC_WheelInteractable : XRBaseInteractable
{
    // 【物理演算関連の変数】
    /// 【車輪の物理演算コンポーネント】
    /// 車輪の回転、重力、力の適用などすべての物理的な動作を管理します
    Rigidbody m_Rigidbody;

    /// 【車輪の半径】
    /// SphereColliderから取得した車輪の半径値を保存
    /// 距離計算や相互作用範囲の決定に使用されます
    float wheelRadius;

    // 【設定可能な機能パラメータ】

    /// 【坂道判定フラグ】
    /// 車椅子が平坦な地面にいるか坂道にいるかを示すフラグ
    /// 将来的に坂道での動作調整に使用可能
    bool onSlope = false;

    /// 【触覚フィードバック有効/無効設定】
    /// Inspectorで設定可能。trueの場合、車輪の動きに応じて
    /// VRコントローラーに振動フィードバックを送信します
    [SerializeField] bool hapticsEnabled = true;

    /// 【自動選択解除の距離閾値】
    /// 車輪の中心からこの距離以上離れると自動的に選択が解除されます
    /// 0〜0.5の範囲で設定可能。デフォルトは0.25（25cm）
    [Range(0, 0.5f), Tooltip("車輪コライダーからの距離。この距離を超えると自動的に選択が解除されます。")]
    [SerializeField] float deselectionThreshold = 0.25f;

    // 【動的生成されるオブジェクト】

    /// 【動的グラブポイント】
    /// ユーザーが車輪をつかんだ時に動的に生成される相互作用オブジェクト
    /// XRGrabInteractable、Rigidbody、FixedJointコンポーネントを持ち、
    /// 車輪との物理的な接続を仲介します
    GameObject grabPoint;

    // 【デバッグ用UI要素】

    /// 【デバッグ用テキスト表示1】
    /// 開発・デバッグ時の情報表示用（使用しない場合は削除可能）
    public Text label1;

    /// 【デバッグ用テキスト表示2】
    /// 開発・デバッグ時の情報表示用（使用しない場合は削除可能）
    public Text label2;

    // 【Unity生命周期メソッド】

    /// 【初期化処理】
    /// ゲーム開始時に一度だけ実行される初期化メソッド
    /// 必要なコンポーネントの取得と継続的な処理の開始を行います

    private void Start()
    {
        // 【物理演算コンポーネントの取得】
        // 同じGameObjectに付いているRigidbodyコンポーネントを取得
        // これがないと車輪の物理的な動作（回転、重力など）ができません
        m_Rigidbody = GetComponent<Rigidbody>();

        // 【車輪サイズの取得】
        // SphereColliderの半径を車輪の半径として使用
        // 距離計算や相互作用範囲の決定に使用されます
        wheelRadius = GetComponent<SphereCollider>().radius;

        // 【坂道検出の開始】
        // バックグラウンドで定期的に地面の傾斜をチェックするコルーチンを開始
        // CPUの負荷を軽減するため、適度な間隔で実行されます
        StartCoroutine(CheckForSlope());
    }

    // 【VR相互作用イベント処理】

    /// 【選択開始イベント】
    /// VRコントローラーがこの車輪を選択（グラブ）した時に自動的に呼び出されるメソッド
    /// 車輪操作の核となる処理がすべてここから開始されます

    /// <param name="eventArgs">選択イベントの詳細情報（どのコントローラーが選択したかなど）</param>
    protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
    {
        // まず基底クラス（XRBaseInteractable）の標準処理を実行
        base.OnSelectEntered(eventArgs);

        // 【選択したコントローラーの情報を取得】
        IXRSelectInteractor interactor = eventArgs.interactorObject;

        // 【直接選択をキャンセルして間接操作に切り替え】
        // 通常のVRオブジェクトと異なり、車輪は直接選択せず
        // 代わりに「グラブポイント」という仲介オブジェクトを使用します
        interactionManager.CancelInteractableSelection((IXRSelectInteractable)this);

        // 【動的グラブポイントシステムの開始】
        // ユーザーがつかんだ位置に相互作用ポイントを動的生成
        SpawnGrabPoint(interactor);

        // 【各種アシスト機能の開始】
        StartCoroutine(BrakeAssist(interactor));          // ブレーキ補助機能
        StartCoroutine(MonitorDetachDistance(interactor)); // 距離監視による自動解除

        // 【触覚フィードバックの開始】
        // 設定で有効になっている場合のみコントローラーの振動を開始
        if (hapticsEnabled)
        {
            StartCoroutine(SendHapticFeedback(interactor));
        }
    }

    // ================================
    // 【動的グラブポイントシステム】
    // ================================

    ///
    /// 【動的グラブポイント生成メソッド】
    /// 
    /// このメソッドは車輪操作の革新的な機能の核となります。
    /// 通常のVRオブジェクトは固定された位置でのみ掴めますが、
    /// 車輪は「どこを掴んでも」適切に動作するように動的にグラブポイントを生成します。
    /// 
    /// 動作原理：
    /// 1. ユーザーがコントローラーで車輪に触れる
    /// 2. その瞬間のコントローラー位置に新しいグラブポイントを生成
    /// 3. グラブポイントを車輪にFixedJointで物理的に接続
    /// 4. ユーザーは実際にはグラブポイントを操作し、それが車輪を動かす

    /// <param name="interactor">選択を行っているVRコントローラー</param>
    void SpawnGrabPoint(IXRSelectInteractor interactor)
    {
        // 【既存グラブポイントのクリーンアップ】
        // 既にアクティブなグラブポイントがある場合は
        // その選択状態をキャンセルして削除準備を行います
        if (grabPoint)
        {
            interactionManager.CancelInteractableSelection((IXRSelectInteractable)grabPoint.GetComponent<XRGrabInteractable>());
        }

        // 【新しいグラブポイントオブジェクトの生成】
        // コントローラーの現在位置に以下のコンポーネントを持つオブジェクトを作成：
        // - XRGrabInteractable: VRでの掴み操作を可能にする
        // - Rigidbody: 物理演算を適用する
        // - FixedJoint: 車輪との物理的な接続を行う
        grabPoint = new GameObject($"{transform.name}'s grabPoint", typeof(XRGrabInteractable), typeof(Rigidbody), typeof(FixedJoint));

        // グラブポイントをコントローラーの現在位置に配置
        grabPoint.transform.position = (interactor as MonoBehaviour).transform.position;

        // 【物理的接続の確立】
        // FixedJointを使用してグラブポイントと車輪を物理的に接続
        // これにより、グラブポイントを動かすと車輪も連動して動きます
        grabPoint.GetComponent<FixedJoint>().connectedBody = GetComponent<Rigidbody>();

        // 【コントローラーとグラブポイントの選択関係を確立】
        // 実際の相互作用はコントローラー ⟷ グラブポイント間で行われます
        interactionManager.SelectEnter(interactor, grabPoint.GetComponent<XRGrabInteractable>());
    }

    // ================================
    // 【ブレーキアシストシステム】
    // ================================

    ///
    /// 【ブレーキアシスト機能】
    /// 
    /// 実際の車椅子のように、ユーザーの手の動きを監視してブレーキ動作を自動検出し、
    /// 適切なブレーキ力を車輪に適用します。これにより直感的な停止操作が可能になります。
    /// 
    /// 動作原理：
    /// 1. コントローラーの前後方向の速度を継続的に監視
    /// 2. 速度がほぼゼロになった時（±0.05f以内）をブレーキ動作と判定
    /// 3. 車輪の回転に対して逆方向のトルクを適用してブレーキ効果を生成
    /// 4. グラブポイントを再配置して操作の継続性を保つ

    /// <param name="interactor">操作中のVRコントローラー</param>
    /// <returns>コルーチンの列挙子</returns>
    IEnumerator BrakeAssist(IXRSelectInteractor interactor)
    {
        // 【コントローラーの速度情報取得】
        // 専用の速度測定コンポーネントから3D空間での移動速度を取得
        New_VRWC_XRNodeVelocitySupplier interactorVelocity = (interactor as MonoBehaviour)?.GetComponent<New_VRWC_XRNodeVelocitySupplier>();

        // 【グラブポイントが存在する間、ブレーキ監視を継続】
        while (grabPoint)
        {
            // 【ブレーキ動作の検出】
            // Z軸（前後方向）の速度がほぼゼロの場合をブレーキ動作として判定
            // 0.05fという値は、手の微細な動きを除外するための閾値
            if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
            {
                // 【ブレーキ力の適用】
                // 車輪の現在の回転方向と逆向きのトルクを適用
                // 25fは経験的に調整されたブレーキ力の強度値
                m_Rigidbody.AddTorque(-m_Rigidbody.angularVelocity.normalized * 25f);

                // 【グラブポイントの更新】
                // ブレーキ後も操作を継続できるようにグラブポイントを再生成
                SpawnGrabPoint(interactor);
            }

            // 【物理演算フレームごとの実行】
            // FixedUpdateのタイミングで実行することで物理演算と同期
            yield return new WaitForFixedUpdate();
        }
    }

    // ================================
    // 【距離監視システム】
    // ================================

    ///
    /// 【距離監視による自動選択解除】
    /// 
    /// 現実的な車椅子操作では、手が車輪から離れすぎると操作ができなくなります。
    /// この機能はその物理的制約をVR空間で再現し、自然な操作感を提供します。
    /// 
    /// 動作原理：
    /// 1. 車輪の中心とコントローラーの距離を継続的に計算
    /// 2. 距離が「車輪の半径 + 設定した閾値」を超えた場合
    /// 3. 自動的に選択状態を解除して操作を終了

    /// <param name="interactor">監視対象のVRコントローラー</param>
    /// <returns>コルーチンの列挙子</returns>
    IEnumerator MonitorDetachDistance(IXRSelectInteractor interactor)
    {
        // 【アクティブなグラブポイントがある間、距離を継続監視】
        while (grabPoint)
        {
            // 【距離計算と閾値チェック】
            // 車輪中心からコントローラーまでの3D距離を計算
            // wheelRadius（車輪の半径）+ deselectionThreshold（設定した閾値）と比較
            if (Vector3.Distance(transform.position, (interactor as MonoBehaviour).transform.position) >= wheelRadius + deselectionThreshold)
            {
                // 【自動選択解除の実行】
                // 距離が閾値を超えた場合、強制的に選択状態をキャンセル
                interactionManager.CancelInteractorSelection(interactor);
            }

            // 【毎フレーム実行】
            // yield return null を使用してフレームレートに依存した実行
            // 距離チェックは高頻度で行う必要があるため
            yield return null;
        }
    }

    // ================================
    // 【触覚フィードバックシステム】
    // ================================

    ///
    /// 【触覚フィードバック（ハプティック）システム】
    /// 
    /// 車輪の物理的な動きをVRコントローラーの振動として伝達し、
    /// より没入感のある車椅子操作体験を提供します。
    /// 
    /// 動作原理：
    /// 1. 車輪の角速度（回転速度）を継続的に監視
    /// 2. 角加速度（回転速度の変化）を計算
    /// 3. 減速が検出された場合（ブレーキや抵抗による）
    /// 4. その強度に応じてコントローラーに振動を送信
    /// 
    /// 技術的詳細：
    /// - XR Interaction Toolkit 3.0以降の最新APIを使用
    /// - 振動強度は物理的な変化量に基づいて動的に調整
    /// - 0.1秒間隔での監視によりCPU負荷を最適化

    /// <param name="interactor">振動を送信するVRコントローラー</param>
    /// <returns>コルーチンの列挙子</returns>
    IEnumerator SendHapticFeedback(IXRSelectInteractor interactor)
    {
        // 【コルーチンの実行間隔設定】
        // 0.1秒間隔で実行することでCPU負荷と応答性のバランスを取る
        float runInterval = 0.1f;

        // 【最新のXR Interaction Toolkit API対応】
        // 古いAPIとの互換性を保ちつつ、新しいハプティック機能を使用
        XRBaseInputInteractor controllerInteractor = interactor as XRBaseInputInteractor;

        // 【前回の角速度を記録】
        // 加速度計算のための基準値として使用
        // X軸のみを使用（車輪の主要な回転軸）
        Vector3 lastAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);

        // 【グラブポイントが存在する間、触覚フィードバックを継続】
        while (grabPoint)
        {
            // 【現在の角速度を取得】
            // 車輪のローカル座標系でのX軸回転速度を取得
            Vector3 currentAngularVelocity = new Vector3(transform.InverseTransformDirection(m_Rigidbody.angularVelocity).x, 0f, 0f);

            // 【角加速度の計算】
            // (現在の速度 - 前回の速度) ÷ 時間間隔 = 加速度
            Vector3 angularAcceleration = (currentAngularVelocity - lastAngularVelocity) / runInterval;

            // 【減速の検出】
            // 内積を使用して速度ベクトルと加速度ベクトルの方向関係を判定
            // 内積が負の場合、ベクトルが反対方向＝減速している
            if (Vector3.Dot(currentAngularVelocity.normalized, angularAcceleration.normalized) < 0f)
            {
                // 【振動強度の計算】
                // 角加速度の絶対値を振動の強度として使用
                float impulseAmplitude = Mathf.Abs(angularAcceleration.x);

                // 【閾値チェック】
                // 小さな変化では振動させず、明確な減速のみを対象とする
                if (impulseAmplitude > 1.5f)
                {
                    // 【振動強度の正規化】
                    // 1.5〜40の範囲を0〜1の範囲にリマップして適切な振動レベルに調整
                    float remappedImpulseAmplitude = Remap(impulseAmplitude, 1.5f, 40f, 0f, 1f);

                    // 【ハプティックインパルスの送信】
                    // XR Interaction Toolkit 3.0以降の推奨ハプティック送信方法
                    // 第1引数：振動の強度（0〜1）
                    // 第2引数：振動の持続時間（runInterval * 2f = 0.2秒）
                    if (controllerInteractor != null)
                    {
                        controllerInteractor.SendHapticImpulse(remappedImpulseAmplitude, runInterval * 2f);
                    }
                }
            }

            // 【次の計算のために現在の値を保存】
            lastAngularVelocity = currentAngularVelocity;

            // 【指定間隔での待機】
            yield return new WaitForSeconds(runInterval);
        }
    }

    // ================================
    // 【ユーティリティメソッド】
    // ================================

    ///
    /// 【値の範囲変換ユーティリティ】
    /// 
    /// ある数値範囲の値を別の数値範囲に変換するユーティリティメソッド
    /// 
    /// 例：温度の摂氏を華氏に変換、0-100の値を0-1の範囲に変換など
    /// ハプティック機能では、物理的な変化量を振動強度（0-1）に変換するために使用

    /// <param name="value">変換する値</param>
    /// <param name="from1">元の範囲の最小値</param>
    /// <param name="to1">元の範囲の最大値</param>
    /// <param name="from2">新しい範囲の最小値</param>
    /// <param name="to2">新しい範囲の最大値</param>
    /// <returns>変換された値</returns>
    float Remap(float value, float from1, float to1, float from2, float to2)
    {
        // 【リマップ計算式】
        // 1. まず元の範囲での比率を計算: (value - from1) / (to1 - from1)
        // 2. その比率を新しい範囲に適用: 比率 * (to2 - from2) + from2
        return (value - from1) / (to1 - from1) * (to2 - from2) + from2;

        // 【代替実装方法（コメントアウト）】
        // Unityの組み込み関数を使用した別の実装方法
        // float normal = Mathf.InverseLerp(from1, to1, value);  // 0-1の比率を計算
        // float bValue = Mathf.Lerp(from2, to2, normal);        // 新しい範囲に適用
        // return bValue;
    }

    // ================================
    // 【坂道検出システム】
    // ================================

    ///
    /// 【坂道検出機能】
    /// 
    /// 定期的に地面の法線ベクトルをチェックして車椅子が坂道にいるかどうかを判定します。
    /// この情報は将来的に坂道での車輪の動作調整（滑り止め、ブレーキ強化など）に使用可能です。
    /// 
    /// 動作原理：
    /// 1. 車輪の位置から地面に向かってレイキャストを発射
    /// 2. 地面の法線ベクトルを取得
    /// 3. 法線が真上（Vector3.up）でない場合、坂道と判定
    /// 4. 0.1秒間隔で継続的にチェック

    /// <returns>コルーチンの列挙子</returns>
    IEnumerator CheckForSlope()
    {
        // 【無限ループで継続的な坂道チェック】
        while (true)
        {
            // 【レイキャストによる地面検出】
            // 車輪の位置から下向き（-Vector3.up）にレイを発射
            // out RaycastHit hit: ヒット情報を受け取る変数
            if (Physics.Raycast(transform.position, -Vector3.up, out RaycastHit hit))
            {
                // 【坂道判定の実行】
                // 地面の法線ベクトルが真上（Vector3.up = 0,1,0）でない場合、坂道と判定
                // 完全に平坦な地面の場合、法線は真上を向く
                onSlope = hit.normal != Vector3.up;
            }

            // 【効率的な実行間隔の設定】
            // 0.1秒間隔でチェックすることで、リアルタイム性とCPU負荷のバランスを取る
            // 坂道の検出は高頻度である必要がないため、この間隔で十分
            yield return new WaitForSeconds(0.1f);
        }
    }
}
