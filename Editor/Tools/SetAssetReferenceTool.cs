using System;
using System.Reflection;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for setting asset references on component fields.
    /// This handles the key gap in update_component: assigning Unity assets
    /// (InputActionAsset, AnimatorController, AudioClip, Material, Sprite, etc.)
    /// to component fields by asset path or GUID.
    /// </summary>
    public class SetAssetReferenceTool : McpToolBase
    {
        public SetAssetReferenceTool()
        {
            Name = "set_asset_reference";
            Description = "Sets an asset reference on a component field. Use this to assign assets like AnimatorController, InputActionAsset, AudioClip, Sprite, Material, etc. to component fields by asset path.";
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string componentName = parameters["componentName"]?.ToObject<string>();
            string fieldName = parameters["fieldName"]?.ToObject<string>();
            string assetPath = parameters["assetPath"]?.ToObject<string>();
            string guid = parameters["guid"]?.ToObject<string>();

            if (!instanceId.HasValue && string.IsNullOrEmpty(objectPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'instanceId' or 'objectPath' must be provided",
                    "validation_error"
                );
            }

            if (string.IsNullOrEmpty(componentName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'componentName' not provided",
                    "validation_error"
                );
            }

            if (string.IsNullOrEmpty(fieldName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameter 'fieldName' not provided",
                    "validation_error"
                );
            }

            if (string.IsNullOrEmpty(assetPath) && string.IsNullOrEmpty(guid))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Either 'assetPath' or 'guid' must be provided",
                    "validation_error"
                );
            }

            // Find the GameObject
            GameObject gameObject = FindGameObject(instanceId, objectPath);
            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject not found (instanceId: {instanceId}, path: '{objectPath}')",
                    "not_found_error"
                );
            }

            // Find the component
            Component component = gameObject.GetComponent(componentName);
            if (component == null)
            {
                // Try searching all assemblies
                Type componentType = FindComponentType(componentName);
                if (componentType != null)
                {
                    component = gameObject.GetComponent(componentType);
                }
            }

            if (component == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Component '{componentName}' not found on GameObject '{gameObject.name}'",
                    "not_found_error"
                );
            }

            // Load the asset
            UnityEngine.Object asset = null;
            if (!string.IsNullOrEmpty(assetPath))
            {
                asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null)
                {
                    // Try loading sub-assets (e.g., animation clips inside FBX)
                    UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
                    if (subAssets != null && subAssets.Length > 0)
                    {
                        asset = subAssets[0];
                    }
                }
            }
            else if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                }
            }

            if (asset == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"Asset not found at path '{assetPath}' or GUID '{guid}'",
                    "not_found_error"
                );
            }

            // Find and set the field or property
            Type compType = component.GetType();
            Undo.RecordObject(component, $"Set {fieldName} on {compType.Name}");

            // Try field first
            FieldInfo fieldInfo = compType.GetField(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (fieldInfo != null)
            {
                if (!fieldInfo.FieldType.IsAssignableFrom(asset.GetType()))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Asset type '{asset.GetType().Name}' is not assignable to field type '{fieldInfo.FieldType.Name}'",
                        "type_error"
                    );
                }

                fieldInfo.SetValue(component, asset);
                EditorUtility.SetDirty(component);

                if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Set field '{fieldName}' on '{compType.Name}' to asset '{asset.name}' ({asset.GetType().Name})"
                };
            }

            // Try property
            PropertyInfo propInfo = compType.GetProperty(fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            if (propInfo != null && propInfo.CanWrite)
            {
                if (!propInfo.PropertyType.IsAssignableFrom(asset.GetType()))
                {
                    return McpUnitySocketHandler.CreateErrorResponse(
                        $"Asset type '{asset.GetType().Name}' is not assignable to property type '{propInfo.PropertyType.Name}'",
                        "type_error"
                    );
                }

                propInfo.SetValue(component, asset);
                EditorUtility.SetDirty(component);

                if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
                }

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Set property '{fieldName}' on '{compType.Name}' to asset '{asset.name}' ({asset.GetType().Name})"
                };
            }

            // Try SerializedObject approach as fallback (handles [SerializeField] private fields)
            SerializedObject serializedObj = new SerializedObject(component);
            SerializedProperty serializedProp = serializedObj.FindProperty(fieldName);

            if (serializedProp != null && serializedProp.propertyType == SerializedPropertyType.ObjectReference)
            {
                serializedProp.objectReferenceValue = asset;
                serializedObj.ApplyModifiedProperties();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Set serialized field '{fieldName}' on '{compType.Name}' to asset '{asset.name}' ({asset.GetType().Name})"
                };
            }

            return McpUnitySocketHandler.CreateErrorResponse(
                $"Field or property '{fieldName}' not found on component '{compType.Name}', or it is not an object reference",
                "not_found_error"
            );
        }

        private GameObject FindGameObject(int? instanceId, string objectPath)
        {
            if (instanceId.HasValue)
            {
                return EditorUtility.InstanceIDToObject(instanceId.Value) as GameObject;
            }

            if (!string.IsNullOrEmpty(objectPath))
            {
                GameObject go = GameObject.Find(objectPath);
                if (go != null) return go;

                // Try hierarchy path
                string[] parts = objectPath.Split('/');
                if (parts.Length == 0) return null;

                foreach (GameObject root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (root.name != parts[0]) continue;

                    GameObject current = root;
                    for (int i = 1; i < parts.Length; i++)
                    {
                        Transform child = current.transform.Find(parts[i]);
                        if (child == null) return null;
                        current = child.gameObject;
                    }
                    return current;
                }
            }

            return null;
        }

        private Type FindComponentType(string componentName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (Type t in assembly.GetTypes())
                    {
                        if (t.Name == componentName && typeof(Component).IsAssignableFrom(t))
                        {
                            return t;
                        }
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }
            return null;
        }
    }
}
