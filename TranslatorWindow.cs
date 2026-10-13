using System;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FrenchChatTranslator;

/// <summary>
/// Fenêtre /htranslate : on écrit son message, il est traduit (langue d'origine détectée
/// automatiquement) et on copie le résultat dans le presse-papiers pour le coller dans le chat.
/// Rien n'est envoyé automatiquement au jeu.
/// </summary>
public sealed class TranslatorWindow : Window
{
    private const long DebounceMs = 600; // on traduit quand on a arrêté de taper
    private const long CopiedLabelMs = 2000;

    private readonly Configuration config;
    private readonly Translator translator;
    private readonly CancellationToken ct;

    // État modifié uniquement par le thread d'affichage.
    private string input = string.Empty;
    private bool dirty;
    private long lastEditTicks;
    private long copiedUntil;

    // État partagé avec la tâche de traduction.
    private readonly object stateLock = new();
    private string output = string.Empty;
    private string detected = string.Empty;
    private bool busy;
    private bool failed;
    private int version;

    public TranslatorWindow(Configuration config, Translator translator, CancellationToken ct)
        : base("Translate my message##htranslate")
    {
        this.config = config;
        this.translator = translator;
        this.ct = ct;

        this.Size = new Vector2(440, 400);
        this.SizeCondition = ImGuiCond.FirstUseEver;
        this.SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(320, 320),
            MaximumSize = new Vector2(1200, 900),
        };
    }

    // Le titre suit la langue de l'interface.
    public override void PreDraw()
    {
        this.WindowName = Loc.T("tw_title") + "##htranslate";
    }

    public override void Draw()
    {
        // --- Langue cible ---
        var langLabels = ConfigWindow.LanguageCodes.Select(ConfigWindow.LanguageLabel).ToArray();
        var langIndex = Array.IndexOf(ConfigWindow.LanguageCodes, this.config.OutgoingLanguage);
        if (langIndex < 0) langIndex = Array.IndexOf(ConfigWindow.LanguageCodes, "en");
        if (ImGui.Combo(Loc.T("tw_target"), ref langIndex, langLabels, langLabels.Length))
        {
            this.config.OutgoingLanguage = ConfigWindow.LanguageCodes[langIndex];
            this.config.Save();
            this.Request();
        }

        ImGui.Spacing();

        // --- Message à traduire ---
        ImGui.TextUnformatted(Loc.T("tw_input"));
        var text = this.input;
        if (ImGui.InputTextMultiline("##tw_in", ref text, 1000, new Vector2(-1f, 100f)))
        {
            this.input = text;
            this.lastEditTicks = Environment.TickCount64;
            this.dirty = true;
        }

        if (this.dirty && Environment.TickCount64 - this.lastEditTicks >= DebounceMs)
            this.Request();

        ImGui.Spacing();

        // --- Traduction ---
        string outText, det;
        bool isBusy, isFailed;
        lock (this.stateLock)
        {
            outText = this.output;
            det = this.detected;
            isBusy = this.busy;
            isFailed = this.failed;
        }

        ImGui.TextUnformatted(Loc.T("tw_output"));
        ImGui.InputTextMultiline("##tw_out", ref outText, 2000, new Vector2(-1f, 100f), ImGuiInputTextFlags.ReadOnly);

        // --- Statut ---
        if (isBusy)
        {
            ImGui.TextUnformatted(Loc.T("tw_translating"));
        }
        else if (isFailed)
        {
            ImGui.PushTextWrapPos(0f);
            ImGui.TextColored(new Vector4(1f, 0.45f, 0.45f, 1f), Loc.T("tw_error"));
            ImGui.PopTextWrapPos();
        }
        else if (det.Length > 0)
        {
            ImGui.TextUnformatted($"{Loc.T("tw_detected")} {LanguageName(det)}");
        }

        ImGui.Spacing();

        // --- Boutons ---
        var canCopy = outText.Length > 0 && !isBusy && !isFailed;
        if (ImGui.Button(Loc.T("tw_copy")) && canCopy)
        {
            ImGui.SetClipboardText(outText);
            this.copiedUntil = Environment.TickCount64 + CopiedLabelMs;
        }

        ImGui.SameLine();
        if (ImGui.Button(Loc.T("tw_clear")))
            this.Clear();

        if (Environment.TickCount64 < this.copiedUntil)
        {
            ImGui.SameLine();
            ImGui.TextUnformatted(Loc.T("tw_copied"));
        }
    }

    private void Clear()
    {
        this.input = string.Empty;
        this.dirty = false;
        Interlocked.Increment(ref this.version); // invalide une traduction en cours
        lock (this.stateLock)
        {
            this.output = string.Empty;
            this.detected = string.Empty;
            this.busy = false;
            this.failed = false;
        }
    }

    private void Request()
    {
        this.dirty = false;

        var text = this.input.Trim();
        var target = this.config.OutgoingLanguage;
        var myVersion = Interlocked.Increment(ref this.version);

        if (text.Length == 0)
        {
            lock (this.stateLock)
            {
                this.output = string.Empty;
                this.detected = string.Empty;
                this.busy = false;
                this.failed = false;
            }

            return;
        }

        lock (this.stateLock)
        {
            this.busy = true;
            this.failed = false;
        }

        _ = Task.Run(async () =>
        {
            TranslationResult? result = null;
            try
            {
                result = await this.translator.TranslateAsync(text, target, this.config, this.ct);
            }
            catch (OperationCanceledException)
            {
                // timeout réseau ou arrêt du plugin : traité comme un échec
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Échec de traduction (/htranslate)");
            }

            lock (this.stateLock)
            {
                // Une saisie plus récente a été lancée entre-temps : ce résultat est périmé.
                if (myVersion != Volatile.Read(ref this.version))
                    return;

                this.busy = false;
                if (result == null)
                {
                    this.failed = true;
                    return;
                }

                this.failed = false;
                this.output = result.Text;
                this.detected = result.SourceLang;
            }
        });
    }

    private static string LanguageName(string code)
    {
        var baseCode = code.Split('-')[0];
        var index = Array.FindIndex(
            ConfigWindow.LanguageCodes,
            c => c.Split('-')[0].Equals(baseCode, StringComparison.OrdinalIgnoreCase));

        return index >= 0
            ? ConfigWindow.LanguageLabel(ConfigWindow.LanguageCodes[index])
            : code.ToUpperInvariant();
    }
}
