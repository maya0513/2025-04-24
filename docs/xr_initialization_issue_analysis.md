# Unity XR及びOpenXRの初期化順序問題調査報告

## 概要
VR車いすプロジェクトで、アプリケーション起動時に最初の車いすタイヤの手回し操作が効かないバグの原因を調査しました。このバグは、OpenXRとXR Interaction Toolkitの初期化順序問題に起因しています。

## 問題の詳細

### 症状
- アプリケーション起動後、最初の車いすタイヤの手回し操作が反応しない
- OpenXR標準のスティック操作で車いすを少し動かすと、その後タイヤの手回し操作が効くようになる

### 根本原因
Unity XR及びOpenXRの初期化順序とInputDevicesの可用性タイミングの問題

## 初期化順序の詳細分析

### 1. XRシステムの初期化タイミング

#### XRGeneralSettings.cs の初期化プロセス
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
internal static void AttemptInitializeXRSDKOnLoad()
{
    XRGeneralSettings instance = XRGeneralSettings.Instance;
    if (instance == null || !instance.InitManagerOnStart)
        return;
    instance.InitXRSDK();
}

[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
internal static void AttemptStartXRSDKOnBeforeSplashScreen()
{
    XRGeneralSettings instance = XRGeneralSettings.Instance;
    if (instance == null || !instance.InitManagerOnStart)
        return;
    instance.StartXRSDK();
}
```

#### XRManagerSettings.cs の初期化プロセス
```csharp
private void InitXRSDK()
{
    m_XRManager.automaticLoading = false;
    m_XRManager.automaticRunning = false;
    m_XRManager.InitializeLoaderSync();
    m_ProviderIntialized = true;
}

private void StartXRSDK()
{
    if (m_XRManager != null && m_XRManager.activeLoader != null)
    {
        m_XRManager.StartSubsystems();
        m_ProviderStarted = true;
    }
}
```

### 2. InputDevices.GetDeviceAtXRNode()がvalidになるタイミング

#### OpenXRLoader.cs の初期化プロセス
```csharp
private bool InitializeInternal()
{
    OpenXRInput.RegisterLayouts();
    OpenXRFeature.Initialize();
    
    if (!LoadOpenXRSymbols()) return false;
    if (!Internal_InitializeSession()) return false;
    
    RequestOpenXRFeatures();
    RegisterOpenXRCallbacks();
    
    if (!CreateSubsystems()) return false;
    
    Application.onBeforeRender += ProcessOpenXRMessageLoop;
    currentLoaderState = LoaderState.Initialized;
    return true;
}
```

**重要**: InputDevicesは、OpenXRのセッション初期化とサブシステム作成が完了してから利用可能になります。これは`RuntimeInitializeLoadType.BeforeSplashScreen`のタイミングで実行されます。

### 3. VRWC_XRNodeVelocitySupplierのWaitForXRInitialization()の問題

#### 現在の実装 (`VRWC_XRNodeVelocitySupplier.cs`)
```csharp
private void Start()
{
    InputDevices.GetDeviceAtXRNode(trackedNode).TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
    StartCoroutine(WaitForXRInitialization());
}

private IEnumerator WaitForXRInitialization()
{
    yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
    // 速度取得処理 - しかし実装が不完全
}
```

**問題点**:
1. `Start()`で即座にInputDevicesにアクセスしているが、この時点ではまだvalidでない可能性が高い
2. `WaitForXRInitialization()`コルーチンが不完全で、初期化完了後の処理が実装されていない

### 4. XRBaseInteractableのSelectEnterイベントが最初に発火するタイミング

#### VRWC_WheelInteractable.cs の問題
```csharp
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
    // ...
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
        // interactorVelocity.velocity.z を使用してブレーキ判定
        if (interactorVelocity.velocity.z < 0.05f && interactorVelocity.velocity.z > -0.05f)
        {
            // ブレーキ処理
        }
        yield return new WaitForFixedUpdate();
    }
}
```

### 5. 初期化順序の問題で最初のインタラクションが失敗する理由

#### タイミングチャート
```
1. RuntimeInitializeLoadType.AfterAssembliesLoaded
   └─ XRGeneralSettings.AttemptInitializeXRSDKOnLoad()
      └─ OpenXRLoader.InitializeInternal()

2. RuntimeInitializeLoadType.BeforeSplashScreen  
   └─ XRGeneralSettings.AttemptStartXRSDKOnBeforeSplashScreen()
      └─ XRManagerSettings.StartSubsystems()

3. MonoBehaviour.Awake() (XRInteractionManager等)

4. MonoBehaviour.Start() (VRWC_XRNodeVelocitySupplier等)
   ├─ この時点でInputDevices.GetDeviceAtXRNode().isValidがfalseの場合が多い
   └─ WaitForXRInitialization()が開始されるが、実装が不完全

5. 最初のSelectEnterEvent
   └─ VRWC_XRNodeVelocitySupplier.velocityが正しく取得できていない
```

## 解決方法

### 1. VRWC_XRNodeVelocitySupplier.cs の修正

```csharp
private void Start()
{
    // Start()での即座のアクセスは削除
    StartCoroutine(WaitForXRInitialization());
}

private IEnumerator WaitForXRInitialization()
{
    // InputDeviceがvalidになるまで待機
    yield return new WaitUntil(() => InputDevices.GetDeviceAtXRNode(trackedNode).isValid);
    
    // 初期化完了後、継続的な速度取得を開始
    StartCoroutine(UpdateVelocityContinuously());
}

private IEnumerator UpdateVelocityContinuously()
{
    while (true)
    {
        var device = InputDevices.GetDeviceAtXRNode(trackedNode);
        if (device.isValid)
        {
            device.TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
        }
        yield return null; // 毎フレーム更新
    }
}

void Update()
{
    // Update()での処理は削除するか、isValidチェックを追加
    var device = InputDevices.GetDeviceAtXRNode(trackedNode);
    if (device.isValid)
    {
        device.TryGetFeatureValue(CommonUsages.deviceVelocity, out _velocity);
    }
}
```

### 2. VRWC_WheelInteractable.cs の修正

```csharp
IEnumerator BrakeAssist(IXRSelectInteractor interactor)
{
    VRWC_XRNodeVelocitySupplier interactorVelocity = interactor.transform.GetComponent<VRWC_XRNodeVelocitySupplier>();
    
    if (interactorVelocity == null)
    {
        yield break;
    }
    
    // XRデバイスの初期化を待機
    yield return new WaitUntil(() => 
    {
        var device = InputDevices.GetDeviceAtXRNode(
            interactorVelocity.trackedNode); // trackedNodeをpublicにする必要あり
        return device.isValid;
    });
    
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
```

### 3. 代替解決方法: 遅延初期化

```csharp
private IEnumerator DelayedBrakeAssist(IXRSelectInteractor interactor)
{
    yield return new WaitForSeconds(0.5f); // XRシステム完全安定化まで追加待機
    StartCoroutine(BrakeAssist(interactor));
}
```

## まとめ

この問題は、Unity XR/OpenXRの初期化が完了する前に`VRWC_XRNodeVelocitySupplier`が`InputDevices.GetDeviceAtXRNode()`にアクセスしようとすることで発生しています。最初のインタラクション時にはInputDeviceがまだvalidでないため、速度データが取得できず、ブレーキアシスト機能が正常に動作しません。

OpenXR標準のスティック操作後に手回し操作が効くようになるのは、その時点でXRシステムの初期化が完全に完了し、InputDeviceがvalidになるためです。

上記の修正を適用することで、XRシステムの初期化完了を適切に待機し、最初のインタラクションから正常に動作するようになります。