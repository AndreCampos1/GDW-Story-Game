using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class GameManager : MonoBehaviour
{
    public static GameManager Instance {get; private set;}

    public CastleData currentCastle;
    public List<Furniture> furnitures = new List<Furniture>();

    private int selectedCount;
    private DialogueController dialogueUI;


    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        dialogueUI = DialogueController.Instance;
    }

    public void OnFurnitureSelected(Furniture furniture, FurnitureOption option)
    {
        selectedCount++;
        if(selectedCount >= furnitures.Count) TriggerEnding();
    }

    public float GetMatchRation()
    {
        if (furnitures.Count == 0) return 0f;

        int match = 0;
        foreach(var furniture in furnitures)
        {
            if(furniture.isDone && furniture.Chosen.aesthetic == currentCastle.npcAesthetic) match++;
        }

        return (float)match / furnitures.Count;
    }

    public void TriggerEnding()
    {
        float ratio = GetMatchRation();
        EventDialogue ending;

        if(ratio >= currentCastle.goodRatio) ending = currentCastle.goodEnding;
        else if (ratio >= currentCastle.mixedRatio) ending = currentCastle.mixedEnding;
        else ending = currentCastle.badEnding;

        PlayEndingDialogue(ending);
    }

    //Ending dialogue
    private EventDialogue currentDialogue;
    private int dialogueIndex;
    private bool isDialogueActive, isTyping;

    void PlayEndingDialogue(EventDialogue data)
    {
        currentDialogue = data;
        dialogueIndex = 0;
        isDialogueActive = true;
        dialogueUI.SetNPCInfo(data.npcName, data.npcPortrait);
        dialogueUI.ShowDialogueUI(true);
        DisplayCurrentLine();
    }

    void Update()
    {
        if(!isDialogueActive) return;
        if(Input.GetMouseButtonDown(0)) NextLine();
    }

    void NextLine()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            dialogueUI.SetDialogueText(currentDialogue.dialogueLines[dialogueIndex]);
            isTyping = false;
            return;
        }

        dialogueUI.ClearChoices();

        foreach (DialogueChoice choice in currentDialogue.choices)
        {
            if (choice.dialogueIndex == dialogueIndex)
            {
                DisplayChoices(choice);
                return;
            }
        }

        if (currentDialogue.endDialogueLines.Length > dialogueIndex
            && currentDialogue.endDialogueLines[dialogueIndex])
        {
            EndDialogue();
            return;
        }

        if (++dialogueIndex < currentDialogue.dialogueLines.Length) DisplayCurrentLine();

        else EndDialogue();
    }

    IEnumerator TypeLine()
    {
        isTyping = true;
        dialogueUI.SetDialogueText("");

        string full = currentDialogue.dialogueLines[dialogueIndex];

        for (int i = 0; i <= full.Length; i++)
        {
            dialogueUI.SetDialogueText(full.Substring(0, i));
            yield return new WaitForSeconds(currentDialogue.typingSpeed);
        }

        isTyping = false;

        if (currentDialogue.autoProgressLines.Length > dialogueIndex && currentDialogue.autoProgressLines[dialogueIndex])
        {
            yield return new WaitForSeconds(currentDialogue.autoProgressDelay);
            NextLine();
        }
    }

    void DisplayChoices(DialogueChoice choice)
    {
        int count = Mathf.Min(choice.choices.Length, choice.nextDialogueIndexes.Length);
        for (int i = 0; i < count; i++)
        {
            int nextIndex = choice.nextDialogueIndexes[i];
            dialogueUI.CreateChoiceButton(choice.choices[i], () => ChooseBranch(nextIndex));
        }
    }

    void ChooseBranch(int nextIndex)
    {
        dialogueIndex = nextIndex;
        dialogueUI.ClearChoices();
        DisplayCurrentLine();
    }

    void DisplayCurrentLine()
    {
        StopAllCoroutines();
        StartCoroutine(TypeLine());
    }

    public void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;
        isTyping = false;
        currentDialogue = null;
        dialogueUI.SetDialogueText("");
        dialogueUI.ClearChoices();
        dialogueUI.ShowDialogueUI(false);
    }
}
