using UnityEngine;
using UnityEngine.AI;

public enum StateTruck
{
    IsWorking,
    End,
}

public class TruckMovement : MonoBehaviour
{
    private NavMeshAgent agent; // Компонент NavMeshAgent
    public StateTruck state = StateTruck.End;
    public Vector3 target;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        state = StateTruck.End;
        target = Vector3.zero;
    }

    void Update()
    {
        if(StateTruck.IsWorking == state)
        {
            // Проверяем, достигла ли текущая цель 
            if (Vector3.Distance(transform.position, agent.destination) < 3f)
            {
                Debug.Log("Доехал!");
                target = Vector3.zero;
                state = StateTruck.End;
            }
        }

        if (StateTruck.End == state)
        {
            if (target != Vector3.zero)
            {
                state = StateTruck.IsWorking;
                MoveToNextTarget();
            }
        }

    }

    public void MoveToNextTarget()
    {
        agent.destination = this.target;
        Debug.Log("Назначена цель с координатами: " + target);

        // Обработка достижения цели
        agent.SetDestination(target);
        agent.isStopped = false;
        agent.stoppingDistance = 0f;
        agent.destination = target;
    }

}