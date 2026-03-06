import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'set_asset_reference';
const toolDescription = 'Sets an asset reference on a component field. Use this to assign assets like AnimatorController, InputActionAsset, AudioClip, Sprite, Material, etc. to component fields by asset path.';
const paramsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  componentName: z.string().describe('The name of the component (e.g., "PlayerInput", "Animator", "AudioSource")'),
  fieldName: z.string().describe('The field or property name to set (e.g., "m_AnimatorController", "runtimeAnimatorController", "_inputActionAsset")'),
  assetPath: z.string().optional().describe('The asset path (e.g., "Assets/_Project/Art/Models/Characters/SkeletonAnimator.controller")'),
  guid: z.string().optional().describe('The asset GUID (alternative to assetPath)')
});

export function registerSetAssetReferenceTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const result = await toolHandler(mcpUnity, params);
        logger.info(`Tool execution successful: ${toolName}`);
        return result;
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}

async function toolHandler(mcpUnity: McpUnity, params: any): Promise<CallToolResult> {
  if ((params.instanceId === undefined || params.instanceId === null) &&
      (!params.objectPath || params.objectPath.trim() === '')) {
    throw new McpUnityError(ErrorType.VALIDATION, "Either 'instanceId' or 'objectPath' must be provided");
  }

  if (!params.componentName) {
    throw new McpUnityError(ErrorType.VALIDATION, "Required parameter 'componentName' must be provided");
  }

  if (!params.fieldName) {
    throw new McpUnityError(ErrorType.VALIDATION, "Required parameter 'fieldName' must be provided");
  }

  if (!params.assetPath && !params.guid) {
    throw new McpUnityError(ErrorType.VALIDATION, "Either 'assetPath' or 'guid' must be provided");
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      instanceId: params.instanceId,
      objectPath: params.objectPath,
      componentName: params.componentName,
      fieldName: params.fieldName,
      assetPath: params.assetPath,
      guid: params.guid
    }
  });

  if (!response.success) {
    throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to set asset reference');
  }

  return {
    content: [{ type: response.type, text: response.message }]
  };
}
