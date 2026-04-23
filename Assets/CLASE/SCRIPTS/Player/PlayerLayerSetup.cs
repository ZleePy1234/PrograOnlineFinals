using Fusion;
using UnityEngine;

public class PlayerLayerSetup : NetworkBehaviour
{
    [Header("Layers")]
    [SerializeField] private string selfLayerName = "Self";
    [SerializeField] private string defaultLayerName = "Default";
    [SerializeField] private string damagableLayerName = "Damagable";

    [Header("Visual root (solo malla/cuerpo)")]
    [SerializeField] private Transform visualRoot;

    private int _selfLayer;
    private int _defaultLayer;
    private int _damagableLayer;

    private void Awake()
    {
        _selfLayer = LayerMask.NameToLayer(selfLayerName);
        _defaultLayer = LayerMask.NameToLayer(defaultLayerName);
        _damagableLayer = LayerMask.NameToLayer(damagableLayerName);

        if (_selfLayer < 0) _selfLayer = 3;
        if (_defaultLayer < 0) _defaultLayer = 0;
        if (_damagableLayer < 0) _damagableLayer = 7;

        // Si no se asignó visualRoot, asumimos que es el primer hijo
        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
    }

    public override void Spawned()
    {
        // El contenedor (raíz) se queda en Damagable para que lo detecten balas/raycasts
        gameObject.layer = _damagableLayer;

        // Solo cambiamos la capa de la parte visual
        if (visualRoot != null)
        {
            int visualLayer = HasInputAuthority ? _selfLayer : _defaultLayer;
            SetLayerRecursive(visualRoot, visualLayer);
        }
    }

    private void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }
}
