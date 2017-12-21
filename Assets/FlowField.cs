using System.Collections;
using System.Collections.Generic;
using UnityEngine;

class V2
{
    public int x, y;
    public V2(int x,int y)
    {
        this.x = x;
        this.y = y;
    }
}

class Agent
{
    public Vector3 position = Vector2.zero;
    public int rotation = 0;
    public Vector2 velocity = Vector2.zero;
    public int maxForce = 20;//rate of acceleration
    public int maxSpeed = 4;    //grid squares / second
    public float radius = 0.4f;
    public float minSeparation = 0.8f;// We'll move away from anyone nearer than this
    public float maxCohesion = 3.5f; //We'll move closer to anyone within this bound
    public Vector2 forceToApply;
    GameObject obj;
    public Agent(Vector3 pos)
    {
        this.position = pos;
        obj = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        obj.transform.position = pos;        
    }
    public void SetPos(Vector3 pos)
    {
        this.position = pos;
        obj.transform.position = pos;
    }
}


public class FlowField : MonoBehaviour {

    public GameObject cube;
    const int Width = 25;
    const int Height = 14;
    V2 destination = new V2(Width - 2, Height / 2);
    List<Agent> agents = new List<Agent>();
    List<V2> obstacles = new List<V2>();
    int[][] dijkstraGrid;
    Vector2[][] flowField;
    Dictionary<int, MeshRenderer> cubes = new Dictionary<int, MeshRenderer>();
    GameObject AddCube(int x,int y,Vector3 pos,Color color)
    {
        GameObject obj = Instantiate<GameObject>(cube);
        obj.transform.position = pos;
        MeshRenderer mr = obj.GetComponent<MeshRenderer>();
        mr.material.color = color;
        cubes.Add(x + y * Width,mr);
        return obj;
    }

    void SetCubeColor(int x,int y,Color color)
    {
        int index = x + y * Width;
        MeshRenderer mr = null;
        if (cubes.TryGetValue(index, out mr))
            mr.material.color = color;
        else
            Debug.LogError("can not find cube");
            
    }

    void SetCubeText(int x,int y,string text)
    {
        int index = x + y * Width;
        MeshRenderer mr = null;
        if (cubes.TryGetValue(index, out mr))
            mr.GetComponentInChildren<TextMesh>().text = text;
        else
            Debug.LogError("can not find cube");
    }

    void Start()
    {
        for(var y = 0; y < Height - 1;y++)
        {
            agents.Add(new Agent(new Vector3(0, 0,y)));
        }
        int count = 0;
        while(count++ < Width*Height/4)
        {
            int x = Random.Range(0, Height - 1);
            int y = Random.Range(1, Width - 1);
            if (x == destination.x && y == destination.y)
                continue;
            obstacles.Add(new V2(x, y));
        }
        generateDijkstraGrid();
        generateFlowField();
        SetCubeColor(destination.x, destination.y, Color.black);
    }

    void generateDijkstraGrid()
    {
        dijkstraGrid = new int[Width][];
        for (var x = 0; x < Width; x++)
        {
            dijkstraGrid[x] = new int[Height];
            for (var y = 0; y < Height; y++)
            {
                dijkstraGrid[x][y] = 1000;
                AddCube(x, y, new Vector3(x, 0f, y), Color.green);
            }
        }

        for(var i = 0; i < obstacles.Count;i++)
        {
            var t = obstacles[i];
            dijkstraGrid[t.y][t.x] = int.MaxValue;
            SetCubeColor(t.y, t.x, Color.red);
        }
        V2 pathEnd = destination;
        dijkstraGrid[pathEnd.x][pathEnd.y] = 0;
        //pathEnd.distance = 0;
        List<V2> toVisit = new List<V2>();
        toVisit.Add(pathEnd);

        for(int i = 0; i < toVisit.Count;i++)
        {
            var visit = toVisit[i];
            var neighbours = straightNeighboursOf(visit);
            for(var j = 0; j < neighbours.Count;j++)
            {
                var n = neighbours[j];
                if(dijkstraGrid[n.x][n.y] == 1000)
                {
                    int dis = dijkstraGrid[visit.x][visit.y] + 1;
                    dijkstraGrid[n.x][n.y] = dis;
                    SetCubeText(n.x, n.y, dis.ToString());
                    toVisit.Add(n);
                }
            }
        }
    }

    void generateFlowField()
    {
        flowField = new Vector2[Width][];
        for (var x = 0; x < Width; x++)
        {
            flowField[x] = new Vector2[Height];
            for (var y = 0; y < Height; y++)
            {
                flowField[x][y] = Vector2.zero;
            }
        }
        for(var x = 0; x < Width;x++)
            for(var y = 0; y < Height;y++)
            {
                if (dijkstraGrid[x][y] == int.MaxValue)
                    continue;
                V2 pos = new V2(x, y);
                var neighbours = allNeighboursOf(pos);
                V2 min = null;
                int minDist = 0;
                for(var i = 0; i < neighbours.Count;i++)
                {
                    var n = neighbours[i];
                    int dist = dijkstraGrid[n.x][n.y] - dijkstraGrid[pos.x][pos.y];
                    if(dist < minDist)
                    {
                        min = n;
                        minDist = dist;
                    }
                }
                if (min != null)
                {
                    Vector2 v = new Vector2(min.x - pos.x, min.y - pos.y).normalized;
                    flowField[x][y] = v;
                    SetCubeText(x,y,string.Format("{0:N1},{1:N1}",v.x,v.y));
                }
            }
    }

    void Update()
    {
        foreach(var item in agents)
        {
            item.forceToApply = steeringBehaviourFlowField(item);
            item.velocity = item.velocity + item.forceToApply * Time.deltaTime;
            float speed = item.velocity.magnitude;
            if (speed > item.maxSpeed)
                item.velocity = item.velocity * (item.maxSpeed / speed);
            item.SetPos(item.position += new Vector3(item.velocity.x, 0f, item.velocity.y) * Time.deltaTime);
        }
    }
    bool isValid(int x, int y)
    {
	    return x >= 0 && y >= 0 && x < Width && y < Height && dijkstraGrid[x][y] != int.MaxValue;
    }

    Vector2 steeringBehaviourFlowField(Agent agent)
    {
        int x = (int)agent.position.x;        
        int z = (int)agent.position.z;

        var f00 = isValid(x,z) ? flowField[x][ z] : Vector2.zero;
        var f01 = isValid(x,z+1) ? flowField[x][z + 1]: Vector2.zero;
        var f10 = isValid(x+1,z) ? flowField[x + 1][ z]: Vector2.zero;
        var f11 = isValid(x + 1, z + 1) ? flowField[x + 1][z + 1] : Vector2.zero;

        var xWeight = agent.position.x - x;
        var top = f00 * (1 - xWeight) + f10 * xWeight;
        var bottom = f01 * (1 - xWeight) + f11 * xWeight;

        var yWeight = agent.position.z - z;
        var direction = (top * (1 - yWeight) + bottom * yWeight).normalized;

        var desiredVelocity = direction * agent.maxSpeed;
        var velocityChange = desiredVelocity - agent.velocity;

        return velocityChange * (agent.maxForce / agent.maxSpeed);
    }

    List<V2> straightNeighboursOf(V2 v)
    {
        List<V2> res = new List<V2>();
        if (v.x > 0)
            res.Add(new V2(v.x - 1, v.y));
        if (v.y > 0)
            res.Add(new V2(v.x, v.y - 1));
        if (v.x < Width - 1)
            res.Add(new V2(v.x + 1, v.y));
        if (v.y < Height - 1)
            res.Add(new V2(v.x, v.y + 1));
        return res;
    }

    List<V2> allNeighboursOf(V2 v)
    {
        var res = new List<V2>();

        for (var dx = -1; dx <= 1; dx++)
        {
            for (var dy = -1; dy <= 1; dy++)
            {
                var x = v.x + dx;
                var y = v.y + dy;

                //All neighbours on the grid that aren't ourself
                if (x >= 0 && y >= 0 && x < Width && y < Height && !(dx == 0 && dy == 0))
                {
                    res.Add(new V2(x, y));
                }
            }
        }

        return res;
    }
}
