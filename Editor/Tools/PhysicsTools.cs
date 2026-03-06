using System;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for configuring physics components: Rigidbody, colliders, joints, layers.
    /// </summary>
    public class ConfigurePhysicsTool : McpToolBase
    {
        public ConfigurePhysicsTool()
        {
            Name = "configure_physics";
            Description = "Add and configure physics components (Rigidbody, BoxCollider, SphereCollider, CapsuleCollider, MeshCollider) on a GameObject with common settings in one call.";
        }

        public override JObject Execute(JObject parameters)
        {
            int? instanceId = parameters["instanceId"]?.ToObject<int?>();
            string objectPath = parameters["objectPath"]?.ToObject<string>();
            string colliderType = parameters["colliderType"]?.ToObject<string>();
            bool addRigidbody = parameters["addRigidbody"]?.ToObject<bool>() ?? false;
            bool isKinematic = parameters["isKinematic"]?.ToObject<bool>() ?? false;
            bool useGravity = parameters["useGravity"]?.ToObject<bool>() ?? true;
            float? mass = parameters["mass"]?.ToObject<float?>();
            float? drag = parameters["drag"]?.ToObject<float?>();
            bool isTrigger = parameters["isTrigger"]?.ToObject<bool>() ?? false;
            JObject center = parameters["center"] as JObject;
            JObject size = parameters["size"] as JObject;
            float? radius = parameters["radius"]?.ToObject<float?>();
            float? height = parameters["height"]?.ToObject<float?>();
            int? direction = parameters["direction"]?.ToObject<int?>();
            bool convex = parameters["convex"]?.ToObject<bool>() ?? false;

            // Find the GameObject
            GameObject gameObject = FindGameObject(instanceId, objectPath);
            if (gameObject == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    $"GameObject not found",
                    "not_found_error"
                );
            }

            string resultMsg = "";

            // Add Rigidbody if requested
            if (addRigidbody)
            {
                Rigidbody rb = gameObject.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = Undo.AddComponent<Rigidbody>(gameObject);
                    resultMsg += "Added Rigidbody. ";
                }

                Undo.RecordObject(rb, "Configure Rigidbody");
                rb.isKinematic = isKinematic;
                rb.useGravity = useGravity;
                if (mass.HasValue) rb.mass = mass.Value;
                if (drag.HasValue) rb.linearDamping = drag.Value;
                EditorUtility.SetDirty(rb);
                resultMsg += $"Configured Rigidbody (kinematic={isKinematic}, gravity={useGravity}). ";
            }

            // Add collider if requested
            if (!string.IsNullOrEmpty(colliderType))
            {
                Vector3 centerVec = center != null
                    ? new Vector3(center["x"]?.ToObject<float>() ?? 0, center["y"]?.ToObject<float>() ?? 0, center["z"]?.ToObject<float>() ?? 0)
                    : Vector3.zero;

                switch (colliderType.ToLower())
                {
                    case "box":
                        BoxCollider box = gameObject.GetComponent<BoxCollider>();
                        if (box == null) box = Undo.AddComponent<BoxCollider>(gameObject);
                        Undo.RecordObject(box, "Configure BoxCollider");
                        box.isTrigger = isTrigger;
                        box.center = centerVec;
                        if (size != null)
                        {
                            box.size = new Vector3(
                                size["x"]?.ToObject<float>() ?? 1,
                                size["y"]?.ToObject<float>() ?? 1,
                                size["z"]?.ToObject<float>() ?? 1
                            );
                        }
                        EditorUtility.SetDirty(box);
                        resultMsg += "Configured BoxCollider. ";
                        break;

                    case "sphere":
                        SphereCollider sphere = gameObject.GetComponent<SphereCollider>();
                        if (sphere == null) sphere = Undo.AddComponent<SphereCollider>(gameObject);
                        Undo.RecordObject(sphere, "Configure SphereCollider");
                        sphere.isTrigger = isTrigger;
                        sphere.center = centerVec;
                        if (radius.HasValue) sphere.radius = radius.Value;
                        EditorUtility.SetDirty(sphere);
                        resultMsg += "Configured SphereCollider. ";
                        break;

                    case "capsule":
                        CapsuleCollider capsule = gameObject.GetComponent<CapsuleCollider>();
                        if (capsule == null) capsule = Undo.AddComponent<CapsuleCollider>(gameObject);
                        Undo.RecordObject(capsule, "Configure CapsuleCollider");
                        capsule.isTrigger = isTrigger;
                        capsule.center = centerVec;
                        if (radius.HasValue) capsule.radius = radius.Value;
                        if (height.HasValue) capsule.height = height.Value;
                        if (direction.HasValue) capsule.direction = direction.Value;
                        EditorUtility.SetDirty(capsule);
                        resultMsg += "Configured CapsuleCollider. ";
                        break;

                    case "mesh":
                        MeshCollider mesh = gameObject.GetComponent<MeshCollider>();
                        if (mesh == null) mesh = Undo.AddComponent<MeshCollider>(gameObject);
                        Undo.RecordObject(mesh, "Configure MeshCollider");
                        mesh.isTrigger = isTrigger;
                        mesh.convex = convex;
                        EditorUtility.SetDirty(mesh);
                        resultMsg += "Configured MeshCollider. ";
                        break;

                    default:
                        return McpUnitySocketHandler.CreateErrorResponse(
                            $"Unknown collider type '{colliderType}'. Use: box, sphere, capsule, mesh",
                            "validation_error"
                        );
                }
            }

            if (string.IsNullOrEmpty(resultMsg))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "No physics configuration specified. Provide 'addRigidbody' and/or 'colliderType'.",
                    "validation_error"
                );
            }

            return new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["message"] = resultMsg.Trim()
            };
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

                string[] parts = objectPath.Split('/');
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
    }
}
