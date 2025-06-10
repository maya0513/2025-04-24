using UnityEngine.XR.Interaction.Toolkit;

/// VRWheelControlとの物理インタラクションを仲介する使い捨てのグラブポイントを提供します。
/// GrabPointは選択されなくなった時にGameObjectを破棄します。
/// GrabPointはUnityのXR Interaction ToolkitのXRGrabInteractableを継承しています。

public class VRWC_GrabPoint : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    protected override void Awake()
    {
        base.Awake();

        // インタラクタブルのデフォルト設定を構成
        movementType = MovementType.VelocityTracking;
        trackRotation = false;
        throwOnDetach = false;
    }

    protected override void OnSelectExited(SelectExitEventArgs args)
    {
        base.OnSelectExited(args);

        // 選択終了時にグラブポイントオブジェクトを破棄
        Destroy(gameObject);
    }
}