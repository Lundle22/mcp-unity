using System;
using System.Linq;
using McpUnity.Unity;
using McpUnity.Utils;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using Newtonsoft.Json.Linq;

namespace McpUnity.Tools
{
    /// <summary>
    /// Tool for reading and modifying Animator Controllers: parameters, states, blend trees, transitions.
    /// </summary>
    public class GetAnimatorInfoTool : McpToolBase
    {
        public GetAnimatorInfoTool()
        {
            Name = "get_animator_info";
            Description = "Gets detailed info about an AnimatorController asset: layers, states, parameters, transitions, blend trees.";
        }

        public override JObject Execute(JObject parameters)
        {
            string assetPath = parameters["assetPath"]?.ToObject<string>();
            if (string.IsNullOrEmpty(assetPath))
            {
                return McpUnitySocketHandler.CreateErrorResponse("Required parameter 'assetPath' not provided", "validation_error");
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (controller == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"AnimatorController not found at '{assetPath}'", "not_found_error");
            }

            JObject result = new JObject
            {
                ["success"] = true,
                ["type"] = "text",
                ["name"] = controller.name,
                ["assetPath"] = assetPath
            };

            // Parameters
            JArray paramsArray = new JArray();
            foreach (var param in controller.parameters)
            {
                paramsArray.Add(new JObject
                {
                    ["name"] = param.name,
                    ["type"] = param.type.ToString(),
                    ["defaultFloat"] = param.defaultFloat,
                    ["defaultInt"] = param.defaultInt,
                    ["defaultBool"] = param.defaultBool
                });
            }
            result["parameters"] = paramsArray;

            // Layers
            JArray layersArray = new JArray();
            foreach (var layer in controller.layers)
            {
                JObject layerObj = new JObject
                {
                    ["name"] = layer.name,
                    ["defaultWeight"] = layer.defaultWeight
                };

                JArray statesArray = new JArray();
                if (layer.stateMachine != null)
                {
                    foreach (var childState in layer.stateMachine.states)
                    {
                        JObject stateObj = new JObject
                        {
                            ["name"] = childState.state.name,
                            ["speed"] = childState.state.speed,
                            ["tag"] = childState.state.tag
                        };

                        if (childState.state.motion is BlendTree blendTree)
                        {
                            stateObj["motionType"] = "BlendTree";
                            stateObj["blendTree"] = SerializeBlendTree(blendTree);
                        }
                        else if (childState.state.motion != null)
                        {
                            stateObj["motionType"] = "AnimationClip";
                            stateObj["motionName"] = childState.state.motion.name;
                            string clipPath = AssetDatabase.GetAssetPath(childState.state.motion);
                            if (!string.IsNullOrEmpty(clipPath))
                            {
                                stateObj["motionAssetPath"] = clipPath;
                            }
                        }

                        // Transitions
                        JArray transArray = new JArray();
                        foreach (var trans in childState.state.transitions)
                        {
                            JObject transObj = new JObject
                            {
                                ["destinationState"] = trans.destinationState?.name,
                                ["hasExitTime"] = trans.hasExitTime,
                                ["exitTime"] = trans.exitTime,
                                ["duration"] = trans.duration,
                                ["hasFixedDuration"] = trans.hasFixedDuration
                            };

                            JArray conditionsArray = new JArray();
                            foreach (var condition in trans.conditions)
                            {
                                conditionsArray.Add(new JObject
                                {
                                    ["parameter"] = condition.parameter,
                                    ["mode"] = condition.mode.ToString(),
                                    ["threshold"] = condition.threshold
                                });
                            }
                            transObj["conditions"] = conditionsArray;
                            transArray.Add(transObj);
                        }
                        stateObj["transitions"] = transArray;

                        statesArray.Add(stateObj);
                    }

                    // Default state
                    if (layer.stateMachine.defaultState != null)
                    {
                        layerObj["defaultState"] = layer.stateMachine.defaultState.name;
                    }
                }
                layerObj["states"] = statesArray;
                layersArray.Add(layerObj);
            }
            result["layers"] = layersArray;
            result["message"] = $"AnimatorController '{controller.name}' has {controller.parameters.Length} parameters and {controller.layers.Length} layers";

            return result;
        }

        private JObject SerializeBlendTree(BlendTree tree)
        {
            JObject btObj = new JObject
            {
                ["blendParameter"] = tree.blendParameter,
                ["blendType"] = tree.blendType.ToString(),
                ["minThreshold"] = tree.minThreshold,
                ["maxThreshold"] = tree.maxThreshold
            };

            JArray children = new JArray();
            for (int i = 0; i < tree.children.Length; i++)
            {
                var child = tree.children[i];
                JObject childObj = new JObject
                {
                    ["threshold"] = child.threshold,
                    ["timeScale"] = child.timeScale
                };

                if (child.motion is BlendTree subTree)
                {
                    childObj["type"] = "BlendTree";
                    childObj["blendTree"] = SerializeBlendTree(subTree);
                }
                else if (child.motion != null)
                {
                    childObj["type"] = "AnimationClip";
                    childObj["motionName"] = child.motion.name;
                    string clipPath = AssetDatabase.GetAssetPath(child.motion);
                    if (!string.IsNullOrEmpty(clipPath))
                    {
                        childObj["motionAssetPath"] = clipPath;
                    }
                }

                children.Add(childObj);
            }
            btObj["children"] = children;
            return btObj;
        }
    }

    /// <summary>
    /// Tool for modifying AnimatorController parameters.
    /// </summary>
    public class ModifyAnimatorParameterTool : McpToolBase
    {
        public ModifyAnimatorParameterTool()
        {
            Name = "modify_animator_parameter";
            Description = "Add, remove, or update parameters on an AnimatorController.";
        }

        public override JObject Execute(JObject parameters)
        {
            string assetPath = parameters["assetPath"]?.ToObject<string>();
            string action = parameters["action"]?.ToObject<string>(); // add, remove
            string paramName = parameters["parameterName"]?.ToObject<string>();
            string paramType = parameters["parameterType"]?.ToObject<string>(); // Float, Int, Bool, Trigger

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(action) || string.IsNullOrEmpty(paramName))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameters: 'assetPath', 'action', 'parameterName'",
                    "validation_error"
                );
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (controller == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"AnimatorController not found at '{assetPath}'", "not_found_error");
            }

            Undo.RecordObject(controller, $"Modify animator parameter '{paramName}'");

            if (action == "add")
            {
                if (string.IsNullOrEmpty(paramType))
                {
                    return McpUnitySocketHandler.CreateErrorResponse("'parameterType' required for 'add' action", "validation_error");
                }

                AnimatorControllerParameterType type;
                if (!Enum.TryParse(paramType, true, out type))
                {
                    return McpUnitySocketHandler.CreateErrorResponse($"Invalid parameter type '{paramType}'. Use: Float, Int, Bool, Trigger", "validation_error");
                }

                controller.AddParameter(paramName, type);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Added parameter '{paramName}' ({paramType}) to '{controller.name}'"
                };
            }
            else if (action == "remove")
            {
                var existingParams = controller.parameters;
                int index = Array.FindIndex(existingParams, p => p.name == paramName);
                if (index < 0)
                {
                    return McpUnitySocketHandler.CreateErrorResponse($"Parameter '{paramName}' not found", "not_found_error");
                }

                controller.RemoveParameter(index);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Removed parameter '{paramName}' from '{controller.name}'"
                };
            }

            return McpUnitySocketHandler.CreateErrorResponse($"Unknown action '{action}'. Use 'add' or 'remove'", "validation_error");
        }
    }

    /// <summary>
    /// Tool for modifying blend tree children (add/remove/update clips and thresholds).
    /// </summary>
    public class ModifyBlendTreeTool : McpToolBase
    {
        public ModifyBlendTreeTool()
        {
            Name = "modify_blend_tree";
            Description = "Add, remove, or update motion clips in a blend tree state. Specify the state name and layer index.";
        }

        public override JObject Execute(JObject parameters)
        {
            string assetPath = parameters["assetPath"]?.ToObject<string>();
            string stateName = parameters["stateName"]?.ToObject<string>();
            int layerIndex = parameters["layerIndex"]?.ToObject<int>() ?? 0;
            string action = parameters["action"]?.ToObject<string>(); // add, remove, set, clear
            string clipPath = parameters["clipPath"]?.ToObject<string>();
            float? threshold = parameters["threshold"]?.ToObject<float?>();
            int? childIndex = parameters["childIndex"]?.ToObject<int?>();
            string blendParameter = parameters["blendParameter"]?.ToObject<string>();

            if (string.IsNullOrEmpty(assetPath) || string.IsNullOrEmpty(stateName) || string.IsNullOrEmpty(action))
            {
                return McpUnitySocketHandler.CreateErrorResponse(
                    "Required parameters: 'assetPath', 'stateName', 'action'",
                    "validation_error"
                );
            }

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(assetPath);
            if (controller == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"AnimatorController not found at '{assetPath}'", "not_found_error");
            }

            if (layerIndex < 0 || layerIndex >= controller.layers.Length)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"Layer index {layerIndex} out of range", "validation_error");
            }

            var stateMachine = controller.layers[layerIndex].stateMachine;
            AnimatorState state = null;
            foreach (var cs in stateMachine.states)
            {
                if (cs.state.name == stateName)
                {
                    state = cs.state;
                    break;
                }
            }

            if (state == null)
            {
                return McpUnitySocketHandler.CreateErrorResponse($"State '{stateName}' not found in layer {layerIndex}", "not_found_error");
            }

            BlendTree blendTree = state.motion as BlendTree;

            if (action == "add")
            {
                if (string.IsNullOrEmpty(clipPath))
                {
                    return McpUnitySocketHandler.CreateErrorResponse("'clipPath' required for 'add' action", "validation_error");
                }

                Motion clip = AssetDatabase.LoadAssetAtPath<Motion>(clipPath);
                if (clip == null)
                {
                    // Try loading as sub-asset
                    var allAssets = AssetDatabase.LoadAllAssetsAtPath(clipPath);
                    clip = allAssets?.OfType<AnimationClip>().FirstOrDefault();
                }

                if (clip == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse($"Motion/clip not found at '{clipPath}'", "not_found_error");
                }

                if (blendTree == null)
                {
                    // Create a new blend tree for this state
                    blendTree = new BlendTree { name = stateName + "_BlendTree" };
                    if (!string.IsNullOrEmpty(blendParameter))
                    {
                        blendTree.blendParameter = blendParameter;
                    }
                    AssetDatabase.AddObjectToAsset(blendTree, assetPath);
                    state.motion = blendTree;
                }

                blendTree.AddChild(clip, threshold ?? 0f);
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Added clip '{clip.name}' at threshold {threshold ?? 0f} to blend tree in state '{stateName}'"
                };
            }
            else if (action == "remove" && childIndex.HasValue)
            {
                if (blendTree == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse("State has no blend tree", "validation_error");
                }

                var children = blendTree.children;
                if (childIndex.Value < 0 || childIndex.Value >= children.Length)
                {
                    return McpUnitySocketHandler.CreateErrorResponse($"Child index {childIndex.Value} out of range (0-{children.Length - 1})", "validation_error");
                }

                var list = children.ToList();
                list.RemoveAt(childIndex.Value);
                blendTree.children = list.ToArray();
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Removed child at index {childIndex.Value} from blend tree in state '{stateName}'"
                };
            }
            else if (action == "set" && childIndex.HasValue)
            {
                if (blendTree == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse("State has no blend tree", "validation_error");
                }

                var children = blendTree.children;
                if (childIndex.Value < 0 || childIndex.Value >= children.Length)
                {
                    return McpUnitySocketHandler.CreateErrorResponse($"Child index {childIndex.Value} out of range", "validation_error");
                }

                var child = children[childIndex.Value];
                if (threshold.HasValue) child.threshold = threshold.Value;

                if (!string.IsNullOrEmpty(clipPath))
                {
                    Motion clip = AssetDatabase.LoadAssetAtPath<Motion>(clipPath);
                    if (clip == null)
                    {
                        var allAssets = AssetDatabase.LoadAllAssetsAtPath(clipPath);
                        clip = allAssets?.OfType<AnimationClip>().FirstOrDefault();
                    }
                    if (clip != null) child.motion = clip;
                }

                children[childIndex.Value] = child;
                blendTree.children = children;
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Updated child at index {childIndex.Value} in blend tree"
                };
            }
            else if (action == "clear")
            {
                if (blendTree == null)
                {
                    return McpUnitySocketHandler.CreateErrorResponse("State has no blend tree", "validation_error");
                }

                blendTree.children = new ChildMotion[0];
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();

                return new JObject
                {
                    ["success"] = true,
                    ["type"] = "text",
                    ["message"] = $"Cleared all children from blend tree in state '{stateName}'"
                };
            }

            return McpUnitySocketHandler.CreateErrorResponse($"Unknown action '{action}'. Use 'add', 'remove', 'set', or 'clear'", "validation_error");
        }
    }
}
