using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public RuntimeAnimatorController[] catAnimatorControllers;

    public CatBed[] catBeds;

    public CatTray catTray;

    public RectTransform catFlyContainer;

    public RectTransform testPos;
    private void Awake()
    {
        Instance = this;
    }
}
