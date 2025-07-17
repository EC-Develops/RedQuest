using UnityEngine;

public class RemovedCodeHolder : MonoBehaviour
{
        // Freeze rotation at x && z = 0
        //player.transform.rotation = Quaternion.Euler(0f, player.transform.rotation.eulerAngles.y, 0f);
        //rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Unfreeze rotation
        //rb.constraints &= ~(RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ);

}
