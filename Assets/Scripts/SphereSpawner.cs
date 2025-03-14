using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AdvancedSphereSpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Transform _poolParent;

    [Header("Spawning Settings")]
    [SerializeField] private GameObject _spherePrefab;
    [SerializeField] private float _yOffset = 2f;
    [SerializeField] private LayerMask _spawnLayerMask;
    [SerializeField] private float _spawnsPerSecond = 10f;
    [SerializeField] private float _autoReturnTime = 5f;

    [Header("Pooling Settings")]
    [SerializeField] private int _initialPoolSize = 1000;
    [SerializeField] private int _poolGrowthAmount = 100;

    private Ray _ray;
    private RaycastHit _hit;
    private float _spawnCooldown;

    private Queue<GameObject> _inactiveSpheres = new Queue<GameObject>();
    private HashSet<GameObject> _activeSpheres = new HashSet<GameObject>();

    public float AutoReturnTime => _autoReturnTime; // Public accessor for return time

    private void Awake()
    {
        // Initialize references
        if (_mainCamera == null)
            _mainCamera = Camera.main;

        if (_poolParent == null)
        {
            _poolParent = new GameObject("SpherePool").transform;
            _poolParent.SetParent(transform);
        }

        InitializePool();
    }

    private void InitializePool()
    {
        for (int i = 0; i < _initialPoolSize; i++)
            CreateNewPooledSphere();
    }

    private GameObject CreateNewPooledSphere()
    {
        GameObject sphere = Instantiate(_spherePrefab, _poolParent);
        sphere.SetActive(false);
        var pooledSphere = sphere.AddComponent<PooledSphere>();
        pooledSphere.Initialize(this);
        _inactiveSpheres.Enqueue(sphere);
        return sphere;
    }

    private void Update()
    {
        HandleSpawningInput();
    }

    private void HandleSpawningInput()
    {
        if (Input.GetMouseButton(0))
        {
            _spawnCooldown -= Time.deltaTime;

            if (_spawnCooldown <= 0f)
            {
                TrySpawnSphere();
                _spawnCooldown = 1f / _spawnsPerSecond;
            }
        }
    }

    private void TrySpawnSphere()
    {
        _ray = _mainCamera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(_ray, out _hit, Mathf.Infinity, _spawnLayerMask))
            SpawnSphere(_hit.point + Vector3.up * _yOffset);
    }

    private void SpawnSphere(Vector3 position)
    {
        if (_inactiveSpheres.Count == 0)
            ExpandPool();

        GameObject sphere = _inactiveSpheres.Dequeue();
        _activeSpheres.Add(sphere);

        sphere.transform.position = position;
        sphere.SetActive(true);

        Rigidbody rb = sphere.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = false;
        }

        sphere.GetComponent<PooledSphere>().OnSpawned();
    }

    private void ExpandPool()
    {
        for (int i = 0; i < _poolGrowthAmount; i++)
            CreateNewPooledSphere();

        Debug.LogWarning($"Pool expanded. New size: {_inactiveSpheres.Count + _activeSpheres.Count}");
    }

    public void ReturnSphereToPool(GameObject sphere)
    {
        if (!_activeSpheres.Contains(sphere)) return;

        sphere.SetActive(false);
        _activeSpheres.Remove(sphere);
        _inactiveSpheres.Enqueue(sphere);

        Rigidbody rb = sphere.GetComponent<Rigidbody>();
        if (rb != null)
            rb.isKinematic = true;
    }
}

public class PooledSphere : MonoBehaviour
{
    private AdvancedSphereSpawner _spawner;
    private Coroutine _returnCoroutine;

    public void Initialize(AdvancedSphereSpawner spawner)
    {
        _spawner = spawner;
    }

    public void OnSpawned()
    {
        if (_returnCoroutine != null)
            StopCoroutine(_returnCoroutine);

        _returnCoroutine = StartCoroutine(AutoReturnCoroutine());
    }

    private IEnumerator AutoReturnCoroutine()
    {
        yield return new WaitForSeconds(_spawner.AutoReturnTime);
        _spawner.ReturnSphereToPool(gameObject);
    }

    private void OnDisable()
    {
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }
    }

    private void OnBecameInvisible()
    {
        _spawner.ReturnSphereToPool(gameObject);
    }
}