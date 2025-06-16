1. VRWC_XRNodeVelocitySupplier.cs (Lines 13,20,24,29-35,40-42):
 - _isInitializedフラグとIsInitializedプロパティを追加
 - Start()メソッドから即座のInputDevice呼び出しを削除
 - WaitForXRInitialization()を完全実装し、初期化完了後にフラグを設定
 - Update()で初期化完了チェックを追加
2. VRWC_WheelInteractable.cs (Line 111):
 - BrakeAssist()に初期化完了待機を追加