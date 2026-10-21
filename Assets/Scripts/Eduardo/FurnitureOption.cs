using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public enum AestheticTag {Cosmic, Evil, Royal}

[CreateAssetMenu(fileName = "NewFurnitureOption", menuName = "Furniture Option")]
public class FurnitureOption : ScriptableObject
{
    public string label;
    public Texture2D furnitureTexture;
    public AestheticTag aesthetic;
    public EventDialogue reactionDialogue; //NPC comment after choosing
}
