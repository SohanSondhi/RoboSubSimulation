using UnityEngine;

/*
    Adapted from the following script by Windexglow 11-13-10.
    https://gist.github.com/gunderson/d7f096bd07874f31671306318019d996
*/

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float fastMultiplier = 2f;
    public float mouseSensitivity = 2.0f;
    public float maxPitchDegrees = 85f;
    private float _yaw;
    private float _pitch;

    private void Awake()
    {
        var euler = transform.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x;
    }

    private void Update()
    {
        HandleLook();
        HandleMove();
    }

    private void HandleLook()
    {
        // Only rotate camera while holding RMB to avoid interfering with UI.
        if (!Input.GetMouseButton(1))
            return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        _yaw += mouseX * mouseSensitivity;
        _pitch -= mouseY * mouseSensitivity;
        _pitch = Mathf.Clamp(_pitch, -maxPitchDegrees, maxPitchDegrees);

        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
    }

    private void HandleMove()
    {
        // Forward/back + left/right only. No vertical movement.
        float x = 0f;
        float z = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) x -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) z += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) z -= 1f;

        var input = new Vector3(x, 0f, z);
        if (input.sqrMagnitude <= 0f)
            return;

        input = input.normalized;

        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift))
            speed *= fastMultiplier;

        // Move relative to camera yaw (i.e., in camera local space).
        transform.Translate(input * (speed * Time.deltaTime), Space.Self);
    }
}
