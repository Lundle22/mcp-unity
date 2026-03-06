import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

// --- get_animator_info ---

const getInfoToolName = 'get_animator_info';
const getInfoDescription = 'Gets detailed info about an AnimatorController asset: layers, states, parameters, transitions, blend trees.';
const getInfoParams = z.object({
  assetPath: z.string().describe('The asset path to the AnimatorController (e.g., "Assets/_Project/Art/Models/Characters/SkeletonAnimator.controller")')
});

export function registerGetAnimatorInfoTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${getInfoToolName}`);

  server.tool(
    getInfoToolName,
    getInfoDescription,
    getInfoParams.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${getInfoToolName}`, params);
        const response = await mcpUnity.sendRequest({
          method: getInfoToolName,
          params: { assetPath: params.assetPath }
        });

        if (!response.success) {
          throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to get animator info');
        }

        return {
          content: [{ type: 'text', text: JSON.stringify(response, null, 2) }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${getInfoToolName}`, error);
        throw error;
      }
    }
  );
}

// --- modify_animator_parameter ---

const modifyParamToolName = 'modify_animator_parameter';
const modifyParamDescription = 'Add or remove parameters on an AnimatorController.';
const modifyParamParams = z.object({
  assetPath: z.string().describe('The asset path to the AnimatorController'),
  action: z.string().describe('Action to perform: "add" or "remove"'),
  parameterName: z.string().describe('The name of the parameter'),
  parameterType: z.string().optional().describe('The type of the parameter (required for "add"): "Float", "Int", "Bool", "Trigger"')
});

export function registerModifyAnimatorParameterTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${modifyParamToolName}`);

  server.tool(
    modifyParamToolName,
    modifyParamDescription,
    modifyParamParams.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${modifyParamToolName}`, params);
        const response = await mcpUnity.sendRequest({
          method: modifyParamToolName,
          params: {
            assetPath: params.assetPath,
            action: params.action,
            parameterName: params.parameterName,
            parameterType: params.parameterType
          }
        });

        if (!response.success) {
          throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to modify animator parameter');
        }

        return {
          content: [{ type: response.type, text: response.message }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${modifyParamToolName}`, error);
        throw error;
      }
    }
  );
}

// --- modify_blend_tree ---

const modifyBTToolName = 'modify_blend_tree';
const modifyBTDescription = 'Add, remove, update, or clear motion clips in a blend tree state.';
const modifyBTParams = z.object({
  assetPath: z.string().describe('The asset path to the AnimatorController'),
  stateName: z.string().describe('The name of the state containing the blend tree'),
  layerIndex: z.number().optional().describe('The layer index (default: 0)'),
  action: z.string().describe('Action: "add" (add clip), "remove" (remove by index), "set" (update by index), "clear" (remove all)'),
  clipPath: z.string().optional().describe('Asset path to the AnimationClip or Motion (for "add" and "set")'),
  threshold: z.number().optional().describe('Threshold value for the clip in the blend tree'),
  childIndex: z.number().optional().describe('Index of the child to modify (for "remove" and "set")'),
  blendParameter: z.string().optional().describe('Blend parameter name (used when creating a new blend tree)')
});

export function registerModifyBlendTreeTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${modifyBTToolName}`);

  server.tool(
    modifyBTToolName,
    modifyBTDescription,
    modifyBTParams.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${modifyBTToolName}`, params);
        const response = await mcpUnity.sendRequest({
          method: modifyBTToolName,
          params: {
            assetPath: params.assetPath,
            stateName: params.stateName,
            layerIndex: params.layerIndex,
            action: params.action,
            clipPath: params.clipPath,
            threshold: params.threshold,
            childIndex: params.childIndex,
            blendParameter: params.blendParameter
          }
        });

        if (!response.success) {
          throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to modify blend tree');
        }

        return {
          content: [{ type: response.type, text: response.message }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${modifyBTToolName}`, error);
        throw error;
      }
    }
  );
}
