using UnityEngine;
using UnityEngine.UIElements;

public class ProgressBarTemplate : TemplateUI
{
    public VisualElement Background { get; private set; }
    public VisualElement Fill { get; private set; }
    public Label Value { get; private set; }

    private float _max = 1f;
    private float _value;
    private string _format = "{0:0.#} / {1:0.#}";

    public bool InvertFill { get; set; }

    public ProgressBarTemplate()
    {
        Template = UiTemplateLoader.Get("ProgressBar_0");

        Background = Template;
        Fill = Template.Q<VisualElement>("Fill");
        Value = Template.Q<Label>("Value");

        Refresh();
    }

    public void Setup(float max, float value = 0f)
    {
        _max = max;
        _value = value;
        Refresh();
    }

    public void SetMax(float max)
    {
        _max = max;
        Refresh();
    }

    public void SetValue(float value)
    {
        _value = value;
        Refresh();
    }

    public override void Refresh()
    {
        var ratio = _max <= 0f ? 0f : Mathf.Clamp01(_value / _max);
        if (InvertFill)
            ratio = 1f - ratio;

        if (Fill != null)
            Fill.style.width = Length.Percent(ratio * 100f);

        if (Value != null)
            Value.text = string.Format(_format, _value, _max);
    }
}
