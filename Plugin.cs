using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game;
using Dalamud.Game.Chat;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FrenchChatTranslator;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/frtrad";

    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    /// <summary>Message retenu en attendant sa traduction, ré-émis dans l'ordre d'arrivée.</summary>
    private sealed record Pending(
        XivChatType Type,
        SeString Sender,
        SeString Original,
        string Text,
        Task<TranslationResult?>? Translation);

    private readonly Configuration config;
    private readonly Translator translator = new();
    private readonly WindowSystem windows = new("FrenchChatTranslator");
    private readonly ConfigWindow configWindow;
    private readonly CancellationTokenSource cts = new();

    private readonly Channel<Pending> queue = Channel.CreateUnbounded<Pending>(
        new UnboundedChannelOptions { SingleReader = true });

    private readonly Task worker;
    private int pendingCount;

    // Messages que nous venons d'afficher nous-mêmes : ils repassent par l'événement, on les laisse tranquilles.
    private readonly ConcurrentDictionary<string, (int Count, long Ticks)> expected = new();
    private const long ExpectedTtlMs = 15_000;

    // Liens cliquables sur le tag [EN→FR] : un lien par « slot », réutilisés en boucle
    // (les 256 derniers messages traduits gardent leur original consultable).
    private const int LinkSlots = 256;
    private readonly DalamudLinkPayload[] linkPayloads = new DalamudLinkPayload[LinkSlots];
    private readonly string?[] originals = new string?[LinkSlots];
    private int linkCounter;

    // Infobulle du message d'origine.
    private string? pendingPopup;
    private string? popupText;
    private Vector2 popupOrigin;
    private long popupUntil;

    public Plugin()
    {
        this.config = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        this.configWindow = new ConfigWindow(this.config);
        this.windows.AddWindow(this.configWindow);

        // Langue de l'interface : celle du jeu par défaut (anglais si elle n'est pas gérée).
        Loc.Apply(this.config.UiLanguage, ClientState.ClientLanguage);
        this.RegisterCommand();
        Loc.LanguageChanged += this.RegisterCommand;

        PluginInterface.UiBuilder.Draw += this.windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += this.configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi += this.configWindow.Toggle;

        for (var i = 0; i < LinkSlots; i++)
            this.linkPayloads[i] = ChatGui.AddChatLinkHandler((uint)i, this.OnOriginalLinkClicked);

        PluginInterface.UiBuilder.Draw += this.DrawOriginalPopup;

        this.worker = Task.Run(this.ProcessQueueAsync);

        ChatGui.ChatMessage += this.OnChatMessage;
    }

    // Appelé par le jeu quand on clique sur le tag [EN→FR] d'un message traduit.
    private void OnOriginalLinkClicked(uint commandId, SeString message)
    {
        if (commandId >= LinkSlots)
            return;

        var original = this.originals[commandId];
        if (original != null)
            Interlocked.Exchange(ref this.pendingPopup, original);
    }

    private void DrawOriginalPopup()
    {
        // La position de la souris n'est lue que pendant le dessin ImGui.
        var pending = Interlocked.Exchange(ref this.pendingPopup, null);
        if (pending != null)
        {
            this.popupText = pending;
            this.popupOrigin = ImGui.GetMousePos();
            this.popupUntil = Environment.TickCount64 + 15_000;
        }

        if (this.popupText == null)
            return;

        var mouse = ImGui.GetMousePos();

        // L'infobulle disparaît quand on éloigne la souris, ou au bout de 15 s.
        if (Environment.TickCount64 > this.popupUntil || Vector2.Distance(mouse, this.popupOrigin) > 80f)
        {
            this.popupText = null;
            return;
        }

        ImGui.SetNextWindowPos(new Vector2(mouse.X, mouse.Y - 14f), ImGuiCond.Always, new Vector2(0.5f, 1f));
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 28f);
        ImGui.TextUnformatted(Loc.T("popup_title"));
        ImGui.Separator();
        ImGui.TextUnformatted(this.popupText);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }

    // (Ré)enregistre la commande pour que son texte d'aide suive la langue de l'interface.
    private void RegisterCommand()
    {
        CommandManager.RemoveHandler(CommandName);
        CommandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = Loc.T("command_help"),
        });
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on":
                this.config.Enabled = true;
                this.config.Save();
                ChatGui.Print(Loc.T("msg_enabled"));
                break;
            case "off":
                this.config.Enabled = false;
                this.config.Save();
                ChatGui.Print(Loc.T("msg_disabled"));
                break;
            default:
                this.configWindow.Toggle();
                break;
        }
    }

    private void OnChatMessage(IHandleableChatMessage chatMessage)
    {
        if (!this.config.Enabled || chatMessage.IsHandled)
            return;

        var type = chatMessage.LogKind;
        if (!this.config.EnabledTypes.Contains(type))
            return;

        var rawText = chatMessage.Message.TextValue;

        // Nos propres sorties repassent par cet événement : on les ignore.
        if (this.ConsumeExpected(type, rawText))
            return;

        var text = rawText.Trim();
        var needsTranslation = text.Length >= this.config.MinLength && text.Any(char.IsLetter);

        // L'objet message est réutilisé par Dalamud après le callback : on copie ce dont on a besoin.
        var senderCopy = new SeString(new List<Payload>(chatMessage.Sender.Payloads));

        // --- Mode « ligne supplémentaire » (ancien comportement) ---
        if (!this.config.OverwriteOriginal)
        {
            if (needsTranslation)
                _ = Task.Run(() => this.ShowExtraLineAsync(type, senderCopy, text));
            return;
        }

        // --- Mode « remplacer le message d'origine » ---
        var hasPending = Volatile.Read(ref this.pendingCount) > 0;

        if (!needsTranslation && !hasPending)
            return; // rien à faire, le message passe tel quel

        // Chemin rapide : traduction déjà en cache et aucun message en attente devant nous
        // => on remplace le texte sur place, sans délai et sans réémettre le message.
        if (needsTranslation && !hasPending && this.translator.TryGetCached(text, this.config, out var cached))
        {
            var display = this.BuildDisplay(cached, text);
            if (display != null)
                chatMessage.Message = display;
            return;
        }

        // Chemin lent : on retient le message, on traduit, puis on affiche la traduction (ou l'original)
        // à sa place. La file garantit que l'ordre des messages est conservé.
        var originalCopy = new SeString(new List<Payload>(chatMessage.Message.Payloads));
        var task = needsTranslation
            ? this.translator.TranslateAsync(text, this.config, this.cts.Token)
            : null;

        chatMessage.PreventOriginal();
        Interlocked.Increment(ref this.pendingCount);
        this.queue.Writer.TryWrite(new Pending(type, senderCopy, originalCopy, text, task));
    }

    private async Task ProcessQueueAsync()
    {
        try
        {
            await foreach (var item in this.queue.Reader.ReadAllAsync(this.cts.Token))
            {
                try
                {
                    TranslationResult? result = null;
                    try
                    {
                        if (item.Translation != null)
                            result = await item.Translation;
                    }
                    catch (OperationCanceledException)
                    {
                        // timeout réseau ou arrêt du plugin : on retombe sur le message d'origine
                    }
                    catch (Exception ex)
                    {
                        Log.Warning(ex, "Échec de traduction");
                    }

                    if (this.cts.IsCancellationRequested)
                        break;

                    var display = this.BuildDisplay(result, item.Text) ?? item.Original;
                    await this.EmitAsync(item.Type, item.Sender, display);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Échec d'affichage du message");
                }
                finally
                {
                    Interlocked.Decrement(ref this.pendingCount);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ShowExtraLineAsync(XivChatType type, SeString sender, string text)
    {
        try
        {
            var result = await this.translator.TranslateAsync(text, this.config, this.cts.Token);
            var display = this.BuildDisplay(result, text);
            if (display != null)
                await this.EmitAsync(type, sender, display);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Échec de traduction");
        }
    }

    /// <summary>
    /// Construit le texte à afficher, ou null s'il n'y a rien à remplacer
    /// (déjà dans la langue cible, langue ignorée, traduction identique, échec...).
    /// </summary>
    private SeString? BuildDisplay(TranslationResult? result, string originalText)
    {
        if (result == null)
            return null;

        var target = BaseLang(this.config.TargetLanguage);
        var src = BaseLang(result.SourceLang);

        // Déjà dans la langue choisie => on ne touche à rien.
        if (src == "?" || src == target)
            return null;

        var ignored = this.config.IgnoredLanguages
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(BaseLang);
        if (ignored.Contains(src))
            return null;

        // Traduction identique au texte original (nom propre, emote, etc.).
        if (string.Equals(result.Text.Trim(), originalText, StringComparison.OrdinalIgnoreCase))
            return null;

        var builder = new SeStringBuilder();
        if (this.config.ShowTag)
        {
            // Le tag est un lien cliquable : un clic affiche le message d'origine près de la souris.
            var slot = (int)((uint)Interlocked.Increment(ref this.linkCounter) % LinkSlots);
            this.originals[slot] = originalText;

            builder.Add(this.linkPayloads[slot])
                   .AddUiForeground($"[{src.ToUpperInvariant()}→{target.ToUpperInvariant()}]", 45)
                   .Add(RawPayload.LinkTerminator)
                   .AddText(" ");
        }

        return builder.AddText(result.Text).Build();
    }

    private Task EmitAsync(XivChatType type, SeString sender, SeString message)
    {
        this.MarkExpected(type, message.TextValue);
        return Framework.RunOnFrameworkThread(() =>
        {
            ChatGui.Print(new XivChatEntry
            {
                Type = type,
                Name = sender,
                Message = message,
            });
        });
    }

    private static string BaseLang(string code) => code.Split('-')[0].Trim().ToLowerInvariant();

    private static string Key(XivChatType type, string text) => $"{(int)type}|{text}";

    private void MarkExpected(XivChatType type, string text)
    {
        var now = Environment.TickCount64;

        if (this.expected.Count > 200)
        {
            foreach (var kv in this.expected)
            {
                if (now - kv.Value.Ticks > ExpectedTtlMs)
                    this.expected.TryRemove(kv.Key, out _);
            }
        }

        this.expected.AddOrUpdate(Key(type, text), (1, now), (_, v) => (v.Count + 1, now));
    }

    private bool ConsumeExpected(XivChatType type, string text)
    {
        var key = Key(type, text);
        if (!this.expected.TryGetValue(key, out var v))
            return false;

        if (Environment.TickCount64 - v.Ticks > ExpectedTtlMs)
        {
            this.expected.TryRemove(key, out _);
            return false;
        }

        if (v.Count <= 1)
            this.expected.TryRemove(key, out _);
        else
            this.expected[key] = (v.Count - 1, v.Ticks);

        return true;
    }

    public void Dispose()
    {
        ChatGui.ChatMessage -= this.OnChatMessage;
        ChatGui.RemoveChatLinkHandler();
        Loc.LanguageChanged -= this.RegisterCommand;
        PluginInterface.UiBuilder.Draw -= this.DrawOriginalPopup;
        PluginInterface.UiBuilder.Draw -= this.windows.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= this.configWindow.Toggle;
        PluginInterface.UiBuilder.OpenMainUi -= this.configWindow.Toggle;
        CommandManager.RemoveHandler(CommandName);

        this.cts.Cancel();
        this.queue.Writer.TryComplete();
        this.windows.RemoveAllWindows();
        this.translator.Dispose();
        this.cts.Dispose();
    }
}
