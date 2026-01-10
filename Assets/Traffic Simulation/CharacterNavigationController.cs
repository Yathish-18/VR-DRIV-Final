using UnityEngine;

public class CharacterNavigationController : MonoBehaviour
{
    [Header("Navigation Settings")]
    public float movementSpeed = 1f;
    public float rotationSpeed = 120f;
    public float stopDistance = 2.5f;

    [Header("Destination")]
    public Vector3 destination;
    public bool reachedDestination = false;

    private Vector3 velocity;
    private Vector3 lastPosition;
    private Animator _animator;

    void Start()
    {
        lastPosition = transform.position;
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (transform.position != destination)
        {
            Vector3 destinationDirection = destination - transform.position;
            destinationDirection.y = 0;
            float destinationDistance = destinationDirection.magnitude;

            if (destinationDistance >= stopDistance)
            {
                reachedDestination = false;
                Quaternion targetRotation = Quaternion.LookRotation(destinationDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
                transform.Translate(Vector3.forward * movementSpeed * Time.deltaTime);
            }
            else
            {
                reachedDestination = true;
            }

            velocity = (transform.position - lastPosition) / Time.deltaTime;
            velocity.y = 0;
            var velocityMagnitude = velocity.magnitude;
            velocity = velocityMagnitude > 0.001f ? velocity.normalized : Vector3.zero;

            var fwdDotProduct = Vector3.Dot(transform.forward, velocity);
            var rightDotProduct = Vector3.Dot(transform.right, velocity);

            _animator.SetFloat("Horizontal", rightDotProduct);
            _animator.SetFloat("Forward", fwdDotProduct);
            lastPosition = transform.position;
        }
    }

    public void SetDestination(Vector3 destination)
    {
        this.destination = destination;
        reachedDestination = false;
    }
}
