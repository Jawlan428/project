using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PollOptionButton : MonoBehaviour
{
    public PollBoard pollBoard;
    public int optionIndex = 0;
    public TMP_Text label;

    void Awake()
    {
        if (pollBoard == null)
            pollBoard = GetComponentInParent<PollBoard>();

        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);

        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);

        UpdateLabel();
        Debug.Log("[PollOptionButton] Wired option " + optionIndex);
    }

    void OnValidate()
    {
        if (label == null)
            label = GetComponentInChildren<TMP_Text>(true);
        UpdateLabel();
    }

    public void UpdateLabel()
    {
        if (label == null || pollBoard == null || pollBoard.options == null) return;
        if (optionIndex < 0 || optionIndex >= pollBoard.options.Length) return;
        label.text = pollBoard.options[optionIndex];
    }

    void OnClicked()
    {
        if (pollBoard == null)
        {
            Debug.LogWarning("[PollOptionButton] PollBoard not assigned.");
            return;
        }
        pollBoard.Vote(optionIndex);
    }
}

