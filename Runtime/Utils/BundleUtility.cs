using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class BundleUtility
{
    /// <summary>
    /// Asynchronously loads a scene with a given key.
    /// </summary>
    public static AsyncOperationHandle<SceneInstance> LoadSceneAsync(object key, LoadSceneMode mode = LoadSceneMode.Single, bool activateOnLoad = true, int priority = 100)
    {
        return Addressables.LoadSceneAsync(key, mode, activateOnLoad, priority);
    }
    
    /// <summary>
    /// Asynchronously loads an asset of a specified type with a given key.
    /// </summary>
    public static AsyncOperationHandle<T> LoadAsync<T>(object key)
    {
        return Addressables.LoadAssetAsync<T>(key);
    }
    
    /// <summary>
    /// Asynchronously loads locations of a specified type with a given key.
    /// </summary>
    public static AsyncOperationHandle<IList<IResourceLocation>> LoadResourceLocationAsync(object key, Type type = null)
    {
        return Addressables.LoadResourceLocationsAsync(key, type);
    }
    
    /// <summary>
    /// Asynchronously loads an asset of a specified type with a given key, using a reference to an AsyncOperationHandle.
    /// </summary>
    public static ref AsyncOperationHandle<T> LoadAsync<T>(this ref AsyncOperationHandle<T> handle, object key)
    {
        handle.ReleaseIfValid();
        handle = Addressables.LoadAssetAsync<T>(key);
        return ref handle;
    }
    
    /// <summary>
    /// Releases an asset if the handle is valid.
    /// </summary>
    public static void ReleaseIfValid(this ref AsyncOperationHandle handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            handle = default;
        }
    }

    /// <summary>
    /// Releases an asset if the handle is valid.
    /// </summary>
    public static void ReleaseIfValid<T>(this ref AsyncOperationHandle<T> handle)
    {
        if (handle.IsValid())
        {
            Addressables.Release(handle);
            handle = default;
        }
    }

    /// <summary>
    /// Asynchronously instantiates a GameObject at a specified position and rotation.
    /// </summary>
    public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        var initParams = new InstantiationParameters(position, rotation, parent);
        var operation = Addressables.InstantiateAsync(key, initParams, false);
        return Addressables.ResourceManager.CreateChainOperation(operation, HandleTrackInstance);
    }
    
    /// <summary>
    /// Asynchronously instantiates a GameObject with a specified parent transform.
    /// </summary>
    public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, Transform parent = null, bool instantiateInWorldSpace = false)
    {
        var initParams = new InstantiationParameters(parent, instantiateInWorldSpace);
        var operation = Addressables.InstantiateAsync(key, initParams, false);
        return Addressables.ResourceManager.CreateChainOperation(operation, HandleTrackInstance);
    }
    /// <summary>
    /// Asynchronously instantiates a GameObject with specified instantiation parameters.
    /// </summary>
    public static AsyncOperationHandle<GameObject> InstantiateAsync(object key, InstantiationParameters parameters)
    {
        var operation = Addressables.InstantiateAsync(key, parameters, false);
        return Addressables.ResourceManager.CreateChainOperation(operation, HandleTrackInstance);
    }
    
    /// <summary>
    /// Asynchronously instantiates a GameObject at a resource location with specified instantiation parameters.
    /// </summary>
    public static AsyncOperationHandle<GameObject> InstantiateAsync(IResourceLocation location, InstantiationParameters parameters)
    {
        var operation = Addressables.InstantiateAsync(location, parameters, false);
        return Addressables.ResourceManager.CreateChainOperation(operation, static handle => HandleTrackInstance(handle));
    }
    
    private static AsyncOperationHandle<GameObject> HandleTrackInstance(AsyncOperationHandle<GameObject> handle)
    {
        if (!handle.Result.scene.IsValid()) return handle;
        handle.Result.AddComponent<BundleReleaseTracker>().SetOperationHandle(handle);
        return handle;
    }
    
    public static void Wait(this AsyncOperationHandle handle) => handle.WaitForCompletion();

    public static TResult Wait<TResult>(this AsyncOperationHandle<TResult> handle) => handle.WaitForCompletion();

    /// <summary>
    /// if you set trackHandle to false, you can only call Addressables.ReleaseInstance with the handle, not with the actual GameObject.
    /// </summary>
    [DisallowMultipleComponent]
    private class BundleReleaseTracker : MonoBehaviour
    {
        private AsyncOperationHandle<GameObject> m_ToRelease;

        
        public void SetOperationHandle(AsyncOperationHandle<GameObject> handle)
        {
            m_ToRelease = handle;
        }
        
        private void Awake()
        {
            hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
        }
        
        private void OnDestroy()
        {
            Addressables.ReleaseInstance(m_ToRelease);
        }
    }
}

public abstract class BundledAssetSet<T>
{
    private AsyncOperationHandle<T> m_Handle;

    protected abstract void OnLoaded(T loaded);
    protected abstract void OnRelease();

    public void Load(object key)
    {
        Release();
        m_Handle = Addressables.LoadAssetAsync<T>(key);
        m_Handle.WaitForCompletion();
        OnLoaded(m_Handle.Result);
    }

    public AsyncOperationHandle<T> LoadAsync(object key)
    {
        Release();
        m_Handle = Addressables.ResourceManager.CreateChainOperation(Addressables.LoadAssetAsync<T>(key),
            handle =>
            {
                OnLoaded(handle.Result);
                return handle;
            });
        return m_Handle;
    }
    
    public void Release()
    {
        OnRelease();
        m_Handle.ReleaseIfValid();
    }
}