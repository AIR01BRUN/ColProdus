using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Barre du haut de l'écran de jeu : affiche l'heure du monde et porte les boutons
/// d'accélération (PAUSE, 1X, 2X, 3X).
/// LabelDate  : jour et année du monde.
/// LabelClock : heure du monde au format HH:MM.
/// SpeedContent: reçoit les boutons de vitesse.
/// </summary>
public class TimeTemplate : TemplateUI
{
    public Label DateLabel { get; private set; }
    public Label ClockLabel { get; private set; }
    public VisualElement SpeedContent { get; private set; }

    public ButtonTemplate BtnPause { get; private set; }
    public ButtonTemplate BtnSpeed1 { get; private set; }
    public ButtonTemplate BtnSpeed2 { get; private set; }
    public ButtonTemplate BtnSpeed3 { get; private set; }

    // Un seul abonnement au changement de vitesse, même si la barre est reconstruite.
    private static TimeTemplate _listener;
    private EntityQuery _timeQuery;
    private World _timeQueryWorld;

    public TimeTemplate()
    {
        Template = UiTemplateLoader.Get("TimeTemplate");

        DateLabel = Template.Q<Label>("LabelDate");
        ClockLabel = Template.Q<Label>("LabelClock");
        SpeedContent = Template.Q<VisualElement>("SpeedContent");

        BtnPause = AddSpeedButton("PAUSE", GameSpeedController.TogglePause);
        BtnSpeed1 = AddSpeedButton("1X", () => GameSpeedController.SetSpeed(GameSpeedController.NormalIndex));
        BtnSpeed2 = AddSpeedButton("2X", () => GameSpeedController.SetSpeed(2));
        BtnSpeed3 = AddSpeedButton("3X", () => GameSpeedController.SetSpeed(3));

        if (_listener != null && _listener != this)
            GameSpeedController.OnSpeedChanged -= _listener.RefreshSpeedButtons;

        _listener = this;
        GameSpeedController.OnSpeedChanged += RefreshSpeedButtons;

        RefreshSpeedButtons(GameSpeedController.CurrentIndex);
        Refresh();

        UiEntityRegistry.RegisterSingleton(this);
    }

    private ButtonTemplate AddSpeedButton(string text, System.Action onClick)
    {
        var button = new ButtonTemplate(1);
        button.Template.style.height = 24;
        button.Setup(text, onClick);
        SpeedContent.Add(button.Template);
        return button;
    }

    /// <summary>Met en avant le bouton de la vitesse courante.</summary>
    public void RefreshSpeedButtons(int currentIndex)
    {
        if (BtnPause == null || BtnSpeed1 == null || BtnSpeed2 == null || BtnSpeed3 == null)
            return;

        BtnPause.SetActive(true);
        BtnSpeed1.SetActive(true);
        BtnSpeed2.SetActive(true);
        BtnSpeed3.SetActive(true);

        if (currentIndex == GameSpeedController.PauseIndex)
        {
            BtnPause.SetActive(false);
        }
        else if (currentIndex == GameSpeedController.NormalIndex)
        {
            BtnSpeed1.SetActive(false);
        }
        else
        {
            BtnSpeed2.SetActive(currentIndex != 2);
            BtnSpeed3.SetActive(currentIndex != 3);
        }
    }

    public override void Refresh()
    {
        RefreshTime();
        RefreshSpeedButtons(GameSpeedController.CurrentIndex);
    }

    /// <summary>Lit WorldTime et met à jour jour / année / heure.</summary>
    private void RefreshTime()
    {
        if (DateLabel == null || ClockLabel == null)
            return;

        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated)
        {
            DateLabel.text = "-- - ----";
            ClockLabel.text = "--:--";
            return;
        }

        var em = world.EntityManager;

        // La query est recréée si le monde a changé (nouvelle partie, chargement).
        if (_timeQueryWorld == null || !_timeQueryWorld.IsCreated || !_timeQueryWorld.Equals(world))
        {
            _timeQuery = em.CreateEntityQuery(ComponentType.ReadOnly<WorldTime>());
            _timeQueryWorld = world;
        }

        if (_timeQuery.IsEmptyIgnoreFilter)
        {
            DateLabel.text = "-- - ----";
            ClockLabel.text = "--:--";
            return;
        }

        var time = _timeQuery.GetSingleton<WorldTime>();
        DateLabel.text = $"JOUR {time.Day} - ANNEE {time.Year}";
        ClockLabel.text = $"{time.Hour:00}:{time.Minute:00}";
    }
}
