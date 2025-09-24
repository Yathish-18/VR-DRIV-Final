using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

public class NpcNavCar : MonoBehaviour
{
    public Transform[] waypoints;
    private NavMeshAgent agent;
    private Transform currentTarget;
    private List<Transform> visited = new List<Transform>();

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.updateRotation = false;
        ChooseNextWaypoint();
    }

    void Update()
    {
        if (!agent.pathPending && agent.remainingDistance < 1f)
        {
            visited.Add(currentTarget); // Mark current as visited
            ChooseNextWaypoint();
        }

        // Smoothly rotate towards movement direction
        if (agent.velocity != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 2f);
        }
    }

    void ChooseNextWaypoint()
    {
        Transform next = null;
        float shortestDistance = Mathf.Infinity;

        foreach (Transform wp in waypoints)
        {
            if (visited.Contains(wp)) continue; // Skip visited

            float distance = Vector3.Distance(transform.position, wp.position);
            if (distance < shortestDistance)
            {
                shortestDistance = distance;
                next = wp;
            }
        }

        // If all are visited, reset visited list and try again
        if (next == null)
        {
            visited.Clear(); // Loop enabled
            ChooseNextWaypoint();
            return;
        }

        currentTarget = next;
        agent.SetDestination(currentTarget.position);
    }
}
