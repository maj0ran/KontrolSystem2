﻿using UnityEngine;

namespace KontrolSystem.KSP.Runtime.KSPUI.UGUI;

public class UGUIElement {
    protected Vector2 minSize;

    protected UGUIElement(GameObject gameObject, Vector2 minSize) {
        GameObject = gameObject;
        this.minSize = minSize;
    }

    public GameObject GameObject { get; protected set; }

    public RectTransform Transform => GameObject.GetComponent<RectTransform>();

    public virtual Vector2 MinSize {
        get => minSize;
        set => minSize = value;
    }

    public virtual Vector2 Layout() {
        return MinSize;
    }

    public void Destroy() {
        Object.Destroy(GameObject);
    }

    public static UGUIElement VScrollView(UGUIElement content, Vector2 minSize) {
        var scrollView = UIFactory.Instance!.CreateScrollView(content.GameObject);
        return new UGUIElement(scrollView, minSize);
    }

    public static UGUIElement Spacer(Vector2 minSize) {
        var gameObject = new GameObject("Spacer", typeof(RectTransform));
        return new UGUIElement(gameObject, minSize);
    }
}
