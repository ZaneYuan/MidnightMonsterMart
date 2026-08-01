using System.IO;
using UnityEditor;
using UnityEngine;
using MonsterMart.Core;
using MonsterMart.Data;

namespace MonsterMart.EditorTools
{
    /// <summary>
    /// 把 GameDatabase 里代码构造的数据导出成真正的 .asset 资产。
    ///
    /// 原型默认走「运行时构造」的路线，好处是克隆下来按 Play 就能玩，
    /// 不依赖任何资产 GUID。当你要开始用 Inspector 调数值时，
    /// 跑一次这个菜单，就会在 Assets/Data/ 下生成对应的 ScriptableObject。
    /// </summary>
    public static class DataAssetGenerator
    {
        const string RootFolder = "Assets/Data";

        [MenuItem("Tools/MonsterStore/生成数据资产", false, 10)]
        public static void Generate()
        {
            GameDatabase.Reset();
            GameDatabase.EnsureBuilt();

            EnsureFolder("Assets", "Data");
            EnsureFolder(RootFolder, "Products");
            EnsureFolder(RootFolder, "Customers");
            EnsureFolder(RootFolder, "Days");

            int count = 0;

            for (int i = 0; i < GameDatabase.Products.Count; i++)
            {
                var source = GameDatabase.Products[i];
                var copy = Object.Instantiate(source);
                copy.name = source.productId;
                WriteAsset(copy, $"{RootFolder}/Products/{source.productId}.asset");
                count++;
            }

            for (int i = 0; i < GameDatabase.Customers.Count; i++)
            {
                var source = GameDatabase.Customers[i];
                var copy = Object.Instantiate(source);
                copy.name = source.customerId;
                WriteAsset(copy, $"{RootFolder}/Customers/{source.customerId}.asset");
                count++;
            }

            for (int i = 0; i < GameDatabase.Days.Count; i++)
            {
                var source = GameDatabase.Days[i];
                var copy = Object.Instantiate(source);
                copy.name = $"Day{source.dayNumber}";
                WriteAsset(copy, $"{RootFolder}/Days/Day{source.dayNumber}.asset");
                count++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[MonsterStore] 已生成 {count} 个数据资产到 {RootFolder}/");
            EditorUtility.DisplayDialog("MonsterStore",
                $"已生成 {count} 个数据资产到 {RootFolder}/\n\n" +
                "注意：运行时仍然使用 GameDatabase 里代码构造的数据。\n" +
                "要改为读取资产，需要修改 GameDatabase 的加载逻辑。",
                "知道了");
        }

        [MenuItem("Tools/MonsterStore/删除存档", false, 30)]
        public static void DeleteSave()
        {
            SaveSystem.Delete();
            Debug.Log($"[MonsterStore] 已删除存档：{SaveSystem.FilePath}");
        }

        [MenuItem("Tools/MonsterStore/打开存档目录", false, 31)]
        public static void OpenSaveFolder()
        {
            var path = Application.persistentDataPath;
            if (Directory.Exists(path)) EditorUtility.RevealInFinder(path);
        }

        static void WriteAsset(Object asset, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        static void EnsureFolder(string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
