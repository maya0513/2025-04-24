# VR車いすアプリ - 初回手回し操作バグ調査レポート

## 問題の概要

VR車いすアプリケーションにおいて、起動後の最初のタイヤ手回し操作が効かない問題が発生している。OpenXRスティック操作で少し動かした後は正常に動作するが、初回の手回し操作のみ失敗する。

## 調査結果

### 1. バグの根本原因

**Unity XR/OpenXRの初期化タイミングの問題**

コードの詳細分析により、以下の初期化順序の問題が特定された：

1. `MonoBehaviour.Start()`が呼ばれるタイミングで、OpenXRシステムがまだ完全に初期化されていない
2. `VRWC_XRNodeVelocitySupplier.Start()`で`InputDevices.GetDeviceAtXRNode()`にアクセスするが、この時点でデバイスが無効
3. 結果として`BrakeAssist`機能で必要な速度データが取得できず、手回し操作が正常に動作しない

### 2. 問題のあるファイルとコード

#### A. `/Assets/justinmajetich_vr-wheelchair/Scripts/Utilities/VRWC_XRNodeVelocitySupplier.cs`

**問題箇所：**
```csharp
private void Start()
{
    // この時点でXRシステムが初期化されていない可能性がある
    InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
    StartCoroutine(WaitForXRInitialization());
}

private IEnumerator WaitForXRInitialization()
{
    yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
    // 速度取得処理
}
```

**問題点：**
- `Start()`で即座に`InputDevices.GetDeviceAtXRNode()`を呼び出している
- `WaitForXRInitialization()`の実装が不完全（待機後の処理が空）
- XRデバイスの初期化完了を適切に待機していない

#### B. `/Assets/justinmajetich_vr-wheelchair/Scripts/VRWC_WheelInteractable.cs`

**問題箇所：**
```csharp
IEnumerator BrakeAssist(IXRSelectInteractor interactor)
{
    VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();

    if (interactorVelocity == null)
    {
        yield break; // nullの場合は早期終了
    }

    while (grabPoint)
    {
        // interactorVelocity.velocity が初期化されていない可能性
        if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
        {
            m_Rigidbody.AddTorque(-m_Rigidbody.angularVelocity.normalized * 25f);
            SpawnGrabPoint(interactor);
        }
        yield return new WaitForFixedUpdate();
    }
}
```

**問題点：**
- `BrakeAssist`が`VRWC_XRNodeVelocitySupplier`に依存しているが、初期化チェックがない
- 速度データが無効な場合の処理が不十分

### 3. 具体的な修正方法

#### A. VRWC_XRNodeVelocitySupplier.csの修正

```csharp
private void Start()
{
    StartCoroutine(WaitForXRInitialization());
}

private IEnumerator WaitForXRInitialization()
{
    // XRデバイスが有効になるまで待機
    yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
    
    // 初期化完了後に速度取得を開始
    InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
    
    // 初期化完了フラグを設定
    isInitialized = true;
}

private bool isInitialized = false;
public bool IsInitialized => isInitialized;

void Update()
{
    if (isInitialized)
    {
        InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
    }
}
```

#### B. VRWC_WheelInteractable.csの修正

```csharp
IEnumerator BrakeAssist(IXRSelectInteractor interactor)
{
    VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();

    if (interactorVelocity == null)
    {
        yield break;
    }

    // XRNodeVelocitySupplierの初期化完了を待機
    yield return new WaitUntil(() => interactorVelocity.IsInitialized);

    while (grabPoint)
    {
        if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
        {
            m_Rigidbody.AddTorque(-m_Rigidbody.angularVelocity.normalized * 25f);
            SpawnGrabPoint(interactor);
        }
        yield return new WaitForFixedUpdate();
    }
}
```

#### C. 代替案：遅延初期化の実装

より堅牢な実装として、以下のような遅延初期化も検討できる：

```csharp
private IEnumerator DelayedBrakeAssist(IXRSelectInteractor interactor)
{
    // XRシステム安定化のための追加待機
    yield return new WaitForSeconds(0.5f);
    
    // InputDeviceの有効性を再確認
    yield return new WaitUntil(() => {
        var device = GetInputDeviceFromInteractor(interactor);
        return device.isValid;
    });
    
    StartCoroutine(BrakeAssist(interactor));
}
```

### 4. なぜスティック操作後は動作するのか

スティック操作を行うことで以下が発生する：
1. OpenXRシステムが完全に初期化される
2. `InputDevices.GetDeviceAtXRNode()`が有効になる
3. `VRWC_XRNodeVelocitySupplier`が正常に速度データを取得できるようになる
4. 結果として手回し操作の`BrakeAssist`機能が正常に動作する

### 5. 推奨する修正アプローチ

**優先度1: 即座に実装すべき修正**
- `VRWC_XRNodeVelocitySupplier.cs`の初期化処理を修正
- 初期化完了フラグ（`IsInitialized`プロパティ）を追加

**優先度2: 安定性向上のための修正**
- `VRWC_WheelInteractable.cs`で初期化完了待機を実装
- エラーハンドリングの強化

**優先度3: 将来的な改善**
- XRシステム初期化イベントの活用
- より堅牢な初期化シーケンスの実装

## まとめ

このバグはVR/XRシステムの初期化タイミングに起因する問題であり、主に`VRWC_XRNodeVelocitySupplier`の不適切な初期化処理が原因である。上記の修正により、初回の手回し操作も正常に動作するようになる。

修正の実装は比較的簡単で、既存のコードへの影響も最小限に抑えられる。