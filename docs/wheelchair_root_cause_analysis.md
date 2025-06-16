# VR車いすアプリ - 初回手回し操作バグ根本原因調査レポート

## 問題の概要

VR車いすアプリケーションにおいて、起動後の最初のタイヤ手回し操作が効かない問題が発生している。OpenXRスティック操作で少し動かした後は正常に動作するが、初回の手回し操作のみ失敗する。

前回の調査では表面的な修正案が提示されたが、根本的な問題は解決されていない。今回は、シーンファイルの詳細分析を含めた抜本的な見直しを実施。

## 根本原因の特定

### 1. **シーン構造における重大な問題**

#### A. XR Origin の競合
- **問題**: シーン内に2つのXR Originが存在
  - メインの`XR Origin (XR Rig)` (GUID: f6336ac4ac8b4d34bc5072418cdc62a0)
  - 車いすプレハブ内の`VRWC Wheelchair Rig`内のXR Origin (無効化されている)
- **影響**: 初期化順序の混乱とトラッキング参照の曖昧性

#### B. 車いすプレハブの不完全な統合
- **削除されたコンポーネント**: fileIDs `3355997934270379110`, `3355997934270379111`, `3355997934027076923`, `3355997933869472933`
- **無効化されたコンポーネント**: 複数のコンポーネントが`m_Enabled: 0`に設定
- **追加されたコンポーネント**: XRDirectInteractorが車いすオブジェクトに直接追加 (fileIDs `188666284`, `2136797960`)

### 2. **初期化タイミングの問題**

#### A. XR Input Device の遅延初期化
```csharp
// VRWC_XRNodeVelocitySupplier.cs:29
yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
```
- Unity起動時、XR Input Deviceの初期化が完了していない
- `InputDevices.GetDeviceAtXRNode()`が無効なデバイスを返す

#### B. 初期化待機の無限ループ
```csharp
// VRWC_WheelInteractable.cs:111
yield return new WaitUntil(() => interactorVelocity.IsInitialized);
```
- `VRWC_XRNodeVelocitySupplier.IsInitialized`がfalseのまま
- BrakeAssist機能が永続的にブロックされる

### 3. **OpenXRスティック操作がなぜ「修正」するのか**

スティック操作により以下が発生：
1. OpenXR Input Subsystemの完全な初期化
2. Input Device enumerationの強制実行
3. `InputDevices.GetDeviceAtXRNode()`の有効化
4. VRWC_XRNodeVelocitySupplierの初期化完了

## 抜本的解決策

### **解決策1: シーン構造の根本的見直し (推奨)**

#### A. 車いすプレハブの完全な再構築
1. **既存の車いすプレハブを削除**
2. **新しいプレハブ作成**:
   - XR Origin子要素として配置
   - 独自のXR Originコンポーネントを削除
   - 正しいActionBasedControllerとの統合

#### B. XR Interaction構造の再設計
```
XR Origin (XR Rig)
├── Camera Offset
│   └── Main Camera
├── Left Controller (ActionBasedController)
│   ├── VRWC_XRNodeVelocitySupplier
│   └── XRDirectInteractor
├── Right Controller (ActionBasedController)
│   ├── VRWC_XRNodeVelocitySupplier
│   └── XRDirectInteractor
└── Wheelchair GameObject
    ├── WheelL (VRWC_WheelInteractable)
    └── WheelR (VRWC_WheelInteractable)
```

### **解決策2: 初期化システムの堅牢化**

#### A. XR Subsystemの明示的初期化確認
```csharp
// 新しいVRWC_XRInitializationManager.cs
public class VRWC_XRInitializationManager : MonoBehaviour
{
    public static bool IsXRFullyInitialized { get; private set; } = false;
    
    private void Start()
    {
        StartCoroutine(WaitForFullXRInitialization());
    }
    
    private IEnumerator WaitForFullXRInitialization()
    {
        // XR Subsystemの初期化待機
        yield return new WaitUntil(() => XRSettings.enabled && XRSettings.loadedDeviceName != "None");
        
        // Input Deviceの enumeration強制実行
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);
        
        // 各XRNodeのDevice有効性確認
        yield return new WaitUntil(() => 
            InputDevices.GetDeviceAtXRNode(XRNode.LeftHand).isValid &&
            InputDevices.GetDeviceAtXRNode(XRNode.RightHand).isValid);
            
        // 追加の安定化待機
        yield return new WaitForSeconds(0.5f);
        
        IsXRFullyInitialized = true;
    }
}
```

#### B. VRWC_XRNodeVelocitySupplierの修正
```csharp
private IEnumerator WaitForXRInitialization()
{
    // XR初期化マネージャーの完了待機
    yield return new WaitUntil(() => VRWC_XRInitializationManager.IsXRFullyInitialized);
    
    // デバイス有効性の再確認
    yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
    
    // タイムアウトの追加
    float timeout = 10f;
    float elapsed = 0f;
    
    while (!InputDevices.GetDeviceAtXRNode(trackedNode).isValid && elapsed < timeout)
    {
        elapsed += Time.deltaTime;
        yield return null;
    }
    
    if (elapsed >= timeout)
    {
        Debug.LogError($"XR Device for {trackedNode} failed to initialize within timeout");
        yield break;
    }
    
    _isInitialized = true;
}
```

#### C. VRWC_WheelInteractableの修正
```csharp
IEnumerator BrakeAssist(IXRSelectInteractor interactor)
{
    VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();

    if (interactorVelocity == null)
    {
        yield break;
    }

    // タイムアウト付きの初期化待機
    float timeout = 15f;
    float elapsed = 0f;
    
    yield return new WaitUntil(() => {
        elapsed += Time.deltaTime;
        return interactorVelocity.IsInitialized || elapsed >= timeout;
    });
    
    if (elapsed >= timeout)
    {
        Debug.LogWarning("VelocitySupplier initialization timeout - proceeding without brake assist");
        yield break;
    }

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

### **解決策3: Script Execution Orderの設定**

Unity Project Settingsで実行順序を設定：
1. `VRWC_XRInitializationManager` (実行順序: -100)
2. `VRWC_XRNodeVelocitySupplier` (実行順序: -50)
3. `VRWC_WheelInteractable` (実行順序: 0)

## 実装優先度

### **緊急対応 (即座に実装)**
1. `VRWC_XRInitializationManager`の作成と配置
2. `VRWC_XRNodeVelocitySupplier`のタイムアウト機能追加
3. `VRWC_WheelInteractable`のフェイルセーフ機能追加

### **根本修正 (1-2日以内)**
1. シーンのXR Origin構造見直し
2. 車いすプレハブの完全再構築
3. Script Execution Orderの設定

### **品質向上 (1週間以内)**
1. エラーハンドリングの強化
2. デバッグログの追加
3. 包括的テスト実施

## まとめ

前回の調査で特定された初期化タイミング問題は正しかったが、**根本原因はシーン構造レベルの設計問題**である。

1. **XR Originの競合**: 2つのXR Originシステムが混在
2. **プレハブ統合の不完全性**: 車いすプレハブが適切にXRシステムと統合されていない
3. **初期化順序の不明確性**: 依存関係が適切に管理されていない

単純なコード修正では解決できず、**シーン構造の抜本的見直し**が必要。上記の解決策により、初回の手回し操作を含む全ての機能が安定動作するようになる。