using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

#if ADDRESSABLE
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

public static class ResourcesManager
{
    private static GameObject PublicOwner
    {
        get
        {
            if (publicOwner == null)
                publicOwner = new GameObject("PublicOwner");

            return publicOwner;
        }
    }
    private static GameObject publicOwner;

#if ADDRESSABLE
    private class BindingHandle
    {
        private HashSet<UnityEngine.Object> owners;
        private AsyncOperationHandle handle;

        public BindingHandle(UnityEngine.Object owner, AsyncOperationHandle handle)
        {
            this.handle = handle;

            AddOwner(owner);
        }

        public void AddOwner(UnityEngine.Object owner)
        {
            if (owners == null)
                owners = new HashSet<UnityEngine.Object>();

            owners.Add(owner);
        }

        public bool Release()
        {
            if (!handle.IsValid())
                return true;

            owners.RemoveWhere(match => match.SafeIsNull());

            if (owners.Count > 0)
                return false;

            Addressables.Release(handle);
            return true;
        }

        public bool CheckOwner(UnityEngine.Object owner)
        {
            return owners.Contains(owner);
        }

        public T Result<T>() where T : UnityEngine.Object
        {
            return handle.Result as T;
        }
    }

    private static Dictionary<string, BindingHandle> bindingHandles = new();
    private static Dictionary<string, Dictionary<string, AsyncOperationHandle>> preloadHandles = new();
    private static HashSet<string> loadingAssets = new HashSet<string>();

    public static bool TryGetAsset<T>(string assetPath, UnityEngine.Object owner, out T asset) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorLoad<T>(assetPath, out asset))
            return true;
#endif
        asset = null;

        if (!bindingHandles.ContainsKey(assetPath))
            return false;

        if (!bindingHandles[assetPath].CheckOwner(owner))
            return false;

        asset = bindingHandles[assetPath].Result<T>();
        return true;
    }

    public static bool TryGetAsset<T>(string assetPath, out T asset) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorLoad<T>(assetPath, out asset))
            return true;
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
        if (TryEditorLoad<T>(assetPath, out asset))
            return true;
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

    public static async UniTask PreloadAssetsWhenAll(string key, string[] assetPaths)
    {
        if (UseAssetDatabase())
            return;

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
        if (UseAssetDatabase())
            return;

        preloadHandles[key] = new Dictionary<string, AsyncOperationHandle>(assetPaths.Length);

        var tasks = new UniTask[assetPaths.Length];

        for (int i = 0; i < assetPaths.Length; ++i)
        {
            preloadHandles[key][assetPaths[i]] = Addressables.LoadAssetAsync<UnityEngine.Object>(assetPaths[i]);
            await preloadHandles[key][assetPaths[i]];
        }
    }

    public static async UniTask<T> InstantiateAsync<T>(string assetPath, Transform parent, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Vector3 scale = new Vector3()) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorInstantiate<T>(assetPath, parent, position, rotation, scale, out T obj))
            return obj;
#endif

        var handle = Addressables.InstantiateAsync(assetPath, position, rotation, parent);
        await handle;

        GameObject instantiate = handle.Result;
        if (instantiate == null)
        {
            instantiate = Resources.Load<GameObject>(assetPath);
            if (instantiate == null)
                return null;
        }
        else
        {
            bindingHandles[assetPath] = new BindingHandle(instantiate, handle);
        }

        instantiate.transform.SetParent(parent);
        instantiate.transform.SetPositionAndRotation(position, rotation);
        instantiate.transform.localScale = scale;

        return instantiate.GetComponent<T>();
    }

    public static async UniTask<T> LoadAsset<T>(string assetPath, UnityEngine.Object owner) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorLoad(assetPath, out T obj))
            return obj;
#endif
        if (owner == null)
            owner = PublicOwner;

        if (bindingHandles.ContainsKey(assetPath))
        {
            bindingHandles[assetPath].AddOwner(owner);
            return bindingHandles[assetPath].Result<T>();
        }

        var asset = Resources.Load<T>(assetPath);
        if (asset != null)
            return asset;

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
            return null;
        }
    }

    public static void LoadAsset<T>(string assetPath, UnityEngine.Object owner, Action<bool, T> onComplete) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorLoad(assetPath, out T obj))
            onComplete(obj != null, obj);
#endif

        if (owner == null)
            owner = PublicOwner;

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
                var asset = Resources.Load<T>(assetPath);
                onComplete(asset != null, asset);
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

    private static bool UseAssetDatabase()
    {
#if UNITY_EDITOR
        var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            return true;

        if (settings.ActivePlayModeDataBuilderIndex == 0)
            return true;
#endif
        return false;
    }
#else
    public static async UniTask<T> InstantiateAsync<T>(string assetPath, Transform parent, Vector3 position = new Vector3(), Quaternion rotation = new Quaternion(), Vector3 scale = new Vector3()) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorInstantiate<T>(assetPath, parent, position, rotation, scale, out T obj))
            return obj;
#endif

        T builtInPrefab = Resources.Load<T>(assetPath);
        if (builtInPrefab == null)
            return null;

        return GameObject.Instantiate(builtInPrefab, parent);
    }

    public static async UniTask<T> LoadAsset<T>(string assetPath, UnityEngine.Object owner) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorLoad(assetPath, out T obj))
            return obj;
#endif
        return Resources.Load<T>(assetPath);
    }

    public static void LoadAsset<T>(string assetPath, UnityEngine.Object owner, Action<bool, T> onComplete) where T : UnityEngine.Object
    {
#if UNITY_EDITOR
        if (TryEditorLoad(assetPath, out T obj))
            onComplete(obj != null, obj);
#endif
        var asset = Resources.Load<T>(assetPath);
        onComplete(asset != null, asset);
    }

    public static async UniTask<Scene> LoadScene(string scenePath, LoadSceneMode loadSceneMode)
    {
        AsyncOperation load = SceneManager.LoadSceneAsync(scenePath, loadSceneMode);

        await load.ToUniTask();

        return SceneManager.GetSceneByName(scenePath);
    }

    private static bool UseAssetDatabase()
    {
        return false;
    }
#endif
    public static T Instantiate<T>(GameObject loadObject, Transform parent) where T : UnityEngine.Object
    {
        GameObject instantiate = GameObject.Instantiate(loadObject);
        if (instantiate == null)
            return null;

        if (parent != null)
        {
            instantiate.transform.SetParent(parent);
            instantiate.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instantiate.transform.localScale = Vector3.one;
            instantiate.ChangeLayer(LayerMask.LayerToName(parent.gameObject.layer), true);
        }

        if (typeof(T) == typeof(GameObject))
            return instantiate as T;

        return instantiate.GetComponent<T>();
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

#if UNITY_EDITOR
    private static bool TryEditorInstantiate<T>(string assetPath, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, out T obj) where T : UnityEngine.Object
    {
        obj = null;

        if (UseAssetDatabase())
        {
            var asset = EditorLoadAsset<GameObject>(assetPath);
            if (asset == null)
                return false;

            var instantiate = GameObject.Instantiate(asset, parent);
            if (instantiate == null)
                return false;

            instantiate.transform.SetParent(parent);
            instantiate.transform.SetPositionAndRotation(position, rotation);
            instantiate.transform.localScale = scale;

            obj = instantiate.GetComponent<T>();
            return obj != null;
        }

        return false;
    }

    private static bool TryEditorLoad<T>(string assetPath, out T obj) where T : UnityEngine.Object
    {
        obj = null;

        if (UseAssetDatabase())
        {
            var asset = EditorLoadAsset<T>(assetPath);
            obj = asset;
            return obj != null;
        }

        return false;
    }

    private static T EditorLoadAsset<T>(string assetPath) where T : UnityEngine.Object
    {
        var asset = Resources.Load<T>(assetPath);
        if (asset == null)
            asset = UnityEditor.AssetDatabase.LoadAssetAtPath<T>($"Assets/ResourcesAddressable/{assetPath}");

        return asset;
    }
#endif
}