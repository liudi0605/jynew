/*
 * 金庸群侠传3D重制版
 * https://github.com/jynew/jynew
 *
 * 这是本开源项目文件头，所有代码均使用MIT协议。
 * 但游戏内资源和第三方插件、dll等请仔细阅读LICENSE相关授权协议文档。
 *
 * 金庸老先生千古！
 */


using Jyx2;
using Lean.Pool;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Jyx2.MOD;
using Jyx2Configs;
using ProtoBuf;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Jyx2
{
    public static class ImageLoadHelper
    {
        public static void LoadAsyncForget(this Image image, UniTask<Sprite> task)
        {   
            LoadAsync(image,task).Forget();
        }
        
        public static async UniTask LoadAsync(this Image image, UniTask<Sprite> task)
        {
            image.gameObject.SetActive(false);
            image.sprite = await task;
            image.gameObject.SetActive(true);
        }
        
        public static void LoadAsyncForget(this Image image, AssetReference reference)
        {
            LoadAsync(image, reference).Forget();
        }
    
        public static async UniTask LoadAsync(this Image image, AssetReference reference)
        {
            image.gameObject.SetActive(false);
            image.sprite = await LoadSprite(reference);
            image.gameObject.SetActive(true);
        }
        
        public static async UniTask<Sprite> LoadSprite(AssetReference refernce)
        {
            //注：不Release的话，Addressable会进行缓存
            //https://forum.unity.com/threads/1-15-1-assetreference-not-allow-loadassetasync-twice.959910/

            /*            return await Addressables.LoadAssetAsync<Sprite>(refernce);*/
            return await MODLoader.LoadAsset<Sprite>(Jyx2ResourceHelper.GetAssetRefAddress(refernce));
        }
    }
}

public static class Jyx2ResourceHelper
{
    private static bool _isInited = false;
    
    public static async Task Init()
    {
        //已经初始化过了
        if (_isInited)
        {
            return;
        }

        _isInited = true;

        await MODLoader.Init();
        
        //全局配置表
        var t = await Addressables.LoadAssetAsync<GlobalAssetConfig>("Assets/BuildSource/Configs/GlobalAssetConfig.asset");
        if (t != null)
        {
            GlobalAssetConfig.Instance = t;
            t.OnLoad();
        }

        //技能池
        var task = await Addressables.LoadAssetsAsync<Jyx2SkillDisplayAsset>("skills", null);
        if (task != null)
        {
            Jyx2SkillDisplayAsset.All = task;
        }

        //基础配置表
        await GameConfigDatabase.Instance.Init();

        //lua
        await LuaManager.InitLuaMapper();
        LuaManager.Init();
    }

    public static GameObject GetCachedPrefab(string path)
    {
        if(GlobalAssetConfig.Instance.CachePrefabDict.TryGetValue(path, out var prefab))
        {
            return prefab;
        }
        
        Debug.LogError($"载入缓存的Prefab失败：{path}(是否没填入GlobalAssetConfig.CachedPrefabs?)");
        return null;
    }

    public static GameObject CreatePrefabInstance(string path)
    {
        var obj = GetCachedPrefab(path);
        return Object.Instantiate(obj);
    }

    public static void ReleasePrefabInstance(GameObject obj)
    {
        Object.Destroy(obj);
    }

    [Obsolete("待修改为tilemap")]
    public static void GetSceneCoordDataSet(string sceneName, Action<SceneCoordDataSet> callback)
    {
        string path = $"{ConStr.BattleBlockDatasetPath}{sceneName}_coord_dataset.bytes";
        Addressables.LoadAssetAsync<TextAsset>(path).Completed += r =>
        {
            if (r.Result == null)
                callback(null);

            using (var memory = new MemoryStream(r.Result.bytes))
            {
                var obj = Serializer.Deserialize<SceneCoordDataSet>(memory);
                callback(obj);
            }
        };
    }

    [Obsolete("待修改为tilemap")]
    public static void GetBattleboxDataset(string fullPath, Action<BattleboxDataset> callback)
    {
        Addressables.LoadAssetAsync<TextAsset>(fullPath).Completed += r =>
        {
            if (r.Result == null)
                callback(null);
            using (var memory = new MemoryStream(r.Result.bytes))
            {
                var obj = Serializer.Deserialize<BattleboxDataset>(memory);
                callback(obj);
            }
        };
    }

    public static void SpawnPrefab(string path, Action<GameObject> callback)
    {
        Addressables.InstantiateAsync(path).Completed += r => { callback(r.Result); };
    }

    public static void LoadAsset<T>(string path, Action<T> callback)
    {
        Addressables.LoadAssetAsync<T>(path).Completed += r => { callback(r.Result); };
    }

    public static async UniTask<Jyx2NodeGraph> LoadEventGraph(int id)
    {
        string url = $"Assets/BuildSource/EventsGraph/{id}.asset";
        var rst = await Addressables.LoadResourceLocationsAsync(url).Task;
        if (rst.Count == 0)
        {
            return null;
        }

        return await Addressables.LoadAssetAsync<Jyx2NodeGraph>(url).Task;
    }
    
    //根据Addressable的Ref查找他实际存储的路径
    public static string GetAssetRefAddress(AssetReference reference)
    {
        foreach (var locator in Addressables.ResourceLocators)
        {
            if (locator.Locate(reference.AssetGUID, typeof(Texture2D), out var locs))
            {
                foreach (var loc in locs)
                {
                    return loc.ToString();
                }
            }
        }

        return "";
    }
}