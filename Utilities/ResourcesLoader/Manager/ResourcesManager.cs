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
    private class BindingHandle
    {
        private List<UnityEngine.Object> owners;
        private AsyncOperationHandle handle;

        public BindingHandle(UnityEngine.Object owner, AsyncOperationHandle handle)
        {
            this.handle = handle;

            AddOwner(owner);
        }

        public void AddOwner(UnityEngine.Object owner)
        {
            if (owners == null)
                owners = new List<UnityEngine.Object>();

            owners.Add(owner);
        }

        public bool Release()
        {
            if (!handle.IsValid())
                return true;

            owners.RemoveAll(match => match.SafeIsNull());

            if (owners.Count > 0)
                return false;

            Addressables.Release(handle);
            return true;
        }

        public T Result<T>() where T : UnityEngine.Object
        {
            return handle.Result as T;
        }
    }

    private static Dictionary<string, BindingHandle> bindingHandles = new();
    private static Dictionary<string, Dictionary<string, AsyncOperationHandle>> preloadHandles = new();
    private static HashSet<string> loadingAssets = new HashSet<string>();

    public static T Instantiate<T>(GameObject loadObject, Transform parent) where T : UnityEngine.Object
    {
        GameObject instantiate = GameObject.Instantiate(loadObject);
        if (instantiate == null)
            return null;

        instantiate.transform.SetParent(parent);
        instantiate.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instantiate.transform.localScale = Vector3.one;

        if (typeof(T) == typeof(GameObject))
            return instantiate as T;

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

#if UNITY_EDITOR
        if (UseAssetDatabase())
            return null;
#endif

        var handle = Addressables.InstantiateAsync(assetPath, position, rotation, parent);
        await handle;

        GameObject loadObject = handle.Result;
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
            var handle = Addressables.InstantiateAsync(assetPath, parent);
            await handle;
            GameObject instantiate = handle.Result;
            if (instantiate == null)
                return null;

            return instantiate.GetComponent<T>();
        }

        if (typeof(T) == typeof(GameObject))
            return GameObject.Instantiate(builtInPrefab, parent) as T;

        GameObject gameObject = GameObject.Instantiate(builtInPrefab, parent);

        return gameObject.GetComponent<T>();
    }

    public static async UniTask<T> LoadAsset<T>(string assetPath, UnityEngine.Object owner) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (UseAssetDatabase())
        {
            var asset = Resources.Load<T>(assetPath);
            if (asset == null)
                asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/ResourcesAddressable/{assetPath}");

            return asset;
        }
#endif

        if (bindingHandles.ContainsKey(assetPath))
        {
            bindingHandles[assetPath].AddOwner(owner);
            return bindingHandles[assetPath].Result<T>();
        }

        var handle = Addressables.LoadAssetAsync<T>(assetPath);

        await handle;

        if (handle.IsValid())
        {
            Debug.LogError($"Success to load asset: {assetPath}");
            bindingHandles[assetPath] = new BindingHandle(owner, handle);
            return bindingHandles[assetPath].Result<T>();
        }
        else
        {
            Debug.LogError($"Failed to load asset: {assetPath}");
            return default;
        }
    }

    public static void LoadAsset<T>(string assetPath, UnityEngine.Object owner, Action<bool, T> onComplete) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (UseAssetDatabase())
        {
            var asset = Resources.Load<T>(assetPath);
            if (asset == null)
                asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/ResourcesAddressable/{assetPath}");

            onComplete(asset != null, asset);
            return;
        }
#endif

        if (bindingHandles.ContainsKey(assetPath))
        {
            bindingHandles[assetPath].AddOwner(owner);
            onComplete(true, bindingHandles[assetPath].Result<T>());
            return;
        }

        if (loadingAssets.Contains(assetPath))
            return;

        loadingAssets.Add(assetPath);

        var handle = Addressables.LoadAssetAsync<T>(assetPath);
        handle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                bindingHandles[assetPath] = new BindingHandle(owner, op);
                onComplete(true, bindingHandles[assetPath].Result<T>());
            }
            else if (op.Status == AsyncOperationStatus.Failed)
            {
                onComplete(false, null);
            }

            loadingAssets.Remove(assetPath);
        };

        handle.WaitForCompletion();
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

    public static async UniTask PreloadAssetsWhenAll(string key, string[] assetPaths)
    {
#if UNITY_EDITOR
        if (UseAssetDatabase())
            return;
#endif

        if (!preloadHandles.ContainsKey(key))
            preloadHandles[key] = new Dictionary<string, AsyncOperationHandle>(assetPaths.Length);

        var tasks = new List<UniTask>(assetPaths.Length);
        for (int i = 0; i < assetPaths.Length; ++i)
        {
            if (preloadHandles[key].ContainsKey(assetPaths[i]))
                continue;

            preloadHandles[key][assetPaths[i]] = Addressables.LoadAssetAsync<UnityEngine.Object>(assetPaths[i]);
            tasks.Add(preloadHandles[key][assetPaths[i]].ToUniTask());
        }

        await UniTask.WhenAll(tasks);
    }

    public static async UniTask PreloadAssets(string key, string[] assetPaths)
    {
#if UNITY_EDITOR
        if (UseAssetDatabase())
            return;
#endif

        preloadHandles[key] = new Dictionary<string, AsyncOperationHandle>(assetPaths.Length);

        var tasks = new UniTask[assetPaths.Length];

        for (int i = 0; i < assetPaths.Length; ++i)
        {
            preloadHandles[key][assetPaths[i]] = Addressables.LoadAssetAsync<UnityEngine.Object>(assetPaths[i]);
            await preloadHandles[key][assetPaths[i]];
        }
    }

    public static void ReleaseAsset(string assetPath)
    {
        if (bindingHandles.ContainsKey(assetPath))
        {
            if (bindingHandles[assetPath].Release())
                bindingHandles.Remove(assetPath);
        }
    }

    public static void ReleasePreloadAsset(string key)
    {
        if (!preloadHandles.ContainsKey(key))
            return;

        foreach (var preloadHandle in preloadHandles[key].Values)
        {
            Addressables.Release(preloadHandle);
        }

        preloadHandles.Remove(key);
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

    public static bool TryGetAsset<T>(string assetPath, UnityEngine.Object owner, out T asset) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (UseAssetDatabase())
        {
            asset = Resources.Load<T>(assetPath);
            if (asset == null)
                asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/ResourcesAddressable/{assetPath}");

            return true;
        }
#endif

        asset = null;

        if (!bindingHandles.ContainsKey(assetPath))
            return false;

        asset = bindingHandles[assetPath].Result<T>();
        return true;
    }

    public static bool TryGetPreloadAsset<T>(string assetPath, out T asset) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (UseAssetDatabase())
        {
            asset = Resources.Load<T>(assetPath);
            if (asset == null)
                asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/ResourcesAddressable/{assetPath}");

            return true;
        }
#endif

        asset = null;

        foreach (var preloadHandle in preloadHandles.Values)
        {
            if (!preloadHandle.ContainsKey(assetPath))
                continue;

            asset = preloadHandle[assetPath].Result as T;
        }

        return asset != null;
    }

#if UNITY_EDITOR
    private static bool UseAssetDatabase()
    {
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return true;

        if (settings.ActivePlayModeDataBuilderIndex == 0)
            return true;

        return false;
    }
#endif
}
#endif