using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace Skuld;

/// <summary>
/// Pushes gameplay config from server/host to joining clients for the session only.
/// </summary>
internal static class ServerConfigSync
{
    private const byte PackageVersion = 1;
    private static bool rpcRegistered;

    internal static void Initialize()
    {
        if (rpcRegistered || ZRoutedRpc.instance == null)
        {
            return;
        }

        ZRoutedRpc.instance.Register<ZPackage>(ModConstants.ConfigSyncRpc, ReceiveFromServer);
        rpcRegistered = true;
        Plugin.Log.LogInfo("Skuld server config sync RPC registered.");
    }

    internal static void SendToPeer(ZNetPeer peer)
    {
        if (peer == null || ZRoutedRpc.instance == null || ZNet.instance == null || !ZNet.instance.IsServer())
        {
            return;
        }

        ZPackage package = WriteServerPackage();
        ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, ModConstants.ConfigSyncRpc, package);
        Plugin.Log.LogInfo(
            $"Skuld sent gameplay config to peer {peer.m_playerName} (uid={peer.m_uid}): "
            + $"EnableMod={ModConfig.EnableMod.Value}, Paydown={ModConfig.GetPaydownShare():F2}, MaxDebt={ModConfig.GetMaxDebtPerSkill()}");
    }

    internal static void ClearSession()
    {
        ModConfig.ClearServerOverlay();
    }

    private static void ReceiveFromServer(long sender, ZPackage package)
    {
        if (package == null || ZNet.instance == null || ZNet.instance.IsServer())
        {
            return;
        }

        if (!TryReadServerPackage(package, out bool enableMod, out float paydownShare, out int maxDebtPerSkill))
        {
            Plugin.Log.LogWarning("Skuld server config sync: ignored malformed package.");
            return;
        }

        bool localEnableMod = ModConfig.EnableMod.Value;
        ModConfig.ApplyServerOverlay(enableMod, paydownShare, maxDebtPerSkill);
        Plugin.Log.LogInfo(
            $"Skuld applied server gameplay config: EnableMod={enableMod}, Paydown={paydownShare:F2}, MaxDebt={maxDebtPerSkill}");

        if (Plugin.Instance != null)
        {
            Plugin.Instance.StartCoroutine(ShowJoinMessagesAfterPlayerReady(localEnableMod, enableMod, paydownShare, maxDebtPerSkill));
        }
    }

    private static ZPackage WriteServerPackage()
    {
        ZPackage package = new ZPackage();
        package.Write(PackageVersion);
        package.Write(ModConfig.EnableMod.Value);
        package.Write(ModConfig.GetPaydownShare());
        package.Write(ModConfig.GetMaxDebtPerSkill());
        return package;
    }

    private static bool TryReadServerPackage(ZPackage package, out bool enableMod, out float paydownShare, out int maxDebtPerSkill)
    {
        enableMod = true;
        paydownShare = 0.5f;
        maxDebtPerSkill = 3;

        if (package.Size() < 13)
        {
            return false;
        }

        byte version = package.ReadByte();
        if (version != PackageVersion)
        {
            return false;
        }

        enableMod = package.ReadBool();
        paydownShare = package.ReadSingle();
        maxDebtPerSkill = package.ReadInt();
        return true;
    }

    private static IEnumerator ShowJoinMessagesAfterPlayerReady(
        bool localEnableMod,
        bool serverEnableMod,
        float paydownShare,
        int maxDebtPerSkill)
    {
        const int maxFrames = 600;
        for (int i = 0; i < maxFrames; i++)
        {
            Player player = Player.m_localPlayer;
            if (player != null)
            {
                if (localEnableMod != serverEnableMod)
                {
                    string mismatch = serverEnableMod
                        ? "Skuld: server has debt enabled but your local EnableMod is false. Gameplay uses server rules; keep Skuld installed for UI."
                        : "Skuld: server has debt disabled. Your local EnableMod setting is ignored while connected.";
                    player.Message(MessageHud.MessageType.TopLeft, mismatch);
                }

                if (serverEnableMod)
                {
                    string capText = maxDebtPerSkill <= 0 ? "uncapped" : $"{maxDebtPerSkill}x death debt";
                    int paydownPercent = Mathf.RoundToInt(paydownShare * 100f);
                    player.Message(
                        MessageHud.MessageType.TopLeft,
                        $"Skuld server rules: {paydownPercent}% paydown, max {capText}");
                }

                yield break;
            }

            yield return null;
        }
    }

    internal static ZNetPeer FindPeerByRpc(ZRpc rpc)
    {
        if (ZNet.instance == null || rpc == null)
        {
            return null;
        }

        List<ZNetPeer> peers = ZNet.instance.GetPeers();
        for (int i = 0; i < peers.Count; i++)
        {
            ZNetPeer peer = peers[i];
            if (peer?.m_rpc == rpc)
            {
                return peer;
            }
        }

        return null;
    }
}

[HarmonyPatch(typeof(ZNet), "Start")]
internal static class ZNetStartServerConfigSyncPatch
{
    private static void Postfix()
    {
        ServerConfigSync.Initialize();
    }
}

[HarmonyPatch(typeof(ZNet), "RPC_PeerInfo")]
internal static class ZNetPeerInfoServerConfigSyncPatch
{
    private static void Postfix(ZNet __instance, ZRpc rpc)
    {
        if (__instance == null || !__instance.IsServer())
        {
            return;
        }

        ServerConfigSync.Initialize();
        ZNetPeer peer = ServerConfigSync.FindPeerByRpc(rpc);
        if (peer == null)
        {
            return;
        }

        ServerConfigSync.SendToPeer(peer);
    }
}

[HarmonyPatch(typeof(ZNet), nameof(ZNet.Shutdown))]
internal static class ZNetShutdownServerConfigSyncPatch
{
    private static void Prefix()
    {
        ServerConfigSync.ClearSession();
    }
}
