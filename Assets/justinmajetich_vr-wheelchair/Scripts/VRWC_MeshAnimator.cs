using UnityEngine;

// 車椅子のメッシュをアニメーション化して、リグの物理的な動きを表現します

public class VRWC_MeshAnimator : MonoBehaviour
{
    public Rigidbody frame;

    public Transform wheelLeft;
    public Transform wheelRight;

    public Rigidbody casterLeftRB;
    public Rigidbody casterRightRB;

    public Transform wheelLeftMesh;
    public Transform wheelRightMesh;

    public Transform forkLeftMesh;
    public Transform forkRightMesh;

    public Transform casterLeftMesh;
    public Transform casterRightMesh;


    void Update()
    {
        if (frame.linearVelocity.magnitude > 0.05f)
        {
            RotateWheels();
            RotateFork();
            RotateCaster();
        }
    }

    void RotateWheels()
    {
        wheelLeftMesh.rotation = wheelLeft.rotation;
        wheelRightMesh.rotation = wheelRight.rotation;
    }

    void RotateFork()
    {
        forkLeftMesh.rotation = Quaternion.Slerp(forkLeftMesh.rotation, Quaternion.LookRotation(frame.linearVelocity.normalized, transform.up), Time.deltaTime * 8f);
        forkRightMesh.rotation = Quaternion.Slerp(forkRightMesh.rotation, Quaternion.LookRotation(-frame.linearVelocity.normalized, transform.up), Time.deltaTime * 8f);
    }

    void RotateCaster()
    {
        casterLeftMesh.Rotate(-Vector3.right, casterLeftRB.angularVelocity.magnitude);
        casterRightMesh.Rotate(Vector3.right, casterRightRB.angularVelocity.magnitude);
    }
}
