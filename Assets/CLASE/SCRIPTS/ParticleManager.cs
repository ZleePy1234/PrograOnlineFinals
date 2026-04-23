using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class ParticleManager : MonoBehaviour
{
    public static ParticleManager Instance;

    public enum PoolType
    {
        Particle,
        Decal
    }

    [System.Serializable]
    public class PoolData
    {
        public string id;
        public PoolType type;
        public GameObject prefab;
        public int size;
    }

    [SerializeField] private List<PoolData> pools;

    [Header("Decals")]
    [SerializeField] private float decalLifeTime = 10f;
    [SerializeField] private int maxDecals = 30;
    [SerializeField] private float decalSurfaceOffset = 0.01f;

    private Dictionary<string, Queue<GameObject>> poolDictionary = new();
    private Dictionary<string, PoolData> poolInfo = new();

    private Queue<GameObject> activeDecals = new();

    private void Awake()
    {
        Instance = this;

        foreach (var pool in pools)
        {
            Queue<GameObject> newPool = new();

            for (int i = 0; i < pool.size; i++)
            {
                var obj = Instantiate(pool.prefab, transform);
                obj.SetActive(false);
                newPool.Enqueue(obj);
            }

            poolDictionary.Add(pool.id, newPool);
            poolInfo.Add(pool.id, pool);
        }
    }

    public void SpawnParticle(string id, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.ContainsKey(id)) return;

        var pool = poolDictionary[id];
        var info = poolInfo[id];

        if (info.type != PoolType.Particle) return;

        GameObject obj = pool.Count > 0 ? pool.Dequeue() : Instantiate(info.prefab, transform);

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        var particle = obj.GetComponent<ParticleSystem>();
        particle.Play();

        ReturnParticle(id, particle);
    }

    public void SpawnDecal(string id, Vector3 position, Quaternion rotation, Vector3 normal)
    {
        if (!poolDictionary.ContainsKey(id)) return;

        var pool = poolDictionary[id];
        var info = poolInfo[id];

        if (info.type != PoolType.Decal) return;

        GameObject obj;

        if (activeDecals.Count >= maxDecals)
            obj = activeDecals.Dequeue();
        else
            obj = pool.Count > 0 ? pool.Dequeue() : Instantiate(info.prefab, transform);

        position += normal * decalSurfaceOffset;

        obj.transform.position = position;
        obj.transform.rotation = rotation;
        obj.SetActive(true);

        activeDecals.Enqueue(obj);

        ReturnDecal(id, obj);
    }

    private async void ReturnParticle(string id, ParticleSystem particle)
    {
        await System.Threading.Tasks.Task.Delay((int)(particle.main.duration * 1000));

        if (particle == null) return;

        particle.gameObject.SetActive(false);
        poolDictionary[id].Enqueue(particle.gameObject);
    }

    private async void ReturnDecal(string id, GameObject decal)
    {
        await System.Threading.Tasks.Task.Delay((int)(decalLifeTime * 1000));

        if (decal == null) return;

        decal.SetActive(false);
        poolDictionary[id].Enqueue(decal);
    }
}