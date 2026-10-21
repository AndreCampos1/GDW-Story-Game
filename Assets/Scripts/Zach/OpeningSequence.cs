using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class OpeningClient
{
    public string displayName;
    public Sprite sprite;
    public EventDialogue dialogue;
    public CastleData castle;
    public string sceneName;
    public ClientPickCard pickCard;
}

public static class ClientSelection
{
    public static string ClientName { get; private set; }
    public static CastleData Castle { get; private set; }

    public static void Set(OpeningClient c)
    {
        ClientName = c.displayName;
        Castle = c.castle;
    }
}

public class OpeningSequence : MonoBehaviour
{
    [Header("Clients (they speak in this order)")]
    public OpeningClient[] clients;

    [Header("Scene references")]
    public GameObject pickScreen;
    public CanvasGroup fadeGroup;
    public AudioSource voiceSource;

    [Header("Timing")]
    public float startDelay = 0.6f;
    public float fadeTime = 0.6f;
    public float portraitPopTime = 0.35f;
    public float portraitPopOffset = 220f;
    public int blipEveryNChars = 2;

    private bool choosing;
    private Vector2 portraitBasePos;

    IEnumerator Start()
    {

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (pickScreen != null) pickScreen.SetActive(false);
        if (fadeGroup != null) fadeGroup.alpha = 1f;

        yield return null;

        DialogueController ui = DialogueController.Instance;
        if (ui == null)
        {
            Debug.LogError("OpeningSequence: no DialogueController in the scene.");
            yield break;
        }

        portraitBasePos = ui.portraitImage.rectTransform.anchoredPosition;

        yield return Fade(1f, 0f);
        yield return new WaitForSeconds(startDelay);

        ui.ShowDialogueUI(true);
        foreach (OpeningClient c in clients) yield return PlayClient(ui, c);
        ui.SetDialogueText("");
        ui.ShowDialogueUI(false);

        ShowPickScreen();
    }

    IEnumerator PlayClient(DialogueController ui, OpeningClient c)
    {
        EventDialogue d = c.dialogue;
        if (d == null || d.dialogueLines == null || d.dialogueLines.Length == 0) yield break;

        string npcName = string.IsNullOrEmpty(d.npcName) ? c.displayName : d.npcName;
        Sprite portrait = d.npcPortrait != null ? d.npcPortrait : c.sprite;

        ui.ClearChoices();
        ui.SetNPCInfo(npcName, portrait);
        ui.SetDialogueText("");
        SetPortraitAlpha(ui, 0f);
        yield return PopPortrait(ui);

        for (int i = 0; i < d.dialogueLines.Length; i++)
        {
            yield return TypeLine(ui, d, d.dialogueLines[i]);

            bool auto = d.autoProgressLines != null && i < d.autoProgressLines.Length && d.autoProgressLines[i];
            if (auto) yield return new WaitForSeconds(d.autoProgressDelay);
            else yield return WaitForAdvance();

            bool end = d.endDialogueLines != null && i < d.endDialogueLines.Length && d.endDialogueLines[i];
            if (end) break;
        }
    }

    IEnumerator TypeLine(DialogueController ui, EventDialogue d, string full)
    {
        ui.SetSpeaking(true);
        bool skipped = false;

        for (int n = 1; n <= full.Length && !skipped; n++)
        {
            ui.SetDialogueText(full.Substring(0, n));

            if (voiceSource != null && d.voiceSound != null && n % Mathf.Max(1, blipEveryNChars) == 0 && !char.IsWhiteSpace(full[n - 1]))
            {
                voiceSource.pitch = d.voicePitch;
                voiceSource.PlayOneShot(d.voiceSound);
            }

            float t = 0f;
            while (t < d.typingSpeed)
            {
                if (AdvancePressed()) { skipped = true; break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        ui.SetDialogueText(full);
        ui.SetSpeaking(false);
    }

    IEnumerator WaitForAdvance()
    {
        yield return null;
        while (!AdvancePressed()) yield return null;
        yield return null;
    }

    IEnumerator PopPortrait(DialogueController ui)
    {
        RectTransform rt = ui.portraitImage.rectTransform;
        float t = 0f;
        while (t < portraitPopTime)
        {
            float k = Mathf.Clamp01(t / portraitPopTime);
            rt.anchoredPosition = portraitBasePos + Vector2.down * (1f - EaseOutBack(k)) * portraitPopOffset;
            SetPortraitAlpha(ui, Mathf.Clamp01(k * 2f));
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        rt.anchoredPosition = portraitBasePos;
        SetPortraitAlpha(ui, 1f);
    }

    static void SetPortraitAlpha(DialogueController ui, float a)
    {
        Color col = ui.portraitImage.color;
        col.a = a;
        ui.portraitImage.color = col;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }

    void ShowPickScreen()
    {
        if (pickScreen != null) pickScreen.SetActive(true);

        foreach (OpeningClient c in clients)
        {
            if (c.pickCard == null) continue;
            OpeningClient captured = c;
            c.pickCard.Bind(c.displayName, c.sprite, () => Choose(captured));
        }
    }

    void Choose(OpeningClient c)
    {
        if (choosing) return;
        choosing = true;
        foreach (OpeningClient other in clients)
            if (other.pickCard != null) other.pickCard.SetInteractable(false);

        ClientSelection.Set(c);
        StartCoroutine(LoadRoutine(c));
    }

    IEnumerator LoadRoutine(OpeningClient c)
    {
        if (!Application.CanStreamedLevelBeLoaded(c.sceneName))
        {
            Debug.LogError("OpeningSequence: scene '" + c.sceneName + "' is not in Build Settings (File > Build Profiles > Scene List).");
            foreach (OpeningClient other in clients)
                if (other.pickCard != null) other.pickCard.SetInteractable(true);
            choosing = false;
            yield break;
        }

        yield return Fade(0f, 1f);
        SceneManager.LoadScene(c.sceneName);
    }

    IEnumerator Fade(float from, float to)
    {
        if (fadeGroup == null) yield break;
        float t = 0f;
        while (t < fadeTime)
        {
            fadeGroup.alpha = Mathf.Lerp(from, to, t / fadeTime);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        fadeGroup.alpha = to;
    }

    static bool AdvancePressed()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var m = UnityEngine.InputSystem.Mouse.current;
        var k = UnityEngine.InputSystem.Keyboard.current;
        return (m != null && m.leftButton.wasPressedThisFrame)
            || (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame));
#else
        return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }
}