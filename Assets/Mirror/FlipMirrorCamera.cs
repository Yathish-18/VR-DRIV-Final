using UnityEngine;

[RequireComponent(typeof(Camera))]
public class FlipMirrorCamera : MonoBehaviour
{
    void Start()
    {
        Camera cam = GetComponent<Camera>();

        // Flip the projection horizontally
        cam.projectionMatrix = cam.projectionMatrix * Matrix4x4.Scale(new Vector3(-1, 1, 1));

        // Fix culling (otherwise geometry may be invisible or reversed)
        cam.ResetWorldToCameraMatrix();
        cam.ResetProjectionMatrix();
        cam.projectionMatrix *= Matrix4x4.Scale(new Vector3(-1, 1, 1));

        // Also invert front face culling
        GL.invertCulling = true;
    }

    void OnDisable()
    {
        // Reset when camera is disabled to avoid affecting other cameras
        GL.invertCulling = false;
    }
}
