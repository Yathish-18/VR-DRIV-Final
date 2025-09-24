// GTANPCCar.cs
using UnityEngine;

public class NpcCar : MonoBehaviour
{
    public Transform[] path;
    public float minSpeed = 5f;
    public float maxSpeed = 10f;
    public float rotateSpeed = 3f;

    private float speed;
    private int index = 0;
    private bool isStopped = false;

    void Start()
    {
        speed = Random.Range(minSpeed, maxSpeed);
    }

    void Update()
    {
        if (isStopped || path.Length == 0) return;

        Vector3 target = path[index].position;
        Vector3 dir = target - transform.position;
        transform.position += dir.normalized * speed * Time.deltaTime;

        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, rotateSpeed * Time.deltaTime);

        if (dir.magnitude < 1f)
        {
            index++;
            if (index >= path.Length)
                Destroy(gameObject); // Or loop or return to pool
        }
    }

    public void StopTemporarily(float duration)
    {
        StartCoroutine(StopForSeconds(duration));
    }

    System.Collections.IEnumerator StopForSeconds(float sec)
    {
        isStopped = true;
        yield return new WaitForSeconds(sec);
        isStopped = false;
    }
}
