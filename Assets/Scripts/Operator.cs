using Accord.MachineLearning;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.AI;

public class Operator : MonoBehaviour
{
    private WebSocketSharp.WebSocket ws;
    public List<GameObject> points;
    private bool[] isPointDelivered;
    private NavMeshHit hit; // Структура для хранения информации о найденной точке

    private List<Vector3> for_test = new List<Vector3>();
    private int[][] drone_route;
    private int currentTargetIndex = 0; // Индекс текущей цели
    private NavMeshAgent agent; // Компонент NavMeshAgent

    /// <summary>
    /// ////////////////////////////////////////////////////////////////////
    /// </summary>

    public GameObject truckTrailer;
    private GameObject[] drones;
    private float maxHeight = 45.0f;
    private float[] accumForDrones;

    private int[][] testForDrone = { new int[] { 1, 0, 2 }, new int[] { 4, 6, 5 }, new int[] { 3, 7, -1 } };
    public static bool methodStartIsFinished = false;
    public bool IsPermitedForTruck = true;
    public bool[] IsPermitedForDrone;

    void Start()
    {
        IsPermitedForDrone = new bool[3];
        IsPermitedForDrone[0] = true;
        IsPermitedForDrone[1] = true;
        IsPermitedForDrone[2] = true;
        agent = GetComponent<NavMeshAgent>();
        ws = new WebSocketSharp.WebSocket("ws://localhost:8765");
        getDrones();
        isPointDelivered = Enumerable.Repeat(false, points.Count).ToArray();

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
            this.drone_route = newData.drone_route;

        };

        float duration = 2f;
        StartCoroutine(TestRoutine(duration));
    }

    IEnumerator TestRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration); // Ждать 2 секунды
        methodStartIsFinished = true;
    }

    // Update is called once per frame
    void Update()
    {
        if (!methodStartIsFinished)
        {
            Debug.Log("Не пропущу");
            return;
        }

        //Debug.Log("state: " + truckTrailer.GetComponent<TruckMovement>().state);
        if (truckTrailer.GetComponent<TruckMovement>().state == StateTruck.End)
        {
            // Есть ещё цели?
            if (IsPermitedForTruck && for_test.Count > currentTargetIndex)
            {
                truckTrailer.GetComponent<TruckMovement>().target = for_test[currentTargetIndex];
                currentTargetIndex++;
                IsPermitedForTruck = false;
                for (int drone_index = 0; drone_index < drones.Length; drone_index++)
                {
                    Debug.Log(IsPermitedForDrone[drone_index]);
                    IsPermitedForDrone[drone_index] = true;
                }
                return;
            }

            int x = 0;
            for (int drone_index = 0; drone_index < drones.Length; drone_index++)
            {
                if (drones[drone_index].GetComponent<DroneMovement>().state == StateDrone.End && 
                    drones[drone_index].GetComponent<DroneMovement>().targets.Count == 0)
                {
                    if (IsPermitedForDrone[drone_index])
                    {
                        //Распределение клиентов по дронам
                        List<GameObject> targets = new List<GameObject>();
                        for (int client_index = (drone_route[currentTargetIndex - 1].Length/3)* drone_index;
                            client_index < (drone_route[currentTargetIndex - 1].Length / 3) * drone_index + (drone_route[currentTargetIndex - 1].Length / 3);
                            client_index++)
                        {
                            if (drone_route[currentTargetIndex - 1][client_index] != -1)
                            {
                                targets.Add(points[drone_route[currentTargetIndex - 1][client_index] + 1]);
                                Debug.Log("Клиент: " + points[drone_route[currentTargetIndex - 1][client_index] + 1]);
                            }
                        }
                        drones[drone_index].GetComponent<DroneMovement>().targets = targets;
                        IsPermitedForDrone[drone_index] = false;
                    }
                    else
                    {
                        x++;
                    }
                }
            }
            if (x == 3)
            {
                IsPermitedForTruck = true;
            }
        }

/*        for (int drone_index = 0; drone_index < drones.Length; drone_index++)
        {
            if (drones[drone_index].GetComponent<DroneMovement>().targets.Count == 0)
            {

            }
        }
*/

        /*        Debug.Log("StateTruck: " + truckTrailer.GetComponent<TruckMovement>().state);
                if (!isLocked && truckTrailer.GetComponent<TruckMovement>().state == StateTruck.End)
                {
                    Debug.Log("Я прошёл!");
                    bool are_drones_ready = false;

                    for (int drone_index = 0; drone_index < drones.Length; drone_index++)
                    {
                        if (drones[drone_index].GetComponent<DroneMovement>().state != StateDrone.End)
                        {
                            are_drones_ready = false;
                            break;
                        }
                        else
                        {
                            are_drones_ready = true;
                        }
                    }

                    for (int drone_index = 0; drone_index < drones.Length; drone_index++)
                    {
                        if (isPointDelivered[drone_route[currentTargetIndex][drone_index] + 1] == true)
                        {
                            continue;
                        }
                        if (are_drones_ready && drones[drone_index].GetComponent<DroneMovement>().state == StateDrone.End)
                        {
                            if (drone_route[currentTargetIndex][drone_index] == -1)
                            {
                                continue;
                            }
                            Debug.Log("Назначаю новые цели!");
                            drones[drone_index].GetComponent<DroneMovement>().goal = points[drone_route[currentTargetIndex][drone_index] + 1];
                            drones[drone_index].GetComponent<DroneMovement>().state = StateDrone.TakeOff;
                            isPointDelivered[drone_route[currentTargetIndex][drone_index] + 1] = true;
                            // Как только назначили цель дрону надо в массиве поставить true, т.е. клиент был обслужен
                            // И сверху прикрутить дополнительную проверку на то, была ли обслужена цель
                        }
                    }



                    // Есть ещё цели?
                    if (are_drones_ready && for_test.Count > currentTargetIndex)
                    {
                        truckTrailer.GetComponent<TruckMovement>().target = for_test[currentTargetIndex];
                        currentTargetIndex++;
                    }
                }*/

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
            maxHeight,
            heightTruck = GetComponent<BoxCollider>().size.y,
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

}

public class Data
{
    public float[][] sorted_stops { get; set; }
    public int[][] drone_route { get; set; }

}