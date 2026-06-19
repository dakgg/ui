using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ServiceLocatorScene : ServiceLocator
{
    static Dictionary<Scene, ServiceLocator> s_Services = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InitializeGameInstanceBeforeLoad()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        var go = new GameObject("SceneProvider");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<ServiceLocatorScene>();
    }

    protected override void Awake()
    {
        base.Awake();
        s_Services.Add(gameObject.scene, this);
    }

    void OnDestroy()
    {
        s_Services.Remove(gameObject.scene);
    }
    
    protected override IEnumerable<IServiceLocatorComponentBase> CollectService()
    {
        var list = new List<IServiceLocatorComponentBase>();
        var roots = gameObject.scene.GetRootGameObjects();
        foreach (var root in roots)
        {
            list.AddRange(root.GetComponents<IServiceLocatorComponentBase>());
        }

        return list;
    }

    public static bool TryResolveFromScene<T>(Scene scene, out T found)
    {
        if (!scene.IsValid())
        {
            found = default;
            return false;
        }

        if (scene.name == "DontDestroyOnLoad")
        {
            found = default;
            return false;
        }

        if (!s_Services.TryGetValue(scene, out var foundResolver))
        {
            found = default;
            return false;
        }

        return foundResolver.TryResolve<T>(out found);
    }

    public static bool TryResolveFromScene(Type type, Scene scene, out object found)
    {
        if (!scene.IsValid())
        {
            found = default;
            return false;
        }

        if (scene.name == "DontDestroyOnLoad")
        {
            found = default;
            return false;
        }

        if (!s_Services.TryGetValue(scene, out var foundResolver))
        {
            found = default;
            return false;
        }

        return foundResolver.TryResolve(type, out found);
    }

    public static T ResolveFromScene<T>(Scene scene)
    {
        if (!TryResolveFromScene<T>(scene, out var found)) throw new Exception("resolve failed from scene");
        return found;
    }
}