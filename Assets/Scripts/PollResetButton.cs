using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class PollResetButton : MonoBehaviour
{
    public PollBoard pollBoard;

    void Awake()
    {
        if (pollBoard == null)
            pollBoard = GetComponentInParent<PollBoard>();

        Button button = GetComponent<Button>();
        button.onClick.AddListener(OnClicked);
    }

    void OnClicked()
    {
        if (pollBoard == null)
        {
            Debug.LogWarning("[PollResetButton] PollBoard not assigned.");
            return;
        }
        pollBoard.ResetVotes();
    }
}

