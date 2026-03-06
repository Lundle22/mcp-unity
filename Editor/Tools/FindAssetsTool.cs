using System;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for searching the AssetDatabase by type, name, label, or folder.
    /// Returns asset paths, GUIDs, and types for use with other tools.
    /// </summary>
    public class FindAssetsTool : McpToolBase
    {
        public FindAssetsTool()
        {
            Name = "find_assets";
            Description = "Search the Unity AssetDatabase for assets by type, name, or label. Returns paths and GUIDs. Useful for finding assets before assigning them to components.";
        }

        public override JObject Execute(JObject parameters)
        {
            string query = parameters["query"]?.ToObject<string>();
            string type = parameters["type"]?.ToObject<string>();
            string folder = parameters["folder"]?.ToObject<string>();
            int maxResults = parameters["maxResults"]?.ToObject<int>() ?? 50;

            // Build search filter
            string filter = "";
            if (!string.IsNullOrEmpty(query))
            {
                filter = query;
            }
            if (!string.IsNullOrEmpty(type))
            {
                filter += (filter.Length > 0 ? " " : "") + $"t:{type}";
            }

            if (string.IsNullOrEmpty(filter))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "At least one of 'query' or 'type' must be provided",
                    "validation_error"
                );
            }

            string[] searchFolders = null;
            if (!string.IsNullOrEmpty(folder))
            {
                searchFolders = new string[] { folder };
            }

            string[] guids = searchFolders != null
                ? AssetDatabase.FindAssets(filter, searchFolders)
                : AssetDatabase.FindAssets(filter);

            JArray results = new JArray();
            int count = Math.Min(guids.Length, maxResults);
            for (int i = 0; i < count; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Type assetType = AssetDatabase.GetMainAssetTypeAtPath(path);

                results.Add(new JObject
                {
                    ["guid"] = guids[i],
                    ["path"] = path,
                    ["type"] = assetType?.Name ?? "Unknown",
                    ["name"] = System.IO.Path.GetFileNameWithoutExtension(path)
                });
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["totalFound"] = guids.Length,
                ["returned"] = count,
                ["assets"] = results,
                ["message"] = $"Found {guids.Length} assets matching filter '{filter}'" + (guids.Length > count ? $" (showing first {count})" : "")
            };
        }
    }
}
