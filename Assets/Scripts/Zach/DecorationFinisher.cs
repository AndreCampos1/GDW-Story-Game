using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class DecorationFinisher : MonoBehaviour
{
    public enum Ending { Good, Ok, Bad }

    [Header("Rooms")]
    public RenovationArea[] areas;
    public RenovationAesthetic fallbackAesthetic;

    [Header("Endings")]
    public EventDialogue goodEnding;
    public EventDialogue okEnding;
    public EventDialogue badEnding;

    [Header("Ending Thresholds (0-1)")]
    [Range(0f, 1f)] public float goodThreshold = 1f;
    [Range(0f, 1f)] public float okThreshold = 0.6f;

    [Header("Hold To Finish")]
    public KeyCode finishKey = KeyCode.F;
    public float holdSeconds = 1.5f;
    public string hintMessage = "Hold F to finish decorating";

    [Header("HUD")]
    public GameObject hudRoot;
    public TMP_Text hintText;
    public RectTransform holdFill;
    public GameObject resultPanel;
    public TMP_Text resultText;

    [Header("Client")]
    public string clientName;
    public Sprite clientPortrait;

    [Header("After Finishing")]
    public Behaviour[] disableOnFinish;
    public AudioSource voiceSource;
    public int blipEveryNChars = 2;
    public bool logCounts = true;
    public UnityEvent onEndingFinished;

    public bool Finished { get; private set; }
    public int LastMatches { get; private set; }
    public int LastTotal { get; private set; }
    public float LastRatio { get; private set; }
    public Ending LastEnding { get; private set; }

    private float held;

    void Awake()
    {
        if (areas == null || areas.Length == 0)
            areas = FindObjectsByType<RenovationArea>(FindObjectsSortMode.None);
    }

    void Start()
    {
        if (hintText != null) hintText.text = hintMessage;
        SetResultVisible(false);
        if (hudRoot != null) hudRoot.SetActive(false);
        SetFill(0f);

        if (areas == null || areas.Length == 0)
            Debug.LogWarning("DecorationFinisher: no RenovationArea found in this scene.");
    }

    void Update()
    {
        if (Finished) return;

        bool available = AnyAreaActive();
        if (hudRoot != null && hudRoot.activeSelf != available) hudRoot.SetActive(available);

        if (available && FinishHeld()) held += Time.unscaledDeltaTime;
        else held = Mathf.Max(0f, held - Time.unscaledDeltaTime * 3f);

        SetFill(holdSeconds > 0f ? held / holdSeconds : 1f);

        if (available && held >= holdSeconds) Finish();
    }

    public float Evaluate(out int matches, out int total)
    {
        matches = 0;
        total = 0;

        FurnitureSpot[] spots = FindObjectsByType<FurnitureSpot>(FindObjectsSortMode.None);
        foreach (FurnitureSpot spot in spots)
        {
            total++;

            RenovationArea area = spot.GetComponentInParent<RenovationArea>();
            RenovationAesthetic target = area != null ? area.targetAesthetic : fallbackAesthetic;

            bool match = spot.HasFurniture && spot.CurrentAesthetic == target;
            if (match) matches++;

            if (logCounts)
                Debug.Log("  " + spot.name + ": " + (spot.HasFurniture ? spot.CurrentAesthetic.ToString() : "empty") + " (needs " + target + ") " + (match ? "MATCH" : "no"));
        }

        return total > 0 ? (float)matches / total : 0f;
    }

    public Ending GetEnding(float ratio)
    {
        if (ratio >= goodThreshold - 0.0001f) return Ending.Good;
        if (ratio >= okThreshold - 0.0001f) return Ending.Ok;
        return Ending.Bad;
    }

    public void Finish()
    {
        if (Finished) return;
        Finished = true;

        int matches, total;
        LastRatio = Evaluate(out matches, out total);
        LastMatches = matches;
        LastTotal = total;
        LastEnding = GetEnding(LastRatio);

        int percent = Mathf.RoundToInt(LastRatio * 100f);
        if (logCounts) Debug.Log("DecorationFinisher: " + matches + "/" + total + " match (" + percent + "%) -> " + LastEnding + " ending");

        if (disableOnFinish != null)
            foreach (Behaviour b in disableOnFinish)
                if (b != null) b.enabled = false;

        if (areas != null)
            foreach (RenovationArea a in areas)
                if (a != null) a.End();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (hudRoot != null) hudRoot.SetActive(false);
        SetFill(0f);

        EventDialogue d = LastEnding == Ending.Good ? goodEnding : LastEnding == Ending.Ok ? okEnding : badEnding;
        StartCoroutine(EndingRoutine(d));
    }

    IEnumerator EndingRoutine(EventDialogue d)
    {
        DialogueController ui = DialogueController.Instance;

        if (ui == null || d == null || d.dialogueLines == null || d.dialogueLines.Length == 0)
        {
            Debug.LogWarning("DecorationFinisher: missing ending dialogue (" + LastEnding + ") or DialogueController.");
            ShowResult();
            onEndingFinished.Invoke();
            yield break;
        }

        string npc = !string.IsNullOrEmpty(d.npcName) ? d.npcName : clientName;
        Sprite portrait = d.npcPortrait != null ? d.npcPortrait : clientPortrait;

        ui.ClearChoices();
        ui.SetNPCInfo(string.IsNullOrEmpty(npc) ? "" : npc, portrait);
        if (ui.portraitImage != null)
        {
            Color c = ui.portraitImage.color;
            c.a = 1f;
            ui.portraitImage.color = c;
        }
        ui.SetDialogueText("");
        ui.ShowDialogueUI(true);
        yield return null;

        for (int i = 0; i < d.dialogueLines.Length; i++)
        {
            yield return TypeLine(ui, d, d.dialogueLines[i]);

            bool auto = d.autoProgressLines != null && i < d.autoProgressLines.Length && d.autoProgressLines[i];
            if (auto) yield return new WaitForSecondsRealtime(d.autoProgressDelay);
            else yield return WaitForAdvance();

            bool end = d.endDialogueLines != null && i < d.endDialogueLines.Length && d.endDialogueLines[i];
            if (end) break;
        }

        ui.SetDialogueText("");
        ui.ShowDialogueUI(false);
        ShowResult();
        onEndingFinished.Invoke();
    }

    void ShowResult()
    {
        int percent = Mathf.RoundToInt(LastRatio * 100f);
        string label = LastEnding == Ending.Good ? "GOOD" : LastEnding == Ending.Ok ? "MIXED" : "BAD";
        Color color = LastEnding == Ending.Good ? new Color(0.45f, 0.95f, 0.5f)
                    : LastEnding == Ending.Ok ? new Color(1f, 0.85f, 0.35f)
                    : new Color(1f, 0.4f, 0.4f);

        if (resultText != null)
        {
            resultText.text = percent + "%  -  " + label + "\n<size=60%>" + LastMatches + " / " + LastTotal + " matched</size>";
            resultText.color = color;
        }
        SetResultVisible(true);
    }

    void SetResultVisible(bool visible)
    {
        if (resultPanel != null) resultPanel.SetActive(visible);
        else if (resultText != null) resultText.gameObject.SetActive(visible);
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

    bool AnyAreaActive()
    {
        if (areas == null) return false;
        foreach (RenovationArea a in areas)
            if (a != null && (a.IsActive || !a.activateOnEnter)) return true;
        return false;
    }

    void SetFill(float value)
    {
        if (holdFill == null) return;
        Vector2 max = holdFill.anchorMax;
        max.x = Mathf.Clamp01(value);
        holdFill.anchorMax = max;
    }

    bool FinishHeld()
    {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var k = UnityEngine.InputSystem.Keyboard.current;
        return k != null && k.fKey.isPressed;
#else
        return Input.GetKey(finishKey);
#endif
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