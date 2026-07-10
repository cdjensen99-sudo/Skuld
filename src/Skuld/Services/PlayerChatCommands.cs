using System;
using System.Collections.Generic;
using System.Linq;

namespace Skuld;

internal static class PlayerChatCommands
{
    private static bool registered;

    internal static void Register()
    {
        if (registered)
        {
            return;
        }

        new Terminal.ConsoleCommand(
            "show",
            "Skuld: /show debt [skill|all|lifetime ...] — show debt progress.",
            ShowCommand,
            isCheat: false,
            isNetwork: false,
            onlyServer: false,
            isSecret: false,
            allowInDevBuild: false,
            GetSkillTabOptions);

        registered = true;
        Plugin.Log.LogInfo("Skuld player chat command registered: /show debt [skill|all|lifetime ...]");
    }

    private static void ShowCommand(Terminal.ConsoleEventArgs args)
    {
        if (args.Length < 2 || !string.Equals(args[1], "debt", StringComparison.OrdinalIgnoreCase))
        {
            args.Context.AddString("Usage: /show debt [skill|all|lifetime <skill|all>]");
            return;
        }

        Player player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context.AddString("No local player.");
            return;
        }

        if (args.Length < 3)
        {
            args.Context.AddString("Usage: /show debt [skill|all|lifetime <skill|all>]");
            return;
        }

        string[] tail = args.Args.Skip(2).ToArray();
        if (tail.Length >= 1 && string.Equals(tail[0], "lifetime", StringComparison.OrdinalIgnoreCase))
        {
            HandleLifetimeCommand(player, args.Context, tail.Skip(1).ToArray());
            return;
        }

        string skillName = string.Join(string.Empty, tail);
        if (string.Equals(skillName, "all", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(string.Join(" ", tail), "all", StringComparison.OrdinalIgnoreCase))
        {
            SkillDebtService.ShowAllDebtOverview(player, args.Context);
            return;
        }

        if (!SkillDebtService.TryResolveSkillType(skillName, out Skills.SkillType skillType))
        {
            skillName = string.Join(" ", tail);
            if (!SkillDebtService.TryResolveSkillType(skillName, out skillType))
            {
                args.Context.AddString($"Unknown skill '{skillName}'.");
                return;
            }
        }

        float remaining = SkillDebtService.GetDebt(player, skillType);
        float paidOff = SkillDebtService.GetPaidOff(player, skillType);
        if (remaining <= 0f && paidOff <= 0f)
        {
            args.Context.AddString($"{SkillDebtService.FormatSkillName(skillType)}: no debt recorded.");
            return;
        }

        string detail = SkillDebtService.FormatProgressDetail(player, skillType);
        args.Context.AddString($"{SkillDebtService.FormatSkillName(skillType)}: {detail}");
        MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, $"{SkillDebtService.FormatSkillName(skillType)} {detail}");
    }

    private static void HandleLifetimeCommand(Player player, Terminal context, string[] tail)
    {
        if (tail.Length == 0)
        {
            context.AddString("Usage: /show debt lifetime <skill|all>");
            return;
        }

        string target = string.Join(" ", tail);
        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
        {
            SkillDebtService.ShowAllLifetimeOverview(player, context);
            return;
        }

        if (!SkillDebtService.TryResolveSkillType(target, out Skills.SkillType skillType))
        {
            context.AddString($"Unknown skill '{target}'.");
            return;
        }

        float lifetime = SkillDebtService.GetIncurred(player, skillType);
        if (lifetime <= 0f)
        {
            context.AddString($"{SkillDebtService.FormatSkillName(skillType)}: no lifetime debt recorded.");
            return;
        }

        string detail = SkillDebtService.FormatLifetimeDetail(player, skillType);
        context.AddString($"{SkillDebtService.FormatSkillName(skillType)} lifetime: {detail}");
        MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, $"{SkillDebtService.FormatSkillName(skillType)} lifetime {detail}");
    }

    private static List<string> GetSkillTabOptions()
    {
        List<string> list = new() { "debt", "all", "lifetime" };
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType != Skills.SkillType.None)
            {
                list.Add(skillType.ToString().ToLowerInvariant());
            }
        }

        return list;
    }
}
