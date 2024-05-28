using Accord.MachineLearning;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class Operator : MonoBehaviour
{
    private WebSocketSharp.WebSocket ws;
    public List<GameObject> points;
    private NavMeshHit hit; // Структура для хранения информации о найденной точке

    private List<Vector3> for_test = new List<Vector3>();
    private int currentTargetIndex = 0; // Индекс текущей цели
    private NavMeshAgent agent; // Компонент NavMeshAgent

    /// <summary>
    /// ////////////////////////////////////////////////////////////////////
    /// </summary>

    public GameObject truckTrailer;
    private GameObject[] drones;
    private GameObject[] goals;
    private float maxHeight = 45.0f;
    private int[,] matrix;
    private List<Queue<int>> goalsForDrones;
    private float[] accumForDrones;

    void Start()
    {
        Debug.Log("Запускаюсь!");
        agent = GetComponent<NavMeshAgent>();
        ws = new WebSocketSharp.WebSocket("ws://localhost:8765");
        //getDrones();

        GetOptimalRoute();

        ws.OnMessage += (sender, e) =>
        {
            Debug.Log("Вывод: " + e.Data); //Вывод координат центра изображения
            Data newData = Newtonsoft.Json.JsonConvert.DeserializeObject<Data>(e.Data);
            List<Vector3> routes = new List<Vector3>();
            for (int i = 1; i < newData.sorted_stops.Length; i++)
            {
                Vector3 route = new Vector3(newData.sorted_stops[i][0], 1, newData.sorted_stops[i][1]);
                routes.Add(route);
            }
            this.for_test.AddRange(routes);
        };

        float duration = 2f;
        StartCoroutine(TestRoutine(duration));
    }

    // Update is called once per frame
    void Update()
    {
        if (truckTrailer.GetComponent<TruckMovement>().state == StateTruck.End)
        {
            // Есть ещё цели?
            if (for_test.Count > currentTargetIndex)
            {
                truckTrailer.GetComponent<TruckMovement>().target = for_test[currentTargetIndex];
                currentTargetIndex++;
            }
        }
    }

    IEnumerator TestRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration); // Ждать 2 секунды
    }

    private void GetOptimalRoute()
    {
        List<List<float>> normalizedPoints = NormalizeData();
        GetSolution(normalizedPoints);
    }

    private List<List<float>> NormalizeData()
    {
        List<List<float>> normalizedPoints = new List<List<float>>();
        for (int i = 0; i < points.Count; i++)
        {
            List<float> coordinatesForPoint = new List<float>() { points[i].transform.position.x, points[i].transform.position.z };
            normalizedPoints.Add(coordinatesForPoint);
            //Debug.Log("Клиент " + i + ", x: " + normalizedPoints[i][0] + "\tz: " + normalizedPoints[i][1]);
        }
        return normalizedPoints;
    }

    private void GetSolution(List<List<float>> normalizedPoints)
    {
        int cluster = 1;
        double[][][] initialCentersList = new double[normalizedPoints.Count - 1][][];
        float[][][] truckMatrixDistancesForEachClusters = new float[normalizedPoints.Count - 1][][];
        while (cluster != normalizedPoints.Count)
        {
            // 1. Первоначальная кластеризация
            KMeans kmeans = new KMeans(cluster);
            double[][] floatArray = ConvertListToFloatArray(normalizedPoints.GetRange(1, normalizedPoints.Count - 1));
            kmeans.Learn(floatArray);

            // 2. Получение первоначальных центров кластеров
            double[][] initialCenters = kmeans.Centroids;
            /*            foreach (double[] i in initialCenters)
                        {
                            Debug.Log("Координаты центроиды: (" + i[0] + "), (" + i[1] + ")");
                        }*/

            // 3. Пересчет центров кластеров
            for (int i = 0; i < initialCenters.Length; i++)
            {
                Vector3 vector = new Vector3((float)initialCenters[i][0], 1, (float)initialCenters[i][1]);
                NavMesh.SamplePosition(vector, out hit, float.MaxValue, NavMesh.AllAreas);
                initialCenters[i] = new double[] { hit.position[0], hit.position[2] };
                /*                Debug.Log("Пересчитанные центроиды: (" + initialCenters[i][0] + "), (" + initialCenters[i][1] + ")");*/
            }
            initialCentersList[cluster - 1] = initialCenters;

            // 4. Создание матрицы расстояний
            float[][] matrixOfDistancesForTruck = CreateMatrixOfDistancesForTruck(initialCenters);
            truckMatrixDistancesForEachClusters[cluster - 1] = matrixOfDistancesForTruck;

            // 5. Увеличиваем число разбиений
            cluster++;
        }
        ConnectToPythonServer(normalizedPoints, initialCentersList, truckMatrixDistancesForEachClusters);

    }

    private double[][] ConvertListToFloatArray(List<List<float>> inputList)
    {
        // Определение размера выходного массива
        double[][] resultArray = new double[inputList.Count][];

        // Проход по каждой строке входного списка
        for (int i = 0; i < inputList.Count; i++)
        {
            // Создание нового массива для текущей строки
            resultArray[i] = new double[inputList[i].Count];

            // Проход по элементам каждой строки
            for (int j = 0; j < inputList[i].Count; j++)
            {
                // Копирование значения из списка в массив
                resultArray[i][j] = inputList[i][j];
            }
        }

        return resultArray;
    }

    private float[][] CreateMatrixOfDistancesForTruck(double[][] initialCenters)
    {
        double[][] initialCentersCopy = new double[initialCenters.Length + 1][];
        initialCentersCopy[0] = new double[] { points[0].transform.position.x, points[0].transform.position.z };
        for (int i = 1; i < initialCentersCopy.Length; i++)
        {
            initialCentersCopy[i] = initialCenters[i - 1];
        }
        initialCenters = initialCentersCopy;

        float[][] matrixOfDistancesForTruck = new float[initialCenters.Length][];

        for (int index_for_point1 = 0; index_for_point1 < matrixOfDistancesForTruck.Length; index_for_point1++)
        {
            float[] distances_for_one_cluster_to_another = new float[matrixOfDistancesForTruck.Length];
            Vector3 point1 = new Vector3((float)initialCenters[index_for_point1][0], 1, (float)initialCenters[index_for_point1][1]);
            for (int index_for_point2 = 0; index_for_point2 < matrixOfDistancesForTruck.Length; index_for_point2++)
            {
                Vector3 point2 = new Vector3((float)initialCenters[index_for_point2][0], 1, (float)initialCenters[index_for_point2][1]);
                float closestTargetDistance = float.MaxValue;
                NavMeshPath Path = null;
                NavMeshPath ShortestPath = null;

                Path = new NavMeshPath();
                /*                Debug.Log("Центроида 1: " + point1);
                                Debug.Log("Центроида 2: " + point2);*/
                if (NavMesh.CalculatePath(point1, point2, agent.areaMask, Path))
                {
                    float distance = Vector3.Distance(point1, Path.corners[0]);
                    /*                    Debug.Log("Path: " + string.Join(" ", Path.corners));*/
                    for (int j = 1; j < Path.corners.Length; j++)
                    {
                        distance += Vector3.Distance(Path.corners[j - 1], Path.corners[j]);
                    }

                    if (distance < closestTargetDistance)
                    {
                        closestTargetDistance = distance;
                        ShortestPath = Path;
                        /*                        Debug.Log(point1 + " => " + point2 + ": " + closestTargetDistance);*/
                        distances_for_one_cluster_to_another[index_for_point2] = closestTargetDistance;
                    }

                    /*                    Debug.Log("Ближайшая дистанция к цели: " + closestTargetDistance);*/
                }
                else
                {
                    Debug.Log("Не могу найти путь!");
                }

            }
            matrixOfDistancesForTruck[index_for_point1] = distances_for_one_cluster_to_another;
        }

        return matrixOfDistancesForTruck;
    }

    private void ConnectToPythonServer(List<List<float>> normalizedPoints, double[][][] initialCentersList, float[][][] truckMatrixDistancesForEachClusters)
    {
        ws.Connect();
        var data = new
        {
            normalizedPoints,
            initialCentersList,
            truckMatrixDistancesForEachClusters,
        };
        string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(data);
        // Debug.Log("jsonData: " + jsonData);
        ws.Send(jsonData); //Отправляет запрос на сервер
    }

    private void getDrones() {
        GameObject Platforms = truckTrailer.transform.GetChild(2).gameObject;
        drones = new GameObject[Platforms.transform.childCount];

        for(int i = 0; i < drones.Length; i++) {
            drones[i] = truckTrailer.transform.GetChild(2).gameObject.
                                    transform.GetChild(i).gameObject.
                                    transform.GetChild(0).gameObject;
        }
    }

    private void getGoals() {
        GameObject Targets = GameObject.Find("Targets");
        goals = new GameObject[Targets.transform.childCount];

        for(int i = 0; i < goals.Length; i++) {
            goals[i] = Targets.transform.GetChild(i).gameObject;
        }
    }

    private float CalculateDistance(GameObject drone, GameObject goal) {
        float distance = 0;
        distance += Vector3.Distance(drone.transform.position, new Vector3(drone.transform.position.x, maxHeight, drone.transform.position.z));
        distance += Vector3.Distance(new Vector3(drone.transform.position.x, maxHeight, drone.transform.position.z),
                                    new Vector3(goal.transform.position.x, maxHeight, goal.transform.position.z));
        distance += Vector3.Distance(new Vector3(goal.transform.position.x, maxHeight, goal.transform.position.z),
                                    new Vector3(goal.transform.position.x, 0.5f, goal.transform.position.z));
        distance *= 2;
        return distance;
    }

    //За 1 секунду расходуется один заряд аккумултора
    private float CalculateBatteryCharge(float distance, float speed) {
        float qt = 1.0f;
        return (distance/speed)*qt;
    }

    private float DeltaQ(float QofDrone, float QofDistance) {
        return QofDistance/QofDrone;
    }

    private int NormilizeDeltaQ(float deltaQ) {
        //print(deltaQ);
        if(deltaQ > 1) {
            return Int32.MaxValue;
        }
        return (int)(deltaQ * 100000);
    }

    private void AssignGoalsToDrones(int[] assignementArray) {
        for(int i = 0; i < drones.Length; i++) {
            if(drones[i].GetComponent<DroneMovement>().GetState() != StateDrone.IsDischarged &&
            drones[i].GetComponent<DroneMovement>().GetState() == StateDrone.End) {
                if(goalsForDrones[i].Peek() != -1) {
                    drones[i].GetComponent<DroneMovement>().SetGoal(goals[goalsForDrones[i].Dequeue()]);
                    drones[i].GetComponent<DroneMovement>().SetState(StateDrone.TakeOff);
                    drones[i].transform.parent = null;
                }
                else {
                    drones[i].GetComponent<DroneMovement>().SetState(StateDrone.IsDischarged);
                }
            }
        }
    }

}

public class Data
{
    public float[][] sorted_stops { get; set; }
/*    public float[][] drone_route { get; set; }*/

}