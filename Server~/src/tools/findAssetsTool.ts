import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'find_assets';
const toolDescription = 'Search the Unity AssetDatabase for assets by type, name, or label. Returns paths and GUIDs. Useful for finding assets before assigning them to components.';
const paramsSchema = z.object({
  query: z.string().optional().describe('Search query string (asset name or partial name)'),
  type: z.string().optional().describe('Asset type filter (e.g., "AnimatorController", "AnimationClip", "AudioClip", "Material", "Prefab", "Shader", "Texture2D", "ScriptableObject", "InputActionAsset")'),
  folder: z.string().optional().describe('Folder to search in (e.g., "Assets/_Project/Art")'),
  maxResults: z.number().optional().describe('Maximum number of results to return (default: 50)')
});

export function registerFindAssetsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
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
  if (!params.query && !params.type) {
    throw new McpUnityError(ErrorType.VALIDATION, "At least one of 'query' or 'type' must be provided");
  }

  const response = await mcpUnity.sendRequest({
    method: toolName,
    params: {
      query: params.query,
      type: params.type,
      folder: params.folder,
      maxResults: params.maxResults
    }
  });

  if (!response.success) {
    throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to search assets');
  }

  // Format the results nicely
  let text = response.message || '';
  if (response.assets) {
    text += '\n\nResults:\n';
    for (const asset of response.assets) {
      text += `- ${asset.name} (${asset.type}) — ${asset.path}\n`;
    }
  }

  return {
    content: [{ type: 'text', text: text.trim() }]
  };
}
