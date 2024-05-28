using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum StateDrone {
    TakeOff,
    MoveToward,
    Landing,
    End,
    IsDischarged,
}

public class DroneMovement : MonoBehaviour
{
    public GameObject goal;
    public GameObject platform;
    private bool productIsDelivered;
    public float speed;
    private float maxHeight = 45.0f;
    public StateDrone state = StateDrone.End;
    public float currentAccum;
    private Vector3 droneVector = new Vector3(0, 0, 0);
    private Vector3 goalVector = new Vector3(0, 0, 0);

    void Start()
    {
        Application.targetFrameRate = 50;
    }

    void Update()
    {

        if(state == StateDrone.IsDischarged) {
            //Debug.Log("StateDrone.IsDischarged");
            return;
        }

        if(state == StateDrone.End) {
            //Debug.Log("StateDrone.End");
            return;
        }

        droneVector = new Vector3(transform.position.x, maxHeight, transform.position.z);       
        goalVector = new Vector3(goal.transform.position.x, maxHeight, goal.transform.position.z); 
        var direction = goalVector - droneVector;
        var distance = direction.magnitude;
        var normilizedDirection = direction / distance;

        if(state == StateDrone.TakeOff) {
            if(transform.position.y < maxHeight) {
                transform.position += Vector3.up * Time.deltaTime * speed;
                //Debug.Log("StateDrone.TakeOff");
            }
            else {
                state = StateDrone.MoveToward;
            }
            currentAccum = currentAccum - 0.02f;
        }

        if(state == StateDrone.MoveToward) {
            if(Vector3.Distance(goalVector, droneVector) > 0.5f) {
                transform.position += normilizedDirection * Time.deltaTime * speed;
                //Debug.Log("StateDrone.Move");
            }
            else {
                state = StateDrone.Landing;
            }
            currentAccum = currentAccum - 0.02f;
        }

        if(state == StateDrone.Landing) {
            if(transform.position.y > goal.transform.position.y + 0.5f) {
                transform.position += Vector3.down * Time.deltaTime * speed;
                //Debug.Log("StateDrone.Landing");
            }
            else {
                if(productIsDelivered == false) {
                    goal.SetActive(false);
                    goal = platform;
                    goalVector = new Vector3(goal.transform.position.x, maxHeight, goal.transform.position.z);
                    state = StateDrone.TakeOff;
                    productIsDelivered = true;
                }
                else {
                    state = StateDrone.End;
                }
            }
            currentAccum = currentAccum - 0.02f;
        }

        if(state == StateDrone.End) {
            productIsDelivered = false;
            transform.parent = platform.transform;
            //Debug.Log("StateDrone.End");
        }

    }

    public float getCurrentAccum() {
        return currentAccum;
    }

    public StateDrone GetState() {
        return state;
    }

    public void SetState(StateDrone state) {
        this.state = state;
    }

    public void SetGoal(GameObject goal) {
        this.goal = goal;
    }
}
