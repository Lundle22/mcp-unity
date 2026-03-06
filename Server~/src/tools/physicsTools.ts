import * as z from 'zod';
import { Logger } from '../utils/logger.js';
import { McpUnity } from '../unity/mcpUnity.js';
import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { McpUnityError, ErrorType } from '../utils/errors.js';
import { CallToolResult } from '@modelcontextprotocol/sdk/types.js';

const toolName = 'configure_physics';
const toolDescription = 'Add and configure physics components (Rigidbody, BoxCollider, SphereCollider, CapsuleCollider, MeshCollider) on a GameObject with common settings in one call.';
const paramsSchema = z.object({
  instanceId: z.number().optional().describe('The instance ID of the GameObject'),
  objectPath: z.string().optional().describe('The path of the GameObject in the hierarchy (alternative to instanceId)'),
  addRigidbody: z.boolean().optional().describe('Whether to add/configure a Rigidbody (default: false)'),
  isKinematic: z.boolean().optional().describe('Set Rigidbody kinematic state (default: false)'),
  useGravity: z.boolean().optional().describe('Set Rigidbody gravity (default: true)'),
  mass: z.number().optional().describe('Rigidbody mass'),
  drag: z.number().optional().describe('Rigidbody linear drag'),
  colliderType: z.string().optional().describe('Type of collider to add: "box", "sphere", "capsule", "mesh"'),
  isTrigger: z.boolean().optional().describe('Set collider as trigger (default: false)'),
  center: z.object({
    x: z.number().optional(),
    y: z.number().optional(),
    z: z.number().optional()
  }).optional().describe('Collider center offset'),
  size: z.object({
    x: z.number().optional(),
    y: z.number().optional(),
    z: z.number().optional()
  }).optional().describe('BoxCollider size'),
  radius: z.number().optional().describe('SphereCollider/CapsuleCollider radius'),
  height: z.number().optional().describe('CapsuleCollider height'),
  direction: z.number().optional().describe('CapsuleCollider direction (0=X, 1=Y, 2=Z)'),
  convex: z.boolean().optional().describe('MeshCollider convex setting (default: false)')
});

export function registerConfigurePhysicsTool(server: McpServer, mcpUnity: McpUnity, logger: Logger) {
  logger.info(`Registering tool: ${toolName}`);

  server.tool(
    toolName,
    toolDescription,
    paramsSchema.shape,
    async (params: any) => {
      try {
        logger.info(`Executing tool: ${toolName}`, params);
        const response = await mcpUnity.sendRequest({
          method: toolName,
          params: params
        });

        if (!response.success) {
          throw new McpUnityError(ErrorType.TOOL_EXECUTION, response.message || 'Failed to configure physics');
        }

        return {
          content: [{ type: response.type, text: response.message }]
        };
      } catch (error) {
        logger.error(`Tool execution failed: ${toolName}`, error);
        throw error;
      }
    }
  );
}
