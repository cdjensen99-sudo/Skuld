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
            "Skuld: /show debt [...] or /show focus — show debt or focus paydown status.",
            ShowCommand,
            isCheat: false,
            isNetwork: false,
            onlyServer: false,
            isSecret: false,
            allowInDevBuild: false,
            GetShowTabOptions);

        new Terminal.ConsoleCommand(
            "focus",
            "Skuld: /focus <skill|all|off> — 100% debt paydown until cleared.",
            FocusCommand,
            isCheat: false,
            isNetwork: false,
            onlyServer: false,
            isSecret: false,
            allowInDevBuild: false,
            GetFocusTabOptions);

        registered = true;
        Plugin.Log.LogInfo("Skuld player chat commands registered: /show debt, /show focus, /focus");
    }

    private static void ShowCommand(Terminal.ConsoleEventArgs args)
    {
        if (args.Length < 2)
        {
            args.Context.AddString("Usage: /show debt [...] or /show focus");
            return;
        }

        Player player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context.AddString("No local player.");
            return;
        }

        if (string.Equals(args[1], "focus", StringComparison.OrdinalIgnoreCase))
        {
            SkillDebtService.ShowFocusOverview(player, args.Context);
            return;
        }

        if (!string.Equals(args[1], "debt", StringComparison.OrdinalIgnoreCase))
        {
            args.Context.AddString("Usage: /show debt [...] or /show focus");
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

    private static void FocusCommand(Terminal.ConsoleEventArgs args)
    {
        if (!ModConfig.IsModEnabled)
        {
            args.Context.AddString("Skuld is disabled.");
            return;
        }

        Player player = Player.m_localPlayer;
        if (player == null)
        {
            args.Context.AddString("No local player.");
            return;
        }

        if (args.Length < 2)
        {
            args.Context.AddString("Usage: /focus <skill|all|off>");
            return;
        }

        string target = string.Join(" ", args.Args.Skip(1));
        if (string.Equals(target, "off", StringComparison.OrdinalIgnoreCase))
        {
            SkillDebtService.ClearAllFocusPaydown(player);
            args.Context.AddString("Focused paydown cleared.");
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, "Focused paydown cleared.");
            return;
        }

        if (string.Equals(target, "all", StringComparison.OrdinalIgnoreCase))
        {
            if (!SkillDebtService.TryEnableFocusAll(player, out string message))
            {
                args.Context.AddString("Could not enable focus all.");
                return;
            }

            args.Context.AddString(message);
            MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, message);
            return;
        }

        if (!SkillDebtService.TryResolveSkillType(target, out Skills.SkillType skillType))
        {
            args.Context.AddString($"Unknown skill '{target}'.");
            return;
        }

        if (!SkillDebtService.TryEnableFocusSkill(player, skillType, out string skillMessage))
        {
            args.Context.AddString("Could not enable focused paydown.");
            return;
        }

        args.Context.AddString(skillMessage);
        MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, skillMessage);
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

        float incurred = SkillDebtService.GetIncurred(player, skillType);
        float repaid = SkillDebtService.GetLifetimeRepaid(player, skillType);
        if (incurred <= 0f && repaid <= 0f)
        {
            context.AddString($"{SkillDebtService.FormatSkillName(skillType)}: no lifetime debt recorded.");
            return;
        }

        string detail = SkillDebtService.FormatLifetimeDetail(player, skillType);
        context.AddString($"{SkillDebtService.FormatSkillName(skillType)}: {detail}");
        MessageHud.instance?.ShowMessage(MessageHud.MessageType.TopLeft, $"{SkillDebtService.FormatSkillName(skillType)}: {detail}");
    }

    private static List<string> GetShowTabOptions()
    {
        List<string> list = new() { "debt", "focus", "all", "lifetime" };
        foreach (Skills.SkillType skillType in Enum.GetValues(typeof(Skills.SkillType)))
        {
            if (skillType != Skills.SkillType.None)
            {
                list.Add(skillType.ToString().ToLowerInvariant());
            }
        }

        return list;
    }

    private static List<string> GetFocusTabOptions()
    {
        List<string> list = new() { "all", "off" };
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
