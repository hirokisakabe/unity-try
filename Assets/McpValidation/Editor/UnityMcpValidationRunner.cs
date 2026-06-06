using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Services;
using MCPForUnity.Editor.Services.Transport;
using UnityEditor;
using UnityEngine;

namespace UnityTry.McpValidation.Editor
{
    public static class UnityMcpValidationRunner
    {
        public static void StartHttpBridge()
        {
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            HttpEndpointUtility.SaveLocalBaseUrl("http://127.0.0.1:8080");
            EditorApplication.delayCall += () => _ = StartHttpBridgeAsync();
        }

        private static async Task StartHttpBridgeAsync()
        {
            bool started = await MCPServiceLocator.TransportManager.StartAsync(TransportMode.Http);
            bool verified = await MCPServiceLocator.TransportManager.VerifyAsync(TransportMode.Http);
            var state = MCPServiceLocator.TransportManager.GetState(TransportMode.Http);

            Debug.Log(
                $"[UnityMcpValidation] HTTP bridge started={started}, verified={verified}, " +
                $"connected={state.IsConnected}, error={state.Error}");

            if (!started || !verified || !state.IsConnected)
            {
                throw new InvalidOperationException($"Unity MCP HTTP bridge failed: {state.Error}");
            }
        }
    }
}
