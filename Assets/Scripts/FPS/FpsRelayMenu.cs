using System;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

[RequireComponent(typeof(NetworkManager), typeof(UnityTransport))]
public sealed class FpsRelayMenu : MonoBehaviour
{
    private NetworkManager network;
    private UnityTransport transport;
    private string joinCodeInput = string.Empty;
    private string hostCode = string.Empty;
    private string status = "Create a 1v1 room or enter a join code.";
    private bool busy;

    private void Awake()
    {
        network = GetComponent<NetworkManager>();
        transport = GetComponent<UnityTransport>();
        network.NetworkConfig.NetworkTransport = transport;
        network.NetworkConfig.ConnectionApproval = true;
        network.ConnectionApprovalCallback += ApproveConnection;
    }

    private void OnDestroy()
    {
        if (network != null) network.ConnectionApprovalCallback -= ApproveConnection;
    }

    private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        bool host = request.ClientNetworkId == Unity.Netcode.NetworkManager.ServerClientId;
        response.Approved = host || network.ConnectedClients.Count < 2;
        response.CreatePlayerObject = response.Approved;
        response.Position = host ? new Vector3(-26f, 0.1f, 0f) : new Vector3(26f, 0.1f, 0f);
        response.Rotation = Quaternion.Euler(0f, host ? 90f : -90f, 0f);
        response.Reason = response.Approved ? string.Empty : "This room already has two players.";
    }

    private async void Host()
    {
        if (busy || network.IsListening) return;
        busy = true;
        status = "Connecting to Unity Relay...";
        try
        {
            await InitializeServices();
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(1);
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            hostCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            status = network.StartHost() ? "Room ready. Share the code with one friend." : "Could not start host.";
        }
        catch (Exception exception)
        {
            status = "Relay failed: " + exception.Message;
            Debug.LogException(exception);
        }
        busy = false;
    }

    private async void Join()
    {
        if (busy || network.IsListening) return;
        string code = joinCodeInput.Trim().ToUpperInvariant();
        if (code.Length == 0) { status = "Enter the host's join code."; return; }
        busy = true;
        status = "Joining Relay room...";
        try
        {
            await InitializeServices();
            JoinAllocation allocation = await RelayService.Instance.JoinAllocationAsync(joinCode: code);
            transport.SetRelayServerData(AllocationUtils.ToRelayServerData(allocation, "dtls"));
            status = network.StartClient() ? "Connecting to host..." : "Could not start client.";
        }
        catch (Exception exception)
        {
            status = "Join failed: " + exception.Message;
            Debug.LogException(exception);
        }
        busy = false;
    }

    private static async System.Threading.Tasks.Task InitializeServices()
    {
        await UnityServices.InitializeAsync();
        if (!AuthenticationService.Instance.IsSignedIn)
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(18, 105, 430, 205), GUI.skin.box);
        GUILayout.Label("BLUE vs RED  |  ONLINE 1v1", new GUIStyle(GUI.skin.label) { fontSize = 19 });
        if (!network.IsListening)
        {
            if (GUILayout.Button("Host room", GUILayout.Height(31))) Host();
            GUILayout.BeginHorizontal();
            joinCodeInput = GUILayout.TextField(joinCodeInput, 12, GUILayout.Width(220));
            if (GUILayout.Button("Join", GUILayout.Height(26))) Join();
            GUILayout.EndHorizontal();
        }
        else
        {
            if (!string.IsNullOrEmpty(hostCode)) GUILayout.Label("JOIN CODE: " + hostCode);
            if (GUILayout.Button("Leave room", GUILayout.Height(26)))
            {
                network.Shutdown();
                hostCode = string.Empty;
                status = "Disconnected.";
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        GUILayout.Label(status);
        GUILayout.EndArea();
    }
}
