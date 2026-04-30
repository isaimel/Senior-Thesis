using UnityEngine;

public class RotatingCamera : MonoBehaviour
{
    [SerializeField] float camDistance = 60f;
    [SerializeField] float camHeight = 35f;
    [SerializeField] float camSpeed = 20f;
    [SerializeField] bool rotateCamera = true;

    void Update()
    {
        if (rotateCamera)
        {
            RotateCamera();
        }
    }

    void RotateCamera()
    {
        Vector3 center = Vector3.zero;

        float angle = Time.time * camSpeed;
        float rad = angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Cos(rad) * camDistance,
            camHeight,
            Mathf.Sin(rad) * camDistance
        );

        transform.position = center + offset;
        transform.LookAt(center);
    }
}