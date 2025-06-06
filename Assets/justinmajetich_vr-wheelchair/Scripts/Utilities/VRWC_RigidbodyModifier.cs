using UnityEngine;
using System;

/// <summary>
/// Rigidbody詳細設定ユーティリティクラス
/// 通常は隠されているRigidbodyプロパティを公開し、エディタ内での修正を可能にします。
/// 物理演算の細かな調整が必要なVRホイールチェアプロジェクトにおいて、
/// 車輪の回転制限や重心の調整を直感的に行えるようにします。
/// </summary>
public class VRWC_RigidbodyModifier : MonoBehaviour
{
    // 対象となるRigidbodyコンポーネント
    Rigidbody rb;

    [Header("Max Angular Velocity")]
    // Rigidbodyの角速度の上限値設定
    // 車輪が異常に高速回転することを防ぎ、安定した物理演算を保証します
    [SerializeField, Range(0, 100), Tooltip("Rigidbodyの角速度の上限値。Unityのデフォルトは7です")]
    float maxAngularVelocity = 7f;

    [Header("Center of Mass")]
    // カスタム重心を使用するかどうかのフラグ
    // チェックを外すと重心が自動計算されます。カスタム重心を設定すると、自動再計算は行われなくなります
    [SerializeField, Tooltip("チェックを外すと重心が自動計算されます。カスタム重心を設定すると、自動再計算は行われなくなります")]
    bool useCustomCenterOfMass = false;

    // カスタム重心の座標（useCustomCenterOfMassがtrueの時のみ有効）
    [SerializeField, Tooltip("カスタム重心の座標。useCustomCenterOfMassがtrueの時のみ有効です")]
    Vector3 centerOfMass;

    // 重心の可視化フラグ
    [SerializeField]
    bool visualizeCenterOfMass = false;

    // 重心可視化用のGameObject
    GameObject visualization = null;

    /// <summary>
    /// 初期化処理
    /// Rigidbodyコンポーネントの取得と初期設定を行います
    /// </summary>
    void Start()
    {
        // 同じGameObjectにアタッチされたRigidbodyコンポーネントを取得
        rb = GetComponent<Rigidbody>();

        // 最大角速度を設定
        // これにより、車輪が物理的に不安定になるほど高速回転することを防げます
        rb.maxAngularVelocity = maxAngularVelocity;

        // カスタム重心が有効な場合、指定された重心を設定
        if (useCustomCenterOfMass)
        {
            rb.centerOfMass = centerOfMass;
        }

        // 重心可視化が有効な場合、可視化オブジェクトを生成
        if (visualizeCenterOfMass)
        {
            // 小さなマゼンタ色の球体を作成して重心位置を表示
            visualization = GameObject.CreatePrimitive(PrimitiveType.Sphere);

            // コライダーを無効化（物理演算に影響しないように）
            visualization.GetComponent<Collider>().enabled = false;

            // 重心のワールド座標位置に配置
            visualization.transform.position = rb.worldCenterOfMass;

            // 小さなサイズに設定（0.05倍）
            visualization.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);

            // マゼンタ色で視認しやすくする
            visualization.GetComponent<MeshRenderer>().material.color = Color.magenta;
        }
    }

    /// <summary>
    /// 毎フレーム実行される更新処理
    /// 重心設定と可視化オブジェクトの管理を行います
    /// </summary>
    void Update()
    {
        // カスタム重心の動的更新
        if (useCustomCenterOfMass)
        {
            // 毎フレーム、設定された重心座標を適用
            // これにより、実行時に重心をリアルタイムで調整可能
            rb.centerOfMass = centerOfMass;
        }
        else
        {
            // カスタム重心が無効な場合、Unityの自動計算重心にリセット
            rb.ResetCenterOfMass();
        }

        // 可視化オブジェクトの管理

        // 可視化が無効になった場合、既存の可視化オブジェクトを削除
        if (!visualizeCenterOfMass && visualization)
        {
            Destroy(visualization);
            visualization = null;
        }

        // 可視化が有効になった場合、新しい可視化オブジェクトを生成
        if (visualizeCenterOfMass && !visualization)
        {
            visualization = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visualization.GetComponent<Collider>().enabled = false;
            visualization.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f);
            visualization.GetComponent<MeshRenderer>().material.color = Color.magenta;
        }

        // 可視化オブジェクトが存在する場合、重心位置に同期
        if (visualization)
        {
            // 毎フレーム、可視化オブジェクトの位置を現在の重心位置に更新
            // これにより、重心の移動がリアルタイムで視覚化されます
            visualization.transform.position = rb.worldCenterOfMass;
        }
    }
}
