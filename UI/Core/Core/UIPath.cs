using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IronJade.UI.Core
{
    /// <summary>
    /// UI의 경로를 Name을 기준으로 가집니다.
    /// </summary>
    [CreateAssetMenu(fileName = "UIPath", menuName = "Scriptable Objects/UIPath")]
    public class UIPath : ScriptableObject
    {
        private static UIPath Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<UIPath>(path);
                    instance.SetUIPath();
                }

                return instance;
            }
        }
        private static UIPath instance;

        private const string path = "UI/ScriptableObjects/UIPath";

        [System.Serializable]
        public struct NameByPath
        {
            public string Name;
            public string Path;
        }

        [SerializeField]
        private NameByPath[] nameByPath = null;

        private Dictionary<string, string> keyValuePairs = new Dictionary<string, string>();

        private void SetUIPath()
        {
            if (!Application.isPlaying)
                return;

            keyValuePairs = new Dictionary<string, string>(nameByPath.Length);

            for (int i = 0; i < nameByPath.Length; i++)
                keyValuePairs.Add(nameByPath[i].Name, nameByPath[i].Path);
        }

        public static string GetPath(string key)
        {
            if (Instance == null)
                return string.Empty;

            if (Instance.keyValuePairs.ContainsKey(key))
                return Instance.keyValuePairs[key];

            return string.Empty;
        }

#if UNITY_EDITOR
        [ContextMenu("프로젝트 내에 BaseView를 가지는 모든 프리팹의 경로를 지정")]
        public void SetAllNameByPath_Editor()
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab");
            List<GameObject> prefabsWithComponent = new List<GameObject>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

                if (prefab != null && prefab.GetComponentInChildren<BaseView>(true) != null)
                {
                    prefabsWithComponent.Add(prefab);
                    Debug.Log($"Found prefab with {typeof(BaseView).Name}: {path}", prefab);
                }
            }

            nameByPath = new NameByPath[prefabsWithComponent.Count];
            string relativePath = "Assets/Resources";
            string relativeAddressablePath = "Assets/ResourcesAddressable";

            for (int i = 0; i < nameByPath.Length; ++i)
            {
                nameByPath[i] = new NameByPath
                {
                    Name = prefabsWithComponent[i].name
                };
                nameByPath[i].Name = nameByPath[i].Name.Replace("View", "Controller");
                nameByPath[i].Name = nameByPath[i].Name.Replace("Popup", "Controller");

                string assetPath = AssetDatabase.GetAssetPath(prefabsWithComponent[i]);
                if (assetPath.Contains(relativeAddressablePath))
                {
                    nameByPath[i].Path = Path.GetRelativePath(relativeAddressablePath, assetPath)
                        //.Replace(".prefab", "")
                        .Replace('\\', '/');
                }
                else
                {
                    nameByPath[i].Path = Path.GetRelativePath(relativePath, assetPath)
                        .Replace(".prefab", "")
                        .Replace('\\', '/');
                }
            }
        }
#endif
    }
}