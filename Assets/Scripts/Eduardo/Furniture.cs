using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Furniture : MonoBehaviour
{
    public FurnitureOption[] options;

    [Header("Shader Texture Settings")]
    public Renderer furnitureRenderer;
    public string texturePropertyName = "_MainTex";

    public FurnitureOption Chosen {get; private set;}
    public bool isDone => Chosen != null;

    private DialogueController dialogueUI;
    private bool isDialogueActive, isTyping;
    private EventDialogue currentDialogue;
    private int dialogueIndex;

    private Material targetMaterial;

    void Start()
    {
        dialogueUI = DialogueController.Instance;

        if (furnitureRenderer != null)
        {
            targetMaterial = furnitureRenderer.material;
        }
    }

    public bool CanInteract()
    {
        return !isDone || isDialogueActive;
    }

    public void Interact()
    {
        if (isDone && !isDialogueActive) return;

        if(isDialogueActive) NextLine();
        else StartSelection();
    }

    void StartSelection()
    {
        isDialogueActive = true;
        dialogueUI.SetNPCInfo("Choose The Furniture Carefully", null);
        dialogueUI.ShowDialogueUI(true);
        dialogueUI.SetDialogueText("Pick an aesthetic:");
        dialogueUI.ClearChoices();

        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            dialogueUI.CreateChoiceButton(opt.label, () => ChooseOption(opt));
        }
    }

    void ChooseOption(FurnitureOption opt)
    {
        Chosen = opt;
        dialogueUI.ClearChoices();
        GameManager.Instance.OnFurnitureSelected(this, opt);

        ApplyTexture(opt.furnitureTexture);

        if (opt.reactionDialogue != null) PlayReaction(opt.reactionDialogue);
        else EndDialogue();
    }

    void ApplyTexture(Texture2D tex)
    {
        if(tex == null || targetMaterial == null) return;
        var block = new MaterialPropertyBlock();
        furnitureRenderer.GetPropertyBlock(block);
        block.SetTexture(texturePropertyName, tex);
        furnitureRenderer.SetPropertyBlock(block);
    }

    void PlayReaction(EventDialogue data)
    {
        currentDialogue = data;
        dialogueIndex = 0;
        dialogueUI.SetNPCInfo(data.npcName, data.npcPortrait);
        dialogueUI.SetDialogueText("");
        DisplayCurrentLine();
    }

    void NextLine()
    {
        if (isTyping)
        {
            //Skip typing animation and show the full line(Player Input)
            StopAllCoroutines();
            dialogueUI.SetDialogueText(currentDialogue.dialogueLines[dialogueIndex]);
            isTyping = false;
            return;
        }

        dialogueUI.ClearChoices(); //Clear choices

        foreach(DialogueChoice dialogueChoice in currentDialogue.choices) //Check if choices & display
        {
            if (dialogueChoice.dialogueIndex == dialogueIndex)
            {
                DisplayChoices(dialogueChoice);
                return;
            }
        }

        if(currentDialogue.endDialogueLines.Length > dialogueIndex && currentDialogue.endDialogueLines[dialogueIndex])// Check endDialogueLines
        {
            EndDialogue();
            return;
        }

        if (++dialogueIndex < currentDialogue.dialogueLines.Length)
        {
            DisplayCurrentLine();
        }
        else
        {
            EndDialogue();
        }
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
