# VR車いす手回し操作バグ調査報告書

## 概要
VR車いすアプリケーションにおいて、起動直後の車いすタイヤ手回し操作が機能しない問題について調査を行いました。OpenXRスティック操作で車いすを少し動かすと手回し操作が機能するようになるという現象の原因を特定しました。

## 調査対象
- **スクリプト**: `./Assets/justinmajetich_vr-wheelchair/Scripts/`
- **シーンファイル**: `./Assets/Scenes/baz/baz.unity`
- **Unity版本**: Unity 6
- **XR framework**: OpenXR, XRI v3.0.8

## 発見された問題

### 1. **クラス名の不一致（重要）**
- **ファイル**: `VRWC_WheelInteractable.cs`
- **問題**: ファイル名は`VRWC_WheelInteractable.cs`だが、内部のクラス名は`WheelInteractable`（12行目）
- **影響**: Unityがスクリプトコンポーネントを正しく認識できない可能性

```csharp
// 12行目
public class WheelInteractable : XRBaseInteractable
```

### 2. **InteractionManagerの参照欠如**
- **シーン内の位置**: 166行目 & 1568行目
- **問題**: 両方のWheelInteractableコンポーネントで`m_InteractionManager: {fileID: 0}`
- **影響**: 適切なInteractionManagerの参照がないと、ホイールインタラクションシステムが機能しない

### 3. **XRデバイス初期化タイミング問題**
- **関連コンポーネント**: `VRWC_XRNodeVelocitySupplier`
- **問題箇所**: 
  - 19行目: `Start()`メソッドで即座にデバイス速度取得を試行
  - 24行目: `Update()`で速度更新

```csharp
// 19行目 - Start()での初期化
InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);

// 24行目 - Update()での更新
InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
```

### 4. **ブレーキアシスト機能の早期作動**
- **問題コード**: `WheelInteractable.cs` 94-108行目
- **メカニズム**: 
  1. `BrakeAssist()`が速度データに依存（94行目）
  2. XRデバイスの速度が0または無効な場合、即座にブレーキが作動（99-104行目）
  3. 連続的にグラブポイントの再生成とカウンタートルクの適用

```csharp
// 94行目 - 速度サプライヤーへの依存
VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();

// 99-104行目 - ブレーキ条件
if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
{
    m_Rigidbody.AddTorque(-m_Rigidbody.angularVelocity.normalized * 25f);
    SpawnGrabPoint(interactor);
}
```

### 5. **物理設定の競合**
- **シーン設定**: 675-676行目で`maxAngularVelocity: 25`
- **ブレーキ力**: 101行目でカウンタートルク25f
- **問題**: 初期動作を阻害するフィードバックループの可能性

## 根本原因分析

**主要原因: XRシステム初期化タイミング問題**

1. **シーン読み込み時**: `VRWC_XRNodeVelocitySupplier`コンポーネントがXRデバイスから有効な速度データを即座に取得できない
2. **初期インタラクション時**: `WheelInteractable.BrakeAssist()`が無効/ゼロの速度データを受信
3. **ブレーキ作動**: 速度がゼロと判定され、即座にブレーキアシストが作動
4. **動作阻害**: ブレーキアシストが連続的にグラブポイントを再生成し、カウンタートルクを適用
5. **スティック操作後の改善**: OpenXRスティック制御がXRシステムを「ウォームアップ」し、速度追跡が信頼できるものになる

## 技術的詳細

### XRデバイス初期化の流れ
```
アプリ起動 → XRランタイム初期化 → デバイス検出 → 速度追跡開始
    ↓              ↓              ↓          ↓
   Start()    速度データ無効    初期インタラクション失敗  正常動作
```

### ブレーキアシストのロジック
```csharp
while (grabPoint)
{
    // Z軸速度がほぼゼロの場合、ブレーキと判定
    if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
    {
        // カウンタートルク適用
        m_Rigidbody.AddTorque(-m_Rigidbody.angularVelocity.normalized * 25f);
        // グラブポイント再生成
        SpawnGrabPoint(interactor);
    }
    yield return new WaitForFixedUpdate();
}
```

## 推奨修正案

### 1. **クラス名修正**
```csharp
// 修正前
public class WheelInteractable : XRBaseInteractable

// 修正後
public class VRWC_WheelInteractable : XRBaseInteractable
```

### 2. **XRデバイス準備チェック追加**
```csharp
private void Start()
{
    StartCoroutine(WaitForXRInitialization());
}

private IEnumerator WaitForXRInitialization()
{
    yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
    // 速度取得処理
}
```

### 3. **ブレーキアシストのnullチェック**
```csharp
IEnumerator BrakeAssist(IXRSelectInteractor interactor)
{
    VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();
    
    if (interactorVelocity == null)
    {
        yield break; // nullの場合は早期終了
    }
    
    // 既存のロジック...
}
```

### 4. **初期化遅延の追加**
```csharp
protected override void OnSelectEntered(SelectEnterEventArgs eventArgs)
{
    base.OnSelectEntered(eventArgs);
    
    // 短い遅延後にブレーキアシストを開始
    StartCoroutine(DelayedBrakeAssist(eventArgs.interactorObject));
}

private IEnumerator DelayedBrakeAssist(IXRSelectInteractor interactor)
{
    yield return new WaitForSeconds(0.1f); // XRシステム安定化待ち
    StartCoroutine(BrakeAssist(interactor));
}
```

### 5. **InteractionManager参照の設定**
シーンエディターで両方のWheelInteractableコンポーネントに適切なXR Interaction Managerを設定する必要があります。

## 結論

この問題は、XRシステムの初期化タイミングとブレーキアシスト機能の早期作動による複合的な問題です。OpenXRスティック操作後に手回し操作が機能するのは、スティック入力がXR追跡システムを適切に初期化し、その後の手の追跡が信頼できるものになるためです。

推奨される修正により、アプリケーション起動直後から安定した手回し操作が可能になると予想されます。