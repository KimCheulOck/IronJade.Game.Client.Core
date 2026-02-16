#if ADDRESSABLE
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class ResourcesManager
{
    private static Dictionary<string, AsyncOperationHandle> handles = new Dictionary<string, AsyncOperationHandle>();
    private static HashSet<string> loadingAssets = new HashSet<string>();

    public static void ReleaseAsset(string assetPath)
    {
        if (handles.TryGetValue(assetPath, out var handle) && handle.IsValid())
        {
            Addressables.Release(handle);
            handles.Remove(assetPath);
        }
    }

    public static T Instantiate<T>(GameObject loadObject, Transform parent) where T : UnityEngine.Object
    {
        GameObject instantiate = GameObject.Instantiate(loadObject);
        if (instantiate == null)
            return null;

        instantiate.transform.SetParent(parent);
        instantiate.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instantiate.transform.localScale = Vector3.one;

        return instantiate.GetComponent<T>();
    }

    public static async UniTask<T> InstantiateAsync<T>(string assetPath, Transform parent, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Vector3 scale = new Vector3()) where T : UnityEngine.Object
    {
        GameObject builtInPrefab = Resources.Load<GameObject>(assetPath);

        if (builtInPrefab != null)
        {
            if (typeof(T) == typeof(GameObject))
                return GameObject.Instantiate(builtInPrefab, position, rotation, parent) as T;

            GameObject gameObject = GameObject.Instantiate(builtInPrefab, position, rotation, parent);
            gameObject.transform.localScale = scale;

            return gameObject.GetComponent<T>();
        }

        GameObject loadObject = await LoadAsset<GameObject>(AssetType.Prefab, assetPath);
        if (loadObject == null)
            return null;

        GameObject instantiate = GameObject.Instantiate(loadObject);
        if (instantiate == null)
            return null;

        instantiate.transform.SetParent(parent);
        instantiate.transform.SetPositionAndRotation(position, rotation);
        instantiate.transform.localScale = scale;

        return instantiate.GetComponent<T>();
    }

    public static async UniTask<T> InstantiateAsync<T>(string assetPath, Transform parent) where T : UnityEngine.Object
    {
        GameObject builtInPrefab = Resources.Load<GameObject>(assetPath);

        if (builtInPrefab == null)
        {
            GameObject loadObject = await LoadAsset<GameObject>(AssetType.Prefab, assetPath);
            if (loadObject == null)
                return null;

            GameObject instantiate = GameObject.Instantiate(loadObject, parent);
            if (instantiate == null)
                return null;

            return instantiate.GetComponent<T>();
        }

        if (typeof(T) == typeof(GameObject))
            return GameObject.Instantiate(builtInPrefab, parent) as T;

        GameObject gameObject = GameObject.Instantiate(builtInPrefab, parent);

        return gameObject.GetComponent<T>();
    }

    public static bool TryGetGameObject(string assetPath, out GameObject prefab)
    {
        prefab = null;

        if (!handles.ContainsKey(assetPath))
        {
            prefab = Resources.Load<GameObject>(assetPath);

            if (prefab == null)
                return false;

            return true;
        }

        if (handles[assetPath].Result == null)
            return false;

        if (handles[assetPath].Result.Equals(null))
            return false;

        prefab = handles[assetPath].Result as GameObject;
        if (prefab == null)
            return false;

        return true;
    }

    public static bool TryGetComponent<T>(string assetPath, out T component) where T : UnityEngine.Object
    {
        component = default;

        if (!handles.ContainsKey(assetPath))
        {
            component = Resources.Load<T>(assetPath);

            if (component == null)
                return false;

            return true;
        }

        if (handles[assetPath].Result == null)
            return false;

        if (handles[assetPath].Result.Equals(null))
            return false;

        component = handles[assetPath].Result as T;
        if (component == null)
        {
            var gameObject = component as GameObject;
            if (gameObject == null)
                return false;

            component = gameObject.GetComponent<T>();
        }

        if (component == null)
            return false;

        return true;
    }

    public static void LoadAsset(AssetType assetType, string assetPath)
    {
        if (loadingAssets.Contains(assetPath))
            return;

        loadingAssets.Add(assetPath);

        LoadAsset(assetType, assetPath, onComplete: (success) =>
        {
            if (success)
            {
                Debug.LogError($"Success to load asset: {assetPath}");
            }
            else
            {
                Debug.LogError($"Failed to load asset: {assetPath}");
            }

            loadingAssets.Remove(assetPath);
        });
    }

    public static void LoadAsset(AssetType assetType, string assetPath, Action<bool> onComplete)
    {
        if (handles.ContainsKey(assetPath))
        {
            onComplete(true);
            return;
        }

        AsyncOperationHandle handle;
        switch (assetType)
        {
            case AssetType.Prefab:
                handle = Addressables.LoadAssetAsync<GameObject>(assetPath);
                break;
            case AssetType.Sprite:
                handle = Addressables.LoadAssetAsync<Sprite>(assetPath);
                break;
            default:
                Debug.LogError("Unsupported asset type.");
                return;
        }

        handle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                handles[assetPath] = op;
                bool success = op.Status == AsyncOperationStatus.Succeeded;
                onComplete(success);
            }
            else if (op.Status == AsyncOperationStatus.Failed)
            {
                loadingAssets.Remove(assetPath);
                onComplete(false);
            }
        };

        handle.WaitForCompletion();
    }

    public static async UniTask<T> LoadAsset<T>(AssetType assetType, string assetPath) where T : UnityEngine.Object
    {
        if (handles.ContainsKey(assetPath))
            return handles[assetPath].Result as T;

        AsyncOperationHandle handle;
        switch (assetType)
        {
            case AssetType.Prefab:
                handle = Addressables.LoadAssetAsync<GameObject>(assetPath);
                break;
            case AssetType.Sprite:
                handle = Addressables.LoadAssetAsync<Sprite>(assetPath);
                break;
            default:
                Debug.LogError("Unsupported asset type.");
                return default;
        }

        await handle;

        if (handle.IsValid())
        {
            Debug.LogError($"Success to load asset: {assetPath}");

            handles[assetPath] = handle;
            return handles[assetPath].Result as T;
        }
        else
        {
            Debug.LogError($"Failed to load asset: {assetPath}");
            return default;
        }
    }

    public static async UniTask<Scene> LoadScene(string scenePath, LoadSceneMode loadSceneMode)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, loadSceneMode);

        if (load == null)
        {
            var handle = Addressables.LoadSceneAsync(scenePath, loadSceneMode);

            await handle.ToUniTask();

            return handle.Result.Scene;
        }

        await load.ToUniTask();

        return SceneManager.GetSceneByName(scenePath);
    }

    public static async UniTask UnloadScene(string scenePath)
    {
        AsyncOperation load = SceneManager.UnloadSceneAsync(scenePath);

        if (load == null)
            return;

        await load.ToUniTask();
    }

    public static void ActiveScene(Scene scene)
    {
        SceneManager.SetActiveScene(scene);
    }
}
#endif